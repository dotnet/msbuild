// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.IO.Pipes;

#if FEATURE_SECURITY_PRINCIPAL_WINDOWS || RUNTIME_TYPE_NETCORE
using System.Security.Principal;
#endif

using System.Reflection;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Internal
{
    /// <summary>
    /// Represents a unique key for identifying task host nodes.
    /// Combines HandshakeOptions (which specify runtime/architecture configuration) with
    /// the scheduled node ID to uniquely identify task hosts in multi-threaded mode.
    /// </summary>
    /// <param name="HandshakeOptions">The handshake options specifying runtime and architecture configuration.</param>
    /// <param name="NodeId">
    /// The scheduled node ID. In traditional multi-proc builds, this is -1 (meaning the task host
    /// is identified by HandshakeOptions alone). In multi-threaded mode, each in-proc node has
    /// its own task host, so the node ID is used to distinguish them.
    /// </param>
    internal readonly record struct TaskHostNodeKey(HandshakeOptions HandshakeOptions, int NodeId);

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
        internal const byte handshakeVersion = 0x01;

        /// <summary>
        /// The timeout to connect to a node.
        /// </summary>
        private const int DefaultNodeConnectionTimeout = 900 * 1000; // 15 minutes; enough time that a dev will typically do another build in this time

        /// <summary>
        /// On Windows, environment variables should be case-insensitive;
        /// on Unix-like systems, they should be case-sensitive, but this might be a breaking change in an edge case.
        /// https://github.com/dotnet/msbuild/issues/12858
        /// </summary>
        internal static StringComparer EnvironmentVariableComparer => FrameworkCommunicationsUtilities.EnvironmentVariableComparer;

        /// <summary>
        /// Gets the node connection timeout.
        /// </summary>e
        internal static int NodeConnectionTimeout
            => EnvironmentUtilities.GetValueAsInt32OrDefault("MSBUILDNODECONNECTIONTIMEOUT", DefaultNodeConnectionTimeout);

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
#if NETCOREAPP2_1_OR_GREATER
            int timeout,
#endif
            out HandshakeResult result)
        {
            // Accept only the first byte of the EndOfHandshakeSignal
            if (stream.TryReadIntForHandshake(
                byteToAccept: null,
#if NETCOREAPP2_1_OR_GREATER
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
                    if (!stream.TryReadIntForHandshake(
                            byteToAccept: null,
#if NETCOREAPP2_1_OR_GREATER
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

                    if (!stream.TryReadIntForHandshake(
                            byteToAccept: null,
#if NETCOREAPP2_1_OR_GREATER
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

#pragma warning disable SA1111, SA1009 // Closing parenthesis should be on line of last parameter
        /// <summary>
        /// Extension method to read a series of bytes from a stream.
        /// If specified, leading byte matches one in the supplied array if any, returns rejection byte and throws IOException.
        /// </summary>
        internal static bool TryReadIntForHandshake(
            this PipeStream stream,
            byte? byteToAccept,
#if NETCOREAPP2_1_OR_GREATER
            int timeout,
#endif
            out HandshakeResult result
            )
#pragma warning restore SA1111, SA1009 // Closing parenthesis should be on line of last parameter
        {
            byte[] bytes = new byte[4];

#if NETCOREAPP2_1_OR_GREATER
            if (!NativeMethodsShared.IsWindows)
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
                    result = HandshakeResult.Failure(HandshakeStatus.Timeout, String.Format(CultureInfo.InvariantCulture, "Did not receive return handshake in {0}ms", timeout));
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
        internal static HandshakeOptions GetHandshakeOptions(
            bool taskHost,
            TaskHostParameters taskHostParameters,
            string architectureFlagToSet = null,
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
                    clrVersion = typeof(bool).GetTypeInfo().Assembly.GetName().Version.Major;
                    architectureFlagToSet = XMakeAttributes.GetCurrentMSBuildArchitecture();
                }
                else // Figure out flags based on parameters given
                {
                    ErrorUtilities.VerifyThrow(taskHostParameters.Runtime != null, "Should always have an explicit runtime when we call this method.");
                    ErrorUtilities.VerifyThrow(taskHostParameters.Architecture != null, "Should always have an explicit architecture when we call this method.");

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
                        ErrorUtilities.ThrowInternalErrorUnreachable();
                    }

                    architectureFlagToSet = taskHostParameters.Architecture;
                }
            }

            if (!string.IsNullOrEmpty(architectureFlagToSet))
            {
                if (architectureFlagToSet.Equals(XMakeAttributes.MSBuildArchitectureValues.x64, StringComparison.OrdinalIgnoreCase))
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
                    ErrorUtilities.ThrowInternalErrorUnreachable();
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

#if FEATURE_SECURITY_PRINCIPAL_WINDOWS || RUNTIME_TYPE_NETCORE
            // If we are running in elevated privs, we will only accept a handshake from an elevated process as well.
            // Both the client and the host will calculate this separately, and the idea is that if they come out the same
            // then we can be sufficiently confident that the other side has the same elevation level as us.  This is complementary
            // to the username check which is also done on connection.
            if (
#if RUNTIME_TYPE_NETCORE
                NativeMethodsShared.IsWindows &&
#endif
                new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
            {
                context |= HandshakeOptions.Administrator;
            }
#endif
            return context;
        }

        /// <summary>
        ///  Writes trace information to a log file.
        /// </summary>
        public static void Trace(string message)
            => FrameworkCommunicationsUtilities.Trace(message);

        /// <inheritdoc cref="Trace(string)" />
        public static void Trace(int nodeId, string message)
            => FrameworkCommunicationsUtilities.Trace(nodeId, message);

        /// <inheritdoc cref="Trace(string)" />
        public static void Trace(FrameworkCommunicationsUtilities.TraceInterpolatedStringHandler message)
            => FrameworkCommunicationsUtilities.Trace(message);

        /// <inheritdoc cref="Trace(string)" />
        public static void Trace(int nodeId, FrameworkCommunicationsUtilities.TraceInterpolatedStringHandler message)
            => FrameworkCommunicationsUtilities.Trace(nodeId, message);

        /// <inheritdoc cref="FrameworkCommunicationsUtilities.GetHashCode(string)"/>
        internal static int GetHashCode(string fileVersion)
            => FrameworkCommunicationsUtilities.GetHashCode(fileVersion);

        internal static int AvoidEndOfHandshakeSignal(int x)
            => FrameworkCommunicationsUtilities.AvoidEndOfHandshakeSignal(x);
    }
}
