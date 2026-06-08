// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Utilities;
using Microsoft.Build.Shared;
using Microsoft.NET.StringTools;

namespace Microsoft.Build.Internal;

/// <summary>
///  This class contains utility methods for the MSBuild engine.
/// </summary>
internal static class CommunicationsUtilities
{
    /// <summary>
    ///  Indicates to the NodeEndpoint that all the various parts of the Handshake have been sent.
    /// </summary>
    private const int EndOfHandshakeSignal = -0x2a2a2a2a;

    /// <summary>
    ///  The version of the handshake. This should be updated each time the handshake structure is altered.
    /// </summary>
    internal const byte handshakeVersion = 0x01;

    /// <summary>
    ///  The timeout to connect to a node.
    /// </summary>
    private const int DefaultNodeConnectionTimeout = 900 * 1000; // 15 minutes; enough time that a dev will typically do another build in this time

    /// <summary>
    ///  Whether to trace communications.
    /// </summary>
    private static readonly bool s_trace = Traits.Instance.DebugNodeCommunication;

    /// <summary>
    ///  Lock trace to ensure we are logging in serial fashion.
    /// </summary>
    private static readonly LockType s_traceLock = new LockType();

    /// <summary>
    ///  Place to dump trace.
    /// </summary>
    private static string? s_debugDumpPath;

    /// <summary>
    ///  Ticks at last time logged.
    /// </summary>
    private static long s_lastLoggedTicks = DateTime.UtcNow.Ticks;

    /// <summary>
    ///  On Windows, environment variables should be case-insensitive;
    ///  on Unix-like systems, they should be case-sensitive, but this might be a breaking change in an edge case.
    ///  https://github.com/dotnet/msbuild/issues/12858.
    /// </summary>
    internal static StringComparer EnvironmentVariableComparer => StringComparer.OrdinalIgnoreCase;

    /// <summary>
    ///  Gets the node connection timeout.
    /// </summary>
    internal static int NodeConnectionTimeout
        => EnvironmentUtilities.GetValueAsInt32OrDefault("MSBUILDNODECONNECTIONTIMEOUT", DefaultNodeConnectionTimeout);

    /// <summary>
    ///  A set of environment variables cached from the last time we called GetEnvironmentVariables.
    ///  Used to avoid allocations if the environment has not changed.
    /// </summary>
    private static EnvironmentState? s_environmentState;

    /// <summary>
    ///  Get environment block.
    /// </summary>
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static extern unsafe char* GetEnvironmentStrings();

