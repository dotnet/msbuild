// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.IO.Pipes;
using System.Security.Principal;

using Microsoft.Build.Internal;

namespace Microsoft.Build.Shared
{
    internal static class NamedPipeUtil
    {
        internal static string GetPipeNameOrPath(string pipeName)
        {
            if (NativeMethodsShared.IsUnixLike)
            {
                // If we're on a Unix machine then named pipes are implemented using Unix Domain Sockets.
                // Most Unix systems have a maximum path length limit for Unix Domain Sockets, with
                // Mac having a particularly short one. Mac also has a generated temp directory that
                // can be quite long, leaving very little room for the actual pipe name. Fortunately,
                // '/tmp' is mandated by POSIX to always be a valid temp directory, so we can use that
                // instead.
                return Path.Combine("/tmp", pipeName);
            }
            else
            {
                return pipeName;
            }
        }
#if !FEATURE_PIPEOPTIONS_CURRENTUSERONLY
        //  This code needs to be in a separate method so that we don't try (and fail) to load the Windows-only APIs when JIT-ing the code
        //  on non-Windows operating systems
        private static void ValidateRemotePipeSecurityOnWindows(NamedPipeClientStream nodeStream)
        {
            SecurityIdentifier identifier = WindowsIdentity.GetCurrent().Owner;
#if FEATURE_PIPE_SECURITY
            PipeSecurity remoteSecurity = nodeStream.GetAccessControl();
#else
            var remoteSecurity = new PipeSecurity(nodeStream.SafePipeHandle, System.Security.AccessControl.AccessControlSections.Access |
                System.Security.AccessControl.AccessControlSections.Owner | System.Security.AccessControl.AccessControlSections.Group);
#endif
            IdentityReference remoteOwner = remoteSecurity.GetOwner(typeof(SecurityIdentifier));
            if (remoteOwner != identifier)
            {
                CommunicationsUtilities.Trace("The remote pipe owner {0} does not match {1}", remoteOwner.Value, identifier.Value);
                throw new UnauthorizedAccessException();
            }
        }
#endif

        /// <summary>
        /// Attempts to connect to the specified process.
        /// </summary>
        internal static NamedPipeClientStream TryConnectToProcess(int nodeProcessId, int timeout, Handshake? handshake)
        {
            string pipeName = GetPipeNameOrPath("MSBuild" + nodeProcessId);
            return TryConnectToProcess(pipeName, timeout, timeout, handshake);
        }

        /// <summary>
        /// Attempts to connect to the specified process.
        /// </summary>
        internal static NamedPipeClientStream TryConnectToProcess(string pipeName, int timeout, Handshake? handshake)
        {
            return TryConnectToProcess(pipeName, null, timeout, handshake);
        }

        private static NamedPipeClientStream TryConnectToProcess(string pipeName, int? nodeProcessId, int timeout, Handshake? handshake)
        {
            NamedPipeClientStream nodeStream = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous
#if FEATURE_PIPEOPTIONS_CURRENTUSERONLY
                                                                         | PipeOptions.CurrentUserOnly
#endif
                                                                         );
            if (nodeProcessId.HasValue)
            {
                CommunicationsUtilities.Trace("Attempting connect to PID {0} with pipe {1} with timeout {2} ms", nodeProcessId.Value, pipeName, timeout);
            }
            else
            {
                CommunicationsUtilities.Trace("Attempting connect to process {0} with pipe {1} with timeout {2} ms", pipeName, pipeName, timeout);
            }

            try
            {
                nodeStream.Connect(timeout);

#if !FEATURE_PIPEOPTIONS_CURRENTUSERONLY
                if (NativeMethodsShared.IsWindows && !NativeMethodsShared.IsMono)
                {
                    // Verify that the owner of the pipe is us.  This prevents a security hole where a remote node has
                    // been faked up with ACLs that would let us attach to it.  It could then issue fake build requests back to
                    // us, potentially causing us to execute builds that do harmful or unexpected things.  The pipe owner can
                    // only be set to the user's own SID by a normal, unprivileged process.  The conditions where a faked up
                    // remote node could set the owner to something else would also let it change owners on other objects, so
                    // this would be a security flaw upstream of us.
                    ValidateRemotePipeSecurityOnWindows(nodeStream);
                }
#endif
                if (handshake.HasValue)
                {

                    int[] handshakeComponents = handshake.Value.RetrieveHandshakeComponents();
                    for (int i = 0; i < handshakeComponents.Length; i++)
                    {
                        CommunicationsUtilities.Trace("Writing handshake part {0} to pipe {1}", i, pipeName);
                        nodeStream.WriteIntForHandshake(handshakeComponents[i]);
                    }

                    // This indicates that we have finished all the parts of our handshake; hopefully the endpoint has as well.
                    nodeStream.WriteEndOfHandshakeSignal();

                    CommunicationsUtilities.Trace("Reading handshake from pipe {0}", pipeName);

#if NETCOREAPP2_1 || MONO
                    nodeStream.ReadEndOfHandshakeSignal(true, timeout);
#else
                nodeStream.ReadEndOfHandshakeSignal(true);
#endif
                }

                // We got a connection.
                CommunicationsUtilities.Trace("Successfully connected to pipe {0}...!", pipeName);
                return nodeStream;
            }
            catch (Exception e) when (!ExceptionHandling.IsCriticalException(e))
            {
                // Can be:
                // UnauthorizedAccessException -- Couldn't connect, might not be a node.
                // IOException -- Couldn't connect, already in use.
                // TimeoutException -- Couldn't connect, might not be a node.
                // InvalidOperationException – Couldn’t connect, probably a different build
                CommunicationsUtilities.Trace("Failed to connect to pipe {0}. {1}", pipeName, e.Message.TrimEnd());

                // If we don't close any stream, we might hang up the child
                nodeStream?.Dispose();
            }

            return null;
        }
    }
}
