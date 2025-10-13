// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.IO.Pipes;
#if !FEATURE_PIPEOPTIONS_CURRENTUSERONLY
using System.Security.AccessControl;
using System.Security.Principal;
#endif
using Microsoft.Build.BackEnd;
using Microsoft.Build.Shared;

#if !TASKHOST
using System.Threading;
using System.Threading.Tasks;
#endif

namespace Microsoft.Build.Internal
{
    internal sealed class NodePipeServer : NodePipeBase
    {
        /// <summary>
        /// The size of kernel-level buffers used by the named pipe. If the total size of pending reads or write requests exceed
        /// this amount (known as the quota), IO will block until either pending operations complete, or the OS increases the quota.
        /// </summary>
        private const int PipeBufferSize = 131_072;

#if NET
        /// <summary>
        /// A timeout for the handshake. This is only used on Unix-like socket implementations, because the
        /// timeout on the PipeStream connection is ignore.
        /// </summary>
        private static readonly int s_handshakeTimeout = NativeMethodsShared.IsWindows ? 0 : 60_000;
#endif

        private readonly NamedPipeServerStream _pipeServer;

        internal NodePipeServer(string pipeName, Handshake handshake, int maxNumberOfServerInstances = 1)
            : base(pipeName, handshake)
        {
            PipeOptions pipeOptions = PipeOptions.Asynchronous;
#if FEATURE_PIPEOPTIONS_CURRENTUSERONLY
            pipeOptions |= PipeOptions.CurrentUserOnly;
#else
            // Restrict access to just this account.  We set the owner specifically here, and on the
            // pipe client side they will check the owner against this one - they must have identical
            // SIDs or the client will reject this server.  This is used to avoid attacks where a
            // hacked server creates a less restricted pipe in an attempt to lure us into using it and
            // then sending build requests to the real pipe client (which is the MSBuild Build Manager.)
            PipeAccessRights pipeAccessRights = PipeAccessRights.ReadWrite;
            if (maxNumberOfServerInstances > 1)
            {
                // Multi-instance pipes will fail without this flag.
                pipeAccessRights |= PipeAccessRights.CreateNewInstance;
            }

            PipeAccessRule rule = new(WindowsIdentity.GetCurrent().Owner, pipeAccessRights, AccessControlType.Allow);
            PipeSecurity security = new();
            security.AddAccessRule(rule);
            security.SetOwner(rule.IdentityReference);
#endif

            _pipeServer = new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                maxNumberOfServerInstances,
                PipeTransmissionMode.Byte,
                pipeOptions,
                inBufferSize: PipeBufferSize,
                outBufferSize: PipeBufferSize
#if !FEATURE_PIPEOPTIONS_CURRENTUSERONLY
                , security,
                HandleInheritability.None
#endif
#pragma warning disable SA1111 // Closing parenthesis should be on line of last parameter
                );
#pragma warning restore SA1111 // Closing parenthesis should be on line of last parameter
        }