    /// <summary>
    ///  Free environment block.
    /// </summary>
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static extern unsafe bool FreeEnvironmentStrings(char* pStrings);

#if NETFRAMEWORK
    /// <summary>
    ///  Set environment variable P/Invoke.
    /// </summary>
    [DllImport("kernel32.dll", EntryPoint = "SetEnvironmentVariable", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetEnvironmentVariableNative(string name, string? value);

    /// <summary>
    ///  Sets an environment variable using P/Invoke to workaround the .NET Framework BCL implementation.
    /// </summary>
    /// <remarks>
    ///  .NET Framework implementation of SetEnvironmentVariable checks the length of the value and throws an exception if
    ///  it's greater than or equal to 32,767 characters. This limitation does not exist on modern Windows or .NET.
    /// </remarks>
    internal static void SetEnvironmentVariable(string name, string? value)
    {
        if (!SetEnvironmentVariableNative(name, value))
        {
            throw Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error());
        }
    }
#endif

    /// <summary>
    ///  A container to atomically swap a cached set of environment variables and the block string used to create it.
    ///  The environment block property will only be set on Windows, since on Unix we need to directly call
    ///  Environment.GetEnvironmentVariables().
    /// </summary>
    private sealed record class EnvironmentState(
        FrozenDictionary<string, string> EnvironmentVariables,
        ReadOnlyMemory<char> EnvironmentBlock = default);

    /// <summary>
    ///  Returns key value pairs of environment variables in a new dictionary
    ///  with a case-insensitive key comparer.
    /// </summary>
    /// <remarks>
    ///  Copied from the BCL implementation to eliminate some expensive security asserts on .NET Framework.
    /// </remarks>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static FrozenDictionary<string, string> GetEnvironmentVariablesWindows()
    {
        // The FrameworkDebugUtils static constructor can set the MSBUILDDEBUGPATH environment variable to propagate the debug path to out of proc nodes.
        // Need to ensure that constructor is called before this method returns in order to capture its env var write.
        // Otherwise the env var is not captured and thus gets deleted when RequestBuilder resets the environment based on the cached results of this method.
        FrameworkErrorUtilities.VerifyThrowInternalNull(FrameworkDebugUtils.ProcessInfoString, nameof(FrameworkDebugUtils.DebugPath));

        unsafe
        {
            char* pEnvironmentBlock = null;

            try
            {
                pEnvironmentBlock = GetEnvironmentStrings();
                if (pEnvironmentBlock == null)
                {
                    throw new OutOfMemoryException();
                }

                // Search for terminating \0\0 (two unicode \0's).
                char* pEnvironmentBlockEnd = pEnvironmentBlock;
                while (!(*pEnvironmentBlockEnd == '\0' && *(pEnvironmentBlockEnd + 1) == '\0'))
                {
                    pEnvironmentBlockEnd++;
                }
                long stringBlockLength = pEnvironmentBlockEnd - pEnvironmentBlock;

                // Avoid allocating any objects if the environment still matches the last state.
                // We speed this up by comparing the full block instead of individual key-value pairs.
                ReadOnlySpan<char> stringBlock = new(pEnvironmentBlock, (int)stringBlockLength);

                if (s_environmentState is { } lastState && lastState.EnvironmentBlock.Span.SequenceEqual(stringBlock))
                {
                    return lastState.EnvironmentVariables;
                }

                Dictionary<string, string> table = new(200, EnvironmentVariableComparer); // Razzle has 150 environment variables

                // Copy strings out, parsing into pairs and inserting into the table.
                // The first few environment variable entries start with an '='!
                // The current working directory of every drive (except for those drives
                // you haven't cd'ed into in your DOS window) are stored in the
                // environment block (as =C:=pwd) and the program's exit code is
                // as well (=ExitCode=00000000)  Skip all that start with =.
                // Read docs about Environment Blocks on MSDN's CreateProcess page.

                // Format for GetEnvironmentStrings is:
                // (=HiddenVar=value\0 | Variable=value\0)* \0
                // See the description of Environment Blocks in MSDN's
                // CreateProcess page (null-terminated array of null-terminated strings).
                // Note the =HiddenVar's aren't always at the beginning.
                for (int i = 0; i < stringBlockLength; i++)
                {
                    int startKey = i;

                    // Skip to key
                    // On some old OS, the environment block can be corrupted.
                    // Some lines will not have '=', so we need to check for '\0'.
                    while (*(pEnvironmentBlock + i) is not ('=' or '\0'))
                    {
                        i++;
                    }

                    if (*(pEnvironmentBlock + i) == '\0')
                    {
                        continue;
                    }

                    // Skip over environment variables starting with '='
                    if (i - startKey == 0)
                    {
                        while (*(pEnvironmentBlock + i) != 0)
                        {
                            i++;
                        }

                        continue;
                    }

                    string key = Strings.WeakIntern(new ReadOnlySpan<char>(pEnvironmentBlock + startKey, length: i - startKey));

                    i++;

                    // skip over '='
                    int startValue = i;

                    while (*(pEnvironmentBlock + i) != 0)
                    {
                        // Read to end of this entry
                        i++;
                    }

                    string value = Strings.WeakIntern(new ReadOnlySpan<char>(pEnvironmentBlock + startValue, length: i - startValue));

                    // skip over 0 handled by for loop's i++
                    table[key] = value;
                }

                // Update with the current state.
                EnvironmentState currentState =
                    new(table.ToFrozenDictionary(EnvironmentVariableComparer), stringBlock.ToArray());
                s_environmentState = currentState;
                return currentState.EnvironmentVariables;
            }
            finally
            {
                if (pEnvironmentBlock != null)
                {
                    FreeEnvironmentStrings(pEnvironmentBlock);
                }
            }
        }
    }

#if NET
    /// <summary>
    ///  Sets an environment variable using <see cref="Environment.SetEnvironmentVariable(string,string)" />.
    /// </summary>
    internal static void SetEnvironmentVariable(string name, string? value)
        => Environment.SetEnvironmentVariable(name, value);
#endif

    /// <summary>
    ///  <para>
    ///   Returns key value pairs of environment variables in a read-only dictionary
    ///   with a case-insensitive key comparer.
    ///  </para>
    ///  <para>
    ///   If the environment variables have not changed since the last time
    ///   this method was called, the same dictionary instance will be returned.
    ///  </para>
    /// </summary>
    internal static FrozenDictionary<string, string> GetEnvironmentVariables()
    {
        // Always call the native method on Windows, as we'll be able to avoid the internal
        // string and Hashtable allocations caused by Environment.GetEnvironmentVariables().
        if (NativeMethods.IsWindows)
        {
            return GetEnvironmentVariablesWindows();
        }

        IDictionary vars = Environment.GetEnvironmentVariables();

        // Directly use the enumerator since Current will box DictionaryEntry.
        IDictionaryEnumerator enumerator = vars.GetEnumerator();

        // If every key-value pair matches the last state, return a cached dictionary.
        if (s_environmentState?.EnvironmentVariables is { } lastEnvironmentVariables &&
            vars.Count == lastEnvironmentVariables.Count)
        {
            bool sameState = true;

            while (enumerator.MoveNext() && sameState)
            {
                DictionaryEntry entry = enumerator.Entry;
                if (!lastEnvironmentVariables.TryGetValue((string)entry.Key, out string? value)
                    || !string.Equals((string?)entry.Value, value, StringComparison.Ordinal))
                {
                    sameState = false;
                    break;
                }
            }

            if (sameState)
            {
                return lastEnvironmentVariables;
            }
        }

        // Otherwise, allocate and update with the current state.
        Dictionary<string, string> table = new(vars.Count, EnvironmentVariableComparer);

        enumerator.Reset();
        while (enumerator.MoveNext())
        {
            DictionaryEntry entry = enumerator.Entry;
            string key = Strings.WeakIntern((string)entry.Key);
            string value = Strings.WeakIntern((string?)entry.Value ?? string.Empty);
            table[key] = value;
        }

        EnvironmentState newState = new(table.ToFrozenDictionary(EnvironmentVariableComparer));
        s_environmentState = newState;

        return newState.EnvironmentVariables;
    }

    /// <summary>
    ///  Updates the environment to match the provided dictionary.
    /// </summary>
    internal static void SetEnvironment(IDictionary<string, string> newEnvironment)
    {
        if (newEnvironment != null)
        {
            // First, delete all no longer set variables
            FrozenDictionary<string, string> currentEnvironment = GetEnvironmentVariables();

            foreach (var (key, value) in currentEnvironment)
            {
                if (!newEnvironment.ContainsKey(key))
                {
                    SetEnvironmentVariable(key, value: null);
                }
            }

            // Then, make sure the new ones have their new values.
            foreach (var (key, value) in newEnvironment)
            {
                if (!currentEnvironment.TryGetValue(key, out string? currentValue) || currentValue != value)
                {
                    SetEnvironmentVariable(key, value);
                }
            }
        }
    }

    /// <summary>
    ///  Indicate to the client that all elements of the Handshake have been sent.
    /// </summary>
    internal static void WriteEndOfHandshakeSignal(this PipeStream stream)
        => stream.WriteIntForHandshake(EndOfHandshakeSignal);

    /// <summary>
    ///  Extension method to write a series of bytes to a stream.
    /// </summary>
    internal static void WriteIntForHandshake(this PipeStream stream, int value)
    {
        byte[] bytes = BitConverter.GetBytes(value);

        // We want to read the long and send it from left to right (this means big endian)
        // if we are little endian we need to reverse the array to keep the left to right reading
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }

        FrameworkErrorUtilities.VerifyThrow(bytes.Length == 4, "Int should be 4 bytes");

        stream.Write(bytes, 0, bytes.Length);
    }

