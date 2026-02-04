// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Internal
{
    /// <summary>
    /// Enumeration of all possible (currently supported) options for handshakes.
    /// </summary>
    [Flags]
    internal enum HandshakeOptions
    {
        None = 0,

        /// <summary>
        /// Process is a TaskHost.
        /// </summary>
        TaskHost = 1,

        /// <summary>
        /// Using the 2.0 CLR.
        /// </summary>
        CLR2 = 2,

        /// <summary>
        /// 64-bit Intel process.
        /// </summary>
        X64 = 4,

        /// <summary>
        /// Node reuse enabled.
        /// </summary>
        NodeReuse = 8,

        /// <summary>
        /// Building with BelowNormal priority.
        /// </summary>
        LowPriority = 16,

        /// <summary>
        /// Building with administrator privileges.
        /// </summary>
        Administrator = 32,

        /// <summary>
        /// Using the .NET Core/.NET 5.0+ runtime.
        /// </summary>
        NET = 64,

        /// <summary>
        /// ARM64 process.
        /// </summary>
        Arm64 = 128,

        /// <summary>
        /// Using a long-running sidecar TaskHost process to reduce startup overhead and reuse in-memory caches.
        /// </summary>
        SidecarTaskHost = 256,
    }

    /// <summary>
    /// Status codes for the handshake process.
    /// It aggregates return values across several functions so we use an aggregate instead of a separate class for each method.
    /// </summary>
    internal enum HandshakeStatus
    {
        /// <summary>
        /// The handshake operation completed successfully.
        /// </summary>
        Success = 0,

        /// <summary>
        /// The other node returned a different value than expected.
        /// This can happen either by attempting to connect to a wrong node type 
        /// (e.g., transient TaskHost trying to connect to a long-running TaskHost)
        /// or by trying to connect to a node that has a different MSBuild version.
        /// </summary>
        VersionMismatch = 1,

        /// <summary>
        /// The handshake was aborted due to connection from an old MSBuild version.
        /// Occurs in TryReadInt when detecting legacy MSBuild.exe connections.
        /// </summary>
        OldMSBuild = 2,

        /// <summary>
        /// The handshake operation timed out before completion.
        /// </summary>
        Timeout = 3,

        /// <summary>
        /// The stream ended unexpectedly during the handshake operation.
        /// Indicates an incomplete or corrupted handshake sequence.
        /// </summary>
        UnexpectedEndOfStream = 4,

        /// <summary>
        /// The endianness (byte order) of the communicating nodes does not match.
        /// Indicates an architecture compatibility issue.
        /// </summary>
        EndiannessMismatch = 5,

        /// <summary>
        /// The handshake status is undefined or uninitialized.
        /// </summary>
        Undefined,
    }

    /// <summary>
    /// An aggregate class for passing around results of a handshake and adjacent information.
    /// ErrorMessage is to propagate error messages where necessary
    /// </summary> 
    internal class HandshakeResult
    {
        /// <summary>
        /// Gets the status code indicating the result of the handshake operation.
        /// </summary>
        public HandshakeStatus Status { get; }

        /// <summary>
        /// Handshake in MSBuild is performed as passing integers back and forth.
        /// This field holds the value returned from a successful handshake step.
        /// </summary>
        public int Value { get; }

        /// <summary>
        /// Gets the error message when a handshake operation fails.
        /// </summary>
        public string ErrorMessage { get; }

        /// <summary>
        /// The negotiated packet version with the child node.
        /// It's needed to ensure both sides of the communication can read/write data in pipe.
        /// </summary>
        public byte NegotiatedPacketVersion { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="HandshakeResult"/> class.
        /// </summary>
        /// <param name="status">The status of the handshake operation.</param>
        /// <param name="value">The value returned from the handshake.</param>
        /// <param name="errorMessage">The error message if the handshake failed.</param>
        /// <param name="negotiatedPacketVersion">The packet version from the child node.</param>
        private HandshakeResult(HandshakeStatus status, int value, string errorMessage, byte negotiatedPacketVersion = 1)
        {
            Status = status;
            Value = value;
            ErrorMessage = errorMessage;
            NegotiatedPacketVersion = negotiatedPacketVersion;
        }

        /// <summary>
        /// Creates a successful handshake result with the specified value.
        /// </summary>
        /// <param name="value">The value returned from the handshake operation.</param>
        /// <param name="negotiatedPacketVersion">The packet version received from the child node.</param>
        /// <returns>A new <see cref="HandshakeResult"/> instance representing a successful operation.</returns>
        public static HandshakeResult Success(int value = 0, byte negotiatedPacketVersion = 1)
            => new(HandshakeStatus.Success, value, null, negotiatedPacketVersion);

        /// <summary>
        /// Creates a failed handshake result with the specified status and error message.
        /// </summary>
        /// <param name="status">The error status code for the failure.</param>
        /// <param name="errorMessage">A description of the error that occurred.</param>
        /// <returns>A new <see cref="HandshakeResult"/> instance representing a failed operation.</returns>
        public static HandshakeResult Failure(HandshakeStatus status, string errorMessage)
            => new(status, 0, errorMessage);
    }

    internal sealed class Handshake
    {
        /// <summary>
        /// Marker indicating that the next integer in the child handshake response is the PacketVersion.
        /// </summary>
        public const int PacketVersionFromChildMarker = -1;

        private readonly HandshakeComponents _handshakeComponents;

        // Helper method to validate handshake option presence
        internal static bool IsHandshakeOptionEnabled(HandshakeOptions hostContext, HandshakeOptions option)
            => (hostContext & option) == option;

        // Source options of the handshake.
        internal HandshakeOptions HandshakeOptions { get; }

        public Handshake(HandshakeOptions nodeType, bool includeSessionId = true)
        {
            HandshakeOptions = nodeType;

            // Build handshake options with version in upper bits
            const int handshakeVersion = CommunicationsUtilities.HandshakeVersion;
            var options = (int)nodeType | (handshakeVersion << 24);
            CommunicationsUtilities.Trace("Building handshake for node type {0}, (version {1}): options {2}.", nodeType, handshakeVersion, options);

            // Tools directory is the path of MSBuildTaskHost.exe
            string toolsDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            // Calculate salt from environment and tools directory
            string handshakeSalt = Environment.GetEnvironmentVariable("MSBUILDNODEHANDSHAKESALT") ?? "";
            int salt = CommunicationsUtilities.GetHashCode($"{handshakeSalt}{toolsDirectory}");

            CommunicationsUtilities.Trace("Handshake salt is {0}", handshakeSalt);
            CommunicationsUtilities.Trace("Tools directory root is {0}", toolsDirectory);

            // Get session ID if needed (expensive call)
            int sessionId = 0;
            if (includeSessionId)
            {
                using var currentProcess = Process.GetCurrentProcess();
                sessionId = currentProcess.SessionId;
            }

            _handshakeComponents = CreateStandardComponents(options, salt, sessionId);
        }

        private static HandshakeComponents CreateStandardComponents(int options, int salt, int sessionId)
        {
            var fileVersion = new Version(FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion);

            return new(
                options,
                salt,
                fileVersion.Major,
                fileVersion.Minor,
                fileVersion.Build,
                fileVersion.Revision,
                sessionId);
        }

        public HandshakeComponents RetrieveHandshakeComponents() => new(
            CommunicationsUtilities.AvoidEndOfHandshakeSignal(_handshakeComponents.Options),
            CommunicationsUtilities.AvoidEndOfHandshakeSignal(_handshakeComponents.Salt),
            CommunicationsUtilities.AvoidEndOfHandshakeSignal(_handshakeComponents.FileVersionMajor),
            CommunicationsUtilities.AvoidEndOfHandshakeSignal(_handshakeComponents.FileVersionMinor),
            CommunicationsUtilities.AvoidEndOfHandshakeSignal(_handshakeComponents.FileVersionBuild),
            CommunicationsUtilities.AvoidEndOfHandshakeSignal(_handshakeComponents.FileVersionPrivate),
            CommunicationsUtilities.AvoidEndOfHandshakeSignal(_handshakeComponents.SessionId));
    }

    /// <summary>
    /// This class contains utility methods for the MSBuild engine.
    /// </summary>
    internal static class CommunicationsUtilities
    {
        /// <summary>
        /// Indicates to the NodeEndpoint that all the various parts of the Handshake have been sent.
        /// </summary>
        private const int EndOfHandshakeSignal = -0x2a2a2a2a;

        /// <summary>
        /// The version of the handshake. This should be updated each time the handshake structure is altered.
        /// </summary>
        internal const byte HandshakeVersion = 0x01;

        /// <summary>
        /// The timeout to connect to a node.
        /// </summary>
        private const int DefaultNodeConnectionTimeout = 900 * 1000; // 15 minutes; enough time that a dev will typically do another build in this time

        /// <summary>
        /// Whether to trace communications
        /// </summary>
        private static readonly bool s_trace = Traits.Instance.DebugNodeCommunication;

        /// <summary>
        /// Lock trace to ensure we are logging in serial fashion.
        /// </summary>
        private static readonly LockType s_traceLock = new LockType();

        /// <summary>
        /// Place to dump trace
        /// </summary>
        private static string s_debugDumpPath;

        /// <summary>
        /// Ticks at last time logged
        /// </summary>
        private static long s_lastLoggedTicks = DateTime.UtcNow.Ticks;

        /// <summary>
        /// Gets or sets the node connection timeout.
        /// </summary>
        internal static int NodeConnectionTimeout
        {
            get { return GetIntegerVariableOrDefault("MSBUILDNODECONNECTIONTIMEOUT", DefaultNodeConnectionTimeout); }
        }

        /// <summary>
        /// Get environment block.
        /// </summary>
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern unsafe char* GetEnvironmentStrings();

        /// <summary>
        /// Free environment block.
        /// </summary>
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern unsafe bool FreeEnvironmentStrings(char* pStrings);

        /// <summary>
        /// Set environment variable P/Invoke.
        /// </summary>
        [DllImport("kernel32.dll", EntryPoint = "SetEnvironmentVariable", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetEnvironmentVariableNative(string name, string value);

        /// <summary>
        /// Sets an environment variable using P/Invoke to workaround the .NET Framework BCL implementation.
        /// </summary>
        /// <remarks>
        /// .NET Framework implementation of SetEnvironmentVariable checks the length of the value and throws an exception if
        /// it's greater than or equal to 32,767 characters. This limitation does not exist on modern Windows or .NET.
        /// </remarks>
        internal static void SetEnvironmentVariable(string name, string value)
        {
            if (!SetEnvironmentVariableNative(name, value))
            {
                throw Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error());
            }
        }

        /// <summary>
        /// Returns key value pairs of environment variables in a new dictionary
        /// with a case-insensitive key comparer.
        /// </summary>
        /// <remarks>
        /// Copied from the BCL implementation to eliminate some expensive security asserts on .NET Framework.
        /// </remarks>
        internal static Dictionary<string, string> GetEnvironmentVariables()
        {
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

                    Dictionary<string, string> table = new(200, StringComparer.OrdinalIgnoreCase); // Razzle has 150 environment variables

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
                        while (*(pEnvironmentBlock + i) != '=' && *(pEnvironmentBlock + i) != '\0')
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

                        string key = new string(pEnvironmentBlock, startKey, i - startKey);

                        i++;

                        // skip over '='
                        int startValue = i;

                        while (*(pEnvironmentBlock + i) != 0)
                        {
                            // Read to end of this entry
                            i++;
                        }

                        string value = new string(pEnvironmentBlock, startValue, i - startValue);

                        // skip over 0 handled by for loop's i++
                        table[key] = value;
                    }

                    return table;
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

        /// <summary>
        /// Updates the environment to match the provided dictionary.
        /// </summary>
        internal static void SetEnvironment(IDictionary<string, string> newEnvironment)
        {
            if (newEnvironment != null)
            {
                // First, delete all no longer set variables
                IDictionary<string, string> currentEnvironment = GetEnvironmentVariables();
                foreach (KeyValuePair<string, string> entry in currentEnvironment)
                {
                    if (!newEnvironment.ContainsKey(entry.Key))
                    {
                        SetEnvironmentVariable(entry.Key, null);
                    }
                }

                // Then, make sure the new ones have their new values.
                foreach (KeyValuePair<string, string> entry in newEnvironment)
                {
                    if (!currentEnvironment.TryGetValue(entry.Key, out string currentValue) || currentValue != entry.Value)
                    {
                        SetEnvironmentVariable(entry.Key, entry.Value);
                    }
                }
            }
        }

#nullable enable
        /// <summary>
        /// Indicate to the client that all elements of the Handshake have been sent.
        /// </summary>
        internal static void WriteEndOfHandshakeSignal(this PipeStream stream)
        {
            stream.WriteIntForHandshake(EndOfHandshakeSignal);
        }

        /// <summary>
        /// Extension method to write a series of bytes to a stream
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

            ErrorUtilities.VerifyThrow(bytes.Length == 4, "Int should be 4 bytes");

            stream.Write(bytes, 0, bytes.Length);
        }

        internal static bool TryReadEndOfHandshakeSignal(
            this PipeStream stream,
            bool isProvider,
            out HandshakeResult result)
        {
            // Accept only the first byte of the EndOfHandshakeSignal
            if (stream.TryReadIntForHandshake(
                byteToAccept: null,
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
                    if (!stream.TryReadIntForHandshake(
                        byteToAccept: null,
                        out HandshakeResult versionResult))
                    {
                        result = versionResult;
                        return false;
                    }

                    byte childVersion = (byte)versionResult.Value;
                    negotiatedPacketVersion = NodePacketTypeExtensions.GetNegotiatedPacketVersion(childVersion);
                    Trace("Node PacketVersion: {0}, Local: {1}, Negotiated: {2}", childVersion, NodePacketTypeExtensions.PacketVersion, negotiatedPacketVersion);

                    if (!stream.TryReadIntForHandshake(
                            byteToAccept: null,
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
            out HandshakeResult result)
        {
            byte[] bytes = new byte[4];
            int bytesRead = stream.Read(bytes, 0, bytes.Length);

            // Abort for connection attempts from ancient MSBuild.exes
            if (byteToAccept != null && bytesRead > 0 && byteToAccept != bytes[0])
            {
                stream.WriteIntForHandshake(0x0F0F0F0F);
                stream.WriteIntForHandshake(0x0F0F0F0F);
                result = HandshakeResult.Failure(HandshakeStatus.OldMSBuild, String.Format(CultureInfo.InvariantCulture, "Client: rejected old host. Received byte {0} instead of {1}.", bytes[0], byteToAccept));
                return false;
            }

            if (bytesRead != bytes.Length)
            {
                // We've unexpectly reached end of stream.
                // We are now in a bad state, disconnect on our end
                result = HandshakeResult.Failure(HandshakeStatus.UnexpectedEndOfStream, String.Format(CultureInfo.InvariantCulture, "Unexpected end of stream while reading for handshake"));

                return false;
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
                result = HandshakeResult.Failure(HandshakeStatus.EndiannessMismatch, String.Format(CultureInfo.InvariantCulture, "Failed to convert the handshake to big-endian. {0}", ex.Message));
                return false;
            }

            return true;
        }
#nullable disable

        /// <summary>
        /// Given the appropriate information, return the equivalent HandshakeOptions.
        /// </summary>
        internal static HandshakeOptions GetHandshakeOptions()
        {
            // For MSBuildTaskHost, the HandshakeOptions are easy to compute.
            HandshakeOptions options = HandshakeOptions.TaskHost;

            options |= HandshakeOptions.CLR2;

            if (NativeMethodsShared.Is64Bit)
            {
                options |= HandshakeOptions.X64;
            }

            // If we are running in elevated privs, we will only accept a handshake from an elevated process as well.
            // Both the client and the host will calculate this separately, and the idea is that if they come out the same
            // then we can be sufficiently confident that the other side has the same elevation level as us.  This is complementary
            // to the username check which is also done on connection.
            if (new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
            {
                options |= HandshakeOptions.Administrator;
            }

            return options;
        }

        /// <summary>
        /// Gets the value of an integer environment variable, or returns the default if none is set or it cannot be converted.
        /// </summary>
        internal static int GetIntegerVariableOrDefault(string environmentVariable, int defaultValue)
        {
            string environmentValue = Environment.GetEnvironmentVariable(environmentVariable);
            if (String.IsNullOrEmpty(environmentValue))
            {
                return defaultValue;
            }

            int localDefaultValue;
            if (Int32.TryParse(environmentValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out localDefaultValue))
            {
                defaultValue = localDefaultValue;
            }

            return defaultValue;
        }

        /// <summary>
        /// Writes trace information to a log file
        /// </summary>
        internal static void Trace<T>(string format, T arg0)
        {
            Trace(nodeId: -1, format, arg0);
        }

        /// <summary>
        /// Writes trace information to a log file
        /// </summary>
        internal static void Trace<T>(int nodeId, string format, T arg0)
        {
            if (s_trace)
            {
                TraceCore(nodeId, string.Format(format, arg0));
            }
        }

        /// <summary>
        /// Writes trace information to a log file
        /// </summary>
        internal static void Trace<T0, T1>(string format, T0 arg0, T1 arg1)
        {
            Trace(nodeId: -1, format, arg0, arg1);
        }

        /// <summary>
        /// Writes trace information to a log file
        /// </summary>
        internal static void Trace<T0, T1>(int nodeId, string format, T0 arg0, T1 arg1)
        {
            if (s_trace)
            {
                TraceCore(nodeId, string.Format(format, arg0, arg1));
            }
        }

        /// <summary>
        /// Writes trace information to a log file
        /// </summary>
        internal static void Trace<T0, T1, T2>(string format, T0 arg0, T1 arg1, T2 arg2)
        {
            Trace(nodeId: -1, format, arg0, arg1, arg2);
        }

        /// <summary>
        /// Writes trace information to a log file
        /// </summary>
        internal static void Trace<T0, T1, T2>(int nodeId, string format, T0 arg0, T1 arg1, T2 arg2)
        {
            if (s_trace)
            {
                TraceCore(nodeId, string.Format(format, arg0, arg1, arg2));
            }
        }

        /// <summary>
        /// Writes trace information to a log file
        /// </summary>
        internal static void Trace(string format, params object[] args)
        {
            Trace(nodeId: -1, format, args);
        }

        /// <summary>
        /// Writes trace information to a log file
        /// </summary>
        internal static void Trace(int nodeId, string format, params object[] args)
        {
            if (s_trace)
            {
                string message = string.Format(CultureInfo.CurrentCulture, format, args);
                TraceCore(nodeId, message);
            }
        }

        internal static void Trace(int nodeId, string message)
        {
            if (s_trace)
            {
                TraceCore(nodeId, message);
            }
        }

        /// <summary>
        /// Writes trace information to a log file
        /// </summary>
        private static void TraceCore(int nodeId, string message)
        {
            lock (s_traceLock)
            {
                s_debugDumpPath ??= Environment.GetEnvironmentVariable("MSBUILDDEBUGPATH");

                if (string.IsNullOrEmpty(s_debugDumpPath))
                {
                    s_debugDumpPath = FileUtilities.TempFileDirectory;
                }
                else
                {
                    Directory.CreateDirectory(s_debugDumpPath);
                }

                try
                {
                    string fileName = nodeId != -1
                        ? $"MSBuild_CommTrace_PID_{EnvironmentUtilities.CurrentProcessId}_node_{nodeId}.txt"
                        : $"MSBuild_CommTrace_PID_{EnvironmentUtilities.CurrentProcessId}.txt";

                    string filePath = Path.Combine(s_debugDumpPath, fileName);

                    using (StreamWriter file = FileUtilities.OpenWrite(filePath, append: true))
                    {
                        long now = DateTime.UtcNow.Ticks;
                        float millisecondsSinceLastLog = (float)(now - s_lastLoggedTicks) / 10000L;
                        s_lastLoggedTicks = now;
                        file.WriteLine("{0} (TID {1}) {2,15} +{3,10}ms: {4}", Thread.CurrentThread.Name, Thread.CurrentThread.ManagedThreadId, now, millisecondsSinceLastLog, message);
                    }
                }
                catch (IOException)
                {
                    // Ignore
                }
            }
        }

        /// <summary>
        /// Gets a hash code for this string.  If strings A and B are such that A.Equals(B), then
        /// they will return the same hash code.
        /// This is as implemented in CLR String.GetHashCode() [ndp\clr\src\BCL\system\String.cs]
        /// but stripped out architecture specific defines
        /// that causes the hashcode to be different and this causes problem in cross-architecture handshaking.
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

        internal static int AvoidEndOfHandshakeSignal(int x) => x == EndOfHandshakeSignal ? ~x : x;
    }

    /// <summary>
    /// Represents the components of a handshake in a structured format with named fields.
    /// </summary>
    internal readonly struct HandshakeComponents
    {
        private readonly int options;
        private readonly int salt;
        private readonly int fileVersionMajor;
        private readonly int fileVersionMinor;
        private readonly int fileVersionBuild;
        private readonly int fileVersionPrivate;
        private readonly int sessionId;

        public HandshakeComponents(int options, int salt, int fileVersionMajor, int fileVersionMinor, int fileVersionBuild, int fileVersionPrivate, int sessionId)
        {
            this.options = options;
            this.salt = salt;
            this.fileVersionMajor = fileVersionMajor;
            this.fileVersionMinor = fileVersionMinor;
            this.fileVersionBuild = fileVersionBuild;
            this.fileVersionPrivate = fileVersionPrivate;
            this.sessionId = sessionId;
        }

        public HandshakeComponents(int options, int salt, int fileVersionMajor, int fileVersionMinor, int fileVersionBuild, int fileVersionPrivate)
            : this(options, salt, fileVersionMajor, fileVersionMinor, fileVersionBuild, fileVersionPrivate, 0)
        {
        }

        public int Options => options;

        public int Salt => salt;

        public int FileVersionMajor => fileVersionMajor;

        public int FileVersionMinor => fileVersionMinor;

        public int FileVersionBuild => fileVersionBuild;

        public int FileVersionPrivate => fileVersionPrivate;

        public int SessionId => sessionId;

        public IEnumerable<KeyValuePair<string, int>> EnumerateComponents()
        {
            yield return new KeyValuePair<string, int>(nameof(Options), Options);
            yield return new KeyValuePair<string, int>(nameof(Salt), Salt);
            yield return new KeyValuePair<string, int>(nameof(FileVersionMajor), FileVersionMajor);
            yield return new KeyValuePair<string, int>(nameof(FileVersionMinor), FileVersionMinor);
            yield return new KeyValuePair<string, int>(nameof(FileVersionBuild), FileVersionBuild);
            yield return new KeyValuePair<string, int>(nameof(FileVersionPrivate), FileVersionPrivate);
            yield return new KeyValuePair<string, int>(nameof(SessionId), SessionId);
        }

        public override string ToString() => $"{options} {salt} {fileVersionMajor} {fileVersionMinor} {fileVersionBuild} {fileVersionPrivate} {sessionId}";
    }
}
