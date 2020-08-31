// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using Microsoft.Build.Internal;

namespace Microsoft.Build.Shared
{
    internal static class NamedPipeUtil
    {
        /// <summary>
        /// The size of the buffers to use for named pipes
        /// </summary>
        private const int PipeBufferSize = 131072;

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
        internal static NamedPipeClientStream TryConnectToProcess(int nodeProcessId, int timeout, Handshake handshake)
        {
            string pipeName = GetPipeNameOrPath("MSBuild" + nodeProcessId);
            return TryConnectToProcess(pipeName, nodeProcessId, timeout, handshake);
        }

        /// <summary>
        /// Attempts to connect to the specified process.
        /// </summary>
        internal static NamedPipeClientStream TryConnectToProcess(string pipeName, int timeout, Handshake handshake)
        {
            return TryConnectToProcess(pipeName, null, timeout, handshake);
        }

        private static NamedPipeClientStream TryConnectToProcess(string pipeName, int? nodeProcessId, int timeout, Handshake handshake)
        {
            NamedPipeClientStream nodeStream = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous
#if FEATURE_PIPEOPTIONS_CURRENTUSERONLY
                                                                         | PipeOptions.CurrentUserOnly
#endif
                                                                         );
            CommunicationsUtilities.Trace("Attempting connect to PID {0} with pipe {1} with timeout {2} ms", nodeProcessId.HasValue ? nodeProcessId.Value.ToString() : pipeName, pipeName, timeout);

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

                int[] handshakeComponents = handshake.RetrieveHandshakeComponents();
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

        internal static bool ValidateHandshake(Handshake handshake, NamedPipeServerStream serverStream, int clientConnectTimeout)
        {
            // The handshake protocol is a series of int exchanges.  The host sends us a each component, and we
            // verify it. Afterwards, the host sends an "End of Handshake" signal, to which we respond in kind.
            // Once the handshake is complete, both sides can be assured the other is ready to accept data.

            bool gotValidConnection = true;
            try
            {
                int[] handshakeComponents = handshake.RetrieveHandshakeComponents();
                for (int i = 0; i < handshakeComponents.Length; i++)
                {
                    int handshakePart = serverStream.ReadIntForHandshake(i == 0 ? (byte?)CommunicationsUtilities.handshakeVersion : null /* this will disconnect a < 16.8 host; it expects leading 00 or F5 or 06. 0x00 is a wildcard */
#if NETCOREAPP2_1 || MONO
                            , clientConnectTimeout /* wait a long time for the handshake from this side */
#endif
                            );

                    if (handshakePart != handshakeComponents[i])
                    {
                        CommunicationsUtilities.Trace("Handshake failed. Received {0} from host not {1}. Probably the host is a different MSBuild build.", handshakePart, handshakeComponents[i]);
                        serverStream.WriteIntForHandshake(i + 1);
                        gotValidConnection = false;
                        break;
                    }
                }

                if (gotValidConnection)
                {
                    // To ensure that our handshake and theirs have the same number of bytes, receive and send a magic number indicating EOS.
#if NETCOREAPP2_1 || MONO
                    serverStream.ReadEndOfHandshakeSignal(false, clientConnectTimeout); /* wait a long time for the handshake from this side */
#else
                    serverStream.ReadEndOfHandshakeSignal(false);
#endif
                    CommunicationsUtilities.Trace("Successfully connected to parent.");
                    serverStream.WriteEndOfHandshakeSignal();

#if FEATURE_SECURITY_PERMISSIONS
                    // We will only talk to a host that was started by the same user as us.  Even though the pipe access is set to only allow this user, we want to ensure they
                    // haven't attempted to change those permissions out from under us.  This ensures that the only way they can truly gain access is to be impersonating the
                    // user we were started by.
                    WindowsIdentity currentIdentity = WindowsIdentity.GetCurrent();
                    WindowsIdentity clientIdentity = null;
                    serverStream.RunAsClient(delegate () { clientIdentity = WindowsIdentity.GetCurrent(true); });

                    if (clientIdentity == null || !string.Equals(clientIdentity.Name, currentIdentity.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        CommunicationsUtilities.Trace("Handshake failed. Host user is {0} but we were created by {1}.", (clientIdentity == null) ? "<unknown>" : clientIdentity.Name, currentIdentity.Name);
                        return false;
                    }
#endif
                }
            }
            catch (IOException e)
            {
                // We will get here when:
                // 1. The host (OOP main node) connects to us, it immediately checks for user privileges
                //    and if they don't match it disconnects immediately leaving us still trying to read the blank handshake
                // 2. The host is too old sending us bits we automatically reject in the handshake
                // 3. We expected to read the EndOfHandshake signal, but we received something else
                CommunicationsUtilities.Trace("Client connection failed but we will wait for another connection. Exception: {0}", e.Message);

                gotValidConnection = false;
            }
            catch (InvalidOperationException)
            {
                gotValidConnection = false;
            }

            if (!gotValidConnection)
            {
                if (serverStream.IsConnected)
                {
                    serverStream.Disconnect();
                }
                return false;
            }

            return true;
        }

        internal static NamedPipeServerStream CreateNamedPipeServer(string pipeName, int? inputBufferSize = null, int? outputBufferSize = null, int maxNumberOfServerInstances = 1, bool allowNewInstances = false)
        {
            inputBufferSize ??= PipeBufferSize;
            outputBufferSize ??= PipeBufferSize;

#if FEATURE_PIPE_SECURITY && FEATURE_NAMED_PIPE_SECURITY_CONSTRUCTOR
            if (!NativeMethodsShared.IsMono)
            {
                SecurityIdentifier identifier = WindowsIdentity.GetCurrent().Owner;
                PipeSecurity security = new PipeSecurity();

                // Restrict access to just this account.  We set the owner specifically here, and on the
                // pipe client side they will check the owner against this one - they must have identical
                // SIDs or the client will reject this server.  This is used to avoid attacks where a
                // hacked server creates a less restricted pipe in an attempt to lure us into using it and 
                // then sending build requests to the real pipe client (which is the MSBuild Build Manager.)

                PipeAccessRights rights = PipeAccessRights.ReadWrite;
                if (allowNewInstances)
                {
                    rights |= PipeAccessRights.CreateNewInstance;
                }

                PipeAccessRule rule = new PipeAccessRule(identifier, rights, AccessControlType.Allow);
                security.AddAccessRule(rule);
                security.SetOwner(identifier);

                return new NamedPipeServerStream
                    (
                    pipeName,
                    PipeDirection.InOut,
                    maxNumberOfServerInstances, // Only allow one connection at a time.
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.WriteThrough,
                    inputBufferSize.Value, // Default input buffer
                    outputBufferSize.Value,  // Default output buffer
                    security,
                    HandleInheritability.None
                );
            }
#endif
            return new NamedPipeServerStream
            (
                pipeName,
                PipeDirection.InOut,
                maxNumberOfServerInstances, // Only allow one connection at a time.
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.WriteThrough,
                inputBufferSize.Value, // Default input buffer
                outputBufferSize.Value  // Default output buffer
            );
        }
    }
}