    internal static bool TryReadEndOfHandshakeSignal(
        this PipeStream stream,
        bool isProvider,
#if NET
        int timeout,
#endif
        out HandshakeResult result)
    {
        // Accept only the first byte of the EndOfHandshakeSignal
        if (TryReadIntForHandshake(
            stream,
            byteToAccept: null,
#if NET
            timeout,
#endif
            out HandshakeResult innerResult))
        {
            byte negotiatedPacketVersion = 1;

            if (innerResult.Value != EndOfHandshakeSignal)
            {
                // If the received handshake part is not PacketVersionFromChildMarker it means we communicate with the host that does not support packet version negotiation.
                // Fallback to the old communication validation pattern.
                if (innerResult.Value != Handshake.PacketVersionFromChildMarker)
                {
                    result = CreateVersionMismatchResult(isProvider, innerResult.Value);
                    return false;
                }

                // We detected packet version marker, now let's read actual PacketVersion
                if (!TryReadIntForHandshake(
                    stream,
                    byteToAccept: null,
#if NET
                    timeout,
#endif
                    out HandshakeResult versionResult))
                {
                    result = versionResult;
                    return false;
                }

                byte childVersion = (byte)versionResult.Value;
                negotiatedPacketVersion = NodePacketTypeExtensions.GetNegotiatedPacketVersion(childVersion);
                Trace($"Node PacketVersion: {childVersion}, Local: {NodePacketTypeExtensions.PacketVersion}, Negotiated: {negotiatedPacketVersion}");

                if (!TryReadIntForHandshake(
                    stream,
                    byteToAccept: null,
#if NET
                    timeout,
#endif
                    out innerResult))
                {
                    result = innerResult;
                    return false;
                }

                if (innerResult.Value != EndOfHandshakeSignal)
                {
                    result = CreateVersionMismatchResult(isProvider, innerResult.Value);
                    return false;
                }
            }

            result = HandshakeResult.Success(0, negotiatedPacketVersion);
            return true;
        }
        else
        {
            result = innerResult;
            return false;
        }
    }