        protected override PipeStream NodeStream => _pipeServer;

#if TASKHOST
        internal LinkStatus WaitForConnection()
#else
        internal async Task<LinkStatus> WaitForConnectionAsync(CancellationToken cancellationToken)
#endif
        {
            DateTime originalWaitStartTime = DateTime.UtcNow;
            bool gotValidConnection = false;

            while (!gotValidConnection)
            {
                gotValidConnection = true;
                DateTime restartWaitTime = DateTime.UtcNow;

                // We only wait to wait the difference between now and the last original start time, in case we have multiple hosts attempting
                // to attach.  This prevents each attempt from resetting the timer.
                TimeSpan usedWaitTime = restartWaitTime - originalWaitStartTime;
                int waitTimeRemaining = Math.Max(0, CommunicationsUtilities.NodeConnectionTimeout - (int)usedWaitTime.TotalMilliseconds);

                try
                {
                    // Wait for a connection
#if TASKHOST
                    IAsyncResult resultForConnection = _pipeServer.BeginWaitForConnection(null, null);
                    CommunicationsUtilities.Trace("Waiting for connection {0} ms...", waitTimeRemaining);
                    bool connected = resultForConnection.AsyncWaitHandle.WaitOne(waitTimeRemaining, false);
                    _pipeServer.EndWaitForConnection(resultForConnection);
#else
                    using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    cts.CancelAfter(waitTimeRemaining);
                    bool connected = false;
                    try
                    {
                        CommunicationsUtilities.Trace("Waiting for connection {0} ms...", waitTimeRemaining);
                        await _pipeServer.WaitForConnectionAsync(cts.Token).ConfigureAwait(false);
                        connected = true;
                    }
                    catch (OperationCanceledException)
                    {
                        connected = false;
                    }
#endif
                    if (!connected)
                    {
                        CommunicationsUtilities.Trace("Connection timed out waiting a host to contact us.  Exiting comm thread.");
                        return LinkStatus.ConnectionFailed;
                    }

                    CommunicationsUtilities.Trace("Parent started connecting. Reading handshake from parent");

                    // The handshake protocol is a series of int exchanges.  The host sends us a each component, and we
                    // verify it. Afterwards, the host sends an "End of Handshake" signal, to which we respond in kind.
                    // Once the handshake is complete, both sides can be assured the other is ready to accept data.
                    try
                    {
                        gotValidConnection = ValidateHandshake();
#if !FEATURE_PIPEOPTIONS_CURRENTUSERONLY
                        gotValidConnection &= ValidateClientIdentity();
#endif
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

                    if (!gotValidConnection && _pipeServer.IsConnected)
                    {
                        _pipeServer.Disconnect();
                    }
                }
                catch (Exception e) when (!ExceptionHandling.IsCriticalException(e))
                {
                    CommunicationsUtilities.Trace("Client connection failed.  Exiting comm thread. {0}", e);
                    if (_pipeServer.IsConnected)
                    {
                        _pipeServer.Disconnect();
                    }

                    ExceptionHandling.DumpExceptionToFile(e);
                    return LinkStatus.Failed;
                }
            }

            return LinkStatus.Active;
        }

        internal void Disconnect()
        {
            try
            {
                if (_pipeServer.IsConnected)
                {
#if NET // OperatingSystem.IsWindows() is new in .NET 5.0
                    if (OperatingSystem.IsWindows())
#endif
                    {
                        _pipeServer.WaitForPipeDrain();
                    }

                    _pipeServer.Disconnect();
                }
            }
            catch (Exception)
            {
                // We don't really care if Disconnect somehow fails, but it gives us a chance to do the right thing.
            }
        }

        private bool ValidateHandshake()
        {
            int index = 0;
            foreach (var component in HandshakeComponents.EnumerateComponents())
            {
                // This will disconnect a < 16.8 host; it expects leading 00 or F5 or 06. 0x00 is a wildcard.

                if (

                    _pipeServer.TryReadIntForHandshake(byteToAccept: index == 0 ? CommunicationsUtilities.handshakeVersion : null,
#if NET
                     s_handshakeTimeout,
#endif
                     out HandshakeResult handshakePart))
                {
                    if (handshakePart.Value != component.Value)
                    {
                        CommunicationsUtilities.Trace("Handshake failed. Received {0} from host not {1}. Probably the host is a different MSBuild build.", handshakePart, component.Value);
                        _pipeServer.WriteIntForHandshake(index + 1);
                        return false;
                    }
                }
                else
                {
                    return false;
                }

                index++;
            }

            // To ensure that our handshake and theirs have the same number of bytes, receive and send a magic number indicating EOS.

            if (_pipeServer.TryReadEndOfHandshakeSignal(false,
#if NET
            s_handshakeTimeout,
#endif
            out HandshakeResult _))
            {
                CommunicationsUtilities.Trace("Successfully connected to parent.");
                _pipeServer.WriteEndOfHandshakeSignal();

                return true;
            }
            else
            {
                return false;
            }
        }

#if !FEATURE_PIPEOPTIONS_CURRENTUSERONLY
        private bool ValidateClientIdentity()
        {
            // We will only talk to a host that was started by the same user as us.  Even though the pipe access is set to only allow this user, we want to ensure they
            // haven't attempted to change those permissions out from under us.  This ensures that the only way they can truly gain access is to be impersonating the
            // user we were started by.
            WindowsIdentity currentIdentity = WindowsIdentity.GetCurrent();
            WindowsIdentity? clientIdentity = null;
            _pipeServer.RunAsClient(() => { clientIdentity = WindowsIdentity.GetCurrent(true); });

            if (clientIdentity == null || !string.Equals(clientIdentity.Name, currentIdentity.Name, StringComparison.OrdinalIgnoreCase))
            {
                CommunicationsUtilities.Trace("Handshake failed. Host user is {0} but we were created by {1}.", (clientIdentity == null) ? "<unknown>" : clientIdentity.Name, currentIdentity.Name);
                return false;
            }

            return true;
        }
#endif

    }
}
