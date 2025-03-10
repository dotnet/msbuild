// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using Microsoft.Build.BackEnd;

namespace Microsoft.Build.Internal
{
    internal sealed class NodePipeClient : NodePipeBase
    {
        /// <summary>
        /// If true, sets a timeout for the handshake. This is only used on Unix-like socket implementations, because the
        /// timeout on the PipeStream connection is ignore.
        /// </summary>
        private static readonly bool s_useHandhakeTimeout = !NativeMethodsShared.IsWindows;

        private readonly NamedPipeClientStream _pipeClient;

        internal NodePipeClient(string pipeName, Handshake handshake)
            : base(pipeName, handshake) =>
#pragma warning disable SA1111, SA1009 // Closing parenthesis should be on line of last parameter
            _pipeClient = new(
                serverName: ".",
                pipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous
#if FEATURE_PIPEOPTIONS_CURRENTUSERONLY
                | PipeOptions.CurrentUserOnly
#endif
            );
#pragma warning restore SA1111, SA1009 // Closing parenthesis should be on line of last parameter

        protected override PipeStream NodeStream => _pipeClient;

        internal void ConnectToServer(int timeout)
        {
            CommunicationsUtilities.Trace("Attempting connect to pipe {0} with timeout {1} ms", PipeName, timeout);
            _pipeClient.Connect(timeout);
#if !FEATURE_PIPEOPTIONS_CURRENTUSERONLY
            // Verify that the owner of the pipe is us.  This prevents a security hole where a remote node has
            // been faked up with ACLs that would let us attach to it.  It could then issue fake build requests back to
            // us, potentially causing us to execute builds that do harmful or unexpected things.  The pipe owner can
            // only be set to the user's own SID by a normal, unprivileged process.  The conditions where a faked up
            // remote node could set the owner to something else would also let it change owners on other objects, so
            // this would be a security flaw upstream of us.
            ValidateRemotePipeOwner();
#endif
            PerformHandshake(s_useHandhakeTimeout ? timeout : 0);
            CommunicationsUtilities.Trace("Successfully connected to pipe {0}...!", PipeName);
        }

#if !FEATURE_PIPEOPTIONS_CURRENTUSERONLY
        // This code needs to be in a separate method so that we don't try (and fail) to load the Windows-only APIs when JIT-ing the code
        //  on non-Windows operating systems
        private void ValidateRemotePipeOwner()
        {
            SecurityIdentifier identifier = WindowsIdentity.GetCurrent().Owner;
            PipeSecurity remoteSecurity = _pipeClient.GetAccessControl();
            IdentityReference remoteOwner = remoteSecurity.GetOwner(typeof(SecurityIdentifier));

            if (remoteOwner != identifier)
            {
                CommunicationsUtilities.Trace("The remote pipe owner {0} does not match {1}", remoteOwner.Value, identifier.Value);
                throw new UnauthorizedAccessException();
            }
        }
#endif

        /// <summary>
        /// Connect to named pipe stream and ensure validate handshake and security.
        /// </summary>
        private void PerformHandshake(int timeout)
        {
            for (int i = 0; i < HandshakeComponents.Length; i++)
            {
                CommunicationsUtilities.Trace("Writing handshake part {0} ({1}) to pipe {2}", i, HandshakeComponents[i], PipeName);
                _pipeClient.WriteIntForHandshake(HandshakeComponents[i]);
            }

            // This indicates that we have finished all the parts of our handshake; hopefully the endpoint has as well.
            _pipeClient.WriteEndOfHandshakeSignal();

            CommunicationsUtilities.Trace("Reading handshake from pipe {0}", PipeName);
            _pipeClient.ReadEndOfHandshakeSignal(true, timeout);
        }
    }
}