    private static HandshakeResult CreateVersionMismatchResult(bool isProvider, int receivedValue)
    {
        var errorMessage = isProvider
            ? $"Handshake failed on part {receivedValue}. Probably the client is a different MSBuild build."
            : $"Expected end of handshake signal but received {receivedValue}. Probably the host is a different MSBuild build.";

        Trace(errorMessage);

        return HandshakeResult.Failure(HandshakeStatus.VersionMismatch, errorMessage);
    }

    /// <summary>
    /// Extension method to read a series of bytes from a stream.
    /// If specified, leading byte matches one in the supplied array if any, returns rejection byte and throws IOException.
    /// </summary>
    internal static bool TryReadIntForHandshake(
        this PipeStream stream,
        byte? byteToAccept,
#if NET
        int timeout,
#endif
        out HandshakeResult result)
    {
        byte[] bytes = new byte[4];

#if NET
        if (!NativeMethods.IsWindows)
        {
            // Enforce a minimum timeout because the timeout passed to Connect() just before
            // calling this method does not apply on UNIX domain socket-based
            // implementations of PipeStream.
            // https://github.com/dotnet/corefx/issues/28791
            timeout = Math.Max(timeout, 50);

            // A legacy MSBuild.exe won't try to connect to MSBuild running
            // in a dotnet host process, so we can read the bytes simply.
            var readTask = stream.ReadAsync(bytes, 0, bytes.Length);

            // Manual timeout here because the timeout passed to Connect() just before
            // calling this method does not apply on UNIX domain socket-based
            // implementations of PipeStream.
            // https://github.com/dotnet/corefx/issues/28791
            if (!readTask.Wait(timeout))
            {
                result = HandshakeResult.Failure(HandshakeStatus.Timeout, $"Did not receive return handshake in {timeout}ms");
                return false;
            }

            readTask.GetAwaiter().GetResult();
        }
        else
#endif
        {
            int bytesRead = stream.Read(bytes, 0, bytes.Length);

            // Abort for connection attempts from ancient MSBuild.exes
            if (byteToAccept != null && bytesRead > 0 && byteToAccept != bytes[0])
            {
                stream.WriteIntForHandshake(0x0F0F0F0F);
                stream.WriteIntForHandshake(0x0F0F0F0F);
                result = HandshakeResult.Failure(HandshakeStatus.OldMSBuild, $"Client: rejected old host. Received byte {bytes[0]} instead of {byteToAccept}.");
                return false;
            }

            if (bytesRead != bytes.Length)
            {
                // We've unexpectly reached end of stream.
                // We are now in a bad state, disconnect on our end
                result = HandshakeResult.Failure(HandshakeStatus.UnexpectedEndOfStream, "Unexpected end of stream while reading for handshake");

                return false;
            }
        }

        try
        {
            // We want to read the long and send it from left to right (this means big endian)
            // If we are little endian the stream has already been reversed by the sender, we need to reverse it again to get the original number
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            result = HandshakeResult.Success(BitConverter.ToInt32(bytes, 0 /* start index */));
        }
        catch (ArgumentException ex)
        {
            result = HandshakeResult.Failure(HandshakeStatus.EndiannessMismatch, $"Failed to convert the handshake to big-endian. {ex.Message}");
            return false;
        }

        return true;
    }

    /// <summary>
    ///  Given the appropriate information, return the equivalent HandshakeOptions.
    /// </summary>
    internal static HandshakeOptions GetHandshakeOptions(
        bool taskHost,
        TaskHostParameters taskHostParameters,
        string? architectureFlagToSet = null,
        bool nodeReuse = false,
        bool lowPriority = false)
    {
        HandshakeOptions context = taskHost ? HandshakeOptions.TaskHost : HandshakeOptions.None;

        int clrVersion = 0;

        // We don't know about the TaskHost.
        if (taskHost)
        {
            // No parameters given, default to current
            if (taskHostParameters.IsEmpty)
            {
                clrVersion = typeof(bool).Assembly.GetName().Version!.Major;
                architectureFlagToSet = XMakeAttributes.GetCurrentMSBuildArchitecture();
            }
            else // Figure out flags based on parameters given
            {
                FrameworkErrorUtilities.VerifyThrow(taskHostParameters.Runtime != null, "Should always have an explicit runtime when we call this method.");
                FrameworkErrorUtilities.VerifyThrow(taskHostParameters.Architecture != null, "Should always have an explicit architecture when we call this method.");

                if (taskHostParameters.Runtime.Equals(XMakeAttributes.MSBuildRuntimeValues.clr2, StringComparison.OrdinalIgnoreCase))
                {
                    clrVersion = 2;
                }
                else if (taskHostParameters.Runtime.Equals(XMakeAttributes.MSBuildRuntimeValues.clr4, StringComparison.OrdinalIgnoreCase))
                {
                    clrVersion = 4;
                }
                else if (taskHostParameters.Runtime.Equals(XMakeAttributes.MSBuildRuntimeValues.net, StringComparison.OrdinalIgnoreCase))
                {
                    clrVersion = 5;
                }
                else
                {
                    FrameworkErrorUtilities.ThrowInternalErrorUnreachable();
                }

                architectureFlagToSet = taskHostParameters.Architecture;
            }
        }

        if (!string.IsNullOrEmpty(architectureFlagToSet))
        {
            if (architectureFlagToSet!.Equals(XMakeAttributes.MSBuildArchitectureValues.x64, StringComparison.OrdinalIgnoreCase))
            {
                context |= HandshakeOptions.X64;
            }
            else if (architectureFlagToSet.Equals(XMakeAttributes.MSBuildArchitectureValues.arm64, StringComparison.OrdinalIgnoreCase))
            {
                context |= HandshakeOptions.Arm64;
            }
        }

        switch (clrVersion)
        {
            case 0:
            // Not a taskhost, runtime must match
            case 4:
                // Default for MSBuild running on .NET Framework 4,
                // not represented in handshake
                break;
            case 2:
                context |= HandshakeOptions.CLR2;
                break;
            case >= 5:
                context |= HandshakeOptions.NET;
                break;
            default:
                FrameworkErrorUtilities.ThrowInternalErrorUnreachable();
                break;
        }

        // Node reuse is not supported in CLR2 because it's a legacy runtime.
        if (nodeReuse && clrVersion != 2)
        {
            context |= HandshakeOptions.NodeReuse;
        }

        if (lowPriority)
        {
            context |= HandshakeOptions.LowPriority;
        }

#if FEATURE_SECURITY_PRINCIPAL_WINDOWS || NET
        // If we are running in elevated privs, we will only accept a handshake from an elevated process as well.
        // Both the client and the host will calculate this separately, and the idea is that if they come out the same
        // then we can be sufficiently confident that the other side has the same elevation level as us.  This is complementary
        // to the username check which is also done on connection.
        if (
#if NET
            NativeMethods.IsWindows &&
#endif
            new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
        {
            context |= HandshakeOptions.Administrator;
        }
#endif
        return context;
    }

    /// <summary>
    ///  Gets a hash code for this string.  If strings A and B are such that A.Equals(B), then
    ///  they will return the same hash code.
    ///  This is as implemented in CLR String.GetHashCode() [ndp\clr\src\BCL\system\String.cs]
    ///  but stripped out architecture specific defines
    ///  that causes the hashcode to be different and this causes problem in cross-architecture handshaking.
    /// </summary>
    internal static int GetHashCode(string fileVersion)
    {
        unsafe
        {
            fixed (char* src = fileVersion)
            {
                int hash1 = (5381 << 16) + 5381;
                int hash2 = hash1;

                int* pint = (int*)src;
                int len = fileVersion.Length;
                while (len > 0)
                {
                    hash1 = ((hash1 << 5) + hash1 + (hash1 >> 27)) ^ pint[0];
                    if (len <= 2)
                    {
                        break;
                    }

                    hash2 = ((hash2 << 5) + hash2 + (hash2 >> 27)) ^ pint[1];
                    pint += 2;
                    len -= 4;
                }

                return hash1 + (hash2 * 1566083941);
            }
        }
    }

    internal static int AvoidEndOfHandshakeSignal(int x)
        => x == EndOfHandshakeSignal ? ~x : x;

    /// <summary>
    ///  Writes trace information to a log file.
    /// </summary>
    public static void Trace(string message)
    {
        if (s_trace)
        {
            TraceCore(nodeId: -1, message);
        }
    }

    /// <inheritdoc cref="Trace(string)" />
    public static void Trace(int nodeId, string message)
    {
        if (s_trace)
        {
            TraceCore(nodeId, message);
        }
    }

    /// <inheritdoc cref="Trace(string)" />
    public static void Trace(TraceInterpolatedStringHandler message)
    {
        if (s_trace)
        {
            TraceCore(nodeId: -1, message.GetFormattedText());
        }
    }

    /// <inheritdoc cref="Trace(string)" />
    public static void Trace(int nodeId, TraceInterpolatedStringHandler message)
    {
        if (s_trace)
        {
            TraceCore(nodeId, message.GetFormattedText());
        }
    }

    [InterpolatedStringHandler]
    public ref struct TraceInterpolatedStringHandler
    {
        private StringBuilderHelper _builder;

        public TraceInterpolatedStringHandler(int literalLength, int formattedCount, out bool isEnabled)
        {
            isEnabled = s_trace;
            _builder = isEnabled ? new(literalLength) : default;
        }

        public readonly void AppendLiteral(string value)
            => _builder.AppendLiteral(value);

        public readonly void AppendFormatted<TValue>(TValue value)
            => _builder.AppendFormatted(value);

        public readonly void AppendFormatted<TValue>(TValue value, string format)
            where TValue : IFormattable
            => _builder.AppendFormatted(value, format);

        public readonly string GetFormattedText()
            => _builder.GetFormattedText();
    }

    private static void TraceCore(int nodeId, string message)
    {
        lock (s_traceLock)
        {
            s_debugDumpPath ??= FrameworkDebugUtils.DebugPath;

            if (!string.IsNullOrEmpty(s_debugDumpPath))
            {
                Directory.CreateDirectory(s_debugDumpPath);
            }
            else
            {
                // Note: FileUtilities.TempFileDirectory ensures the directory exists,
                // so we don't need to create it in that case.
                s_debugDumpPath = FileUtilities.TempFileDirectory;
            }

            try
            {
                string fileName = nodeId == -1
                    ? $"MSBuild_CommTrace_PID_{EnvironmentUtilities.CurrentProcessId}.txt"
                    : $"MSBuild_CommTrace_PID_{EnvironmentUtilities.CurrentProcessId}_node_{nodeId}.txt";

                string filePath = Path.Combine(s_debugDumpPath, fileName);

                using (StreamWriter writer = FileUtilities.OpenWrite(filePath, append: true))
                {
                    long now = DateTime.UtcNow.Ticks;
                    float millisecondsSinceLastLog = (float)(now - s_lastLoggedTicks) / 10000L;
                    s_lastLoggedTicks = now;

                    writer.WriteLine($"{Thread.CurrentThread.Name} (TID {Environment.CurrentManagedThreadId}) {now,15} +{millisecondsSinceLastLog,10}ms: {message}");
                }
            }
            catch (IOException)
            {
                // Ignore
            }
        }
    }
}
