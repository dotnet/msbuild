// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Contract;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Services;
using Microsoft.VisualStudio.Threading;

using StreamJsonRpc;

#nullable enable
namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Server
{
    public sealed class RarController
    {
        private const int PipeBufferSize = 131072;

        /// <summary>
        /// Name of <see cref="NamedPipeServerStream"/>
        /// </summary>
        private readonly string _pipeName;

        /// <summary>
        /// Handler for all incoming tasks
        /// </summary>
        private readonly IResolveAssemblyReferenceTaskHandler _resolveAssemblyReferenceTaskHandler;

        /// <summary>
        /// Timeout for incoming connections
        /// </summary>
        private readonly TimeSpan Timeout = new TimeSpan(0, 15, 0);

        public RarController(string pipeName, TimeSpan? timeout = null) : this(pipeName, timeout: timeout, resolveAssemblyReferenceTaskHandler: new RarTaskHandler())
        {
        }

        internal RarController(string pipeName, IResolveAssemblyReferenceTaskHandler resolveAssemblyReferenceTaskHandler, TimeSpan? timeout = null)
        {
            _pipeName = pipeName;
            _resolveAssemblyReferenceTaskHandler = resolveAssemblyReferenceTaskHandler;

            if (timeout.HasValue)
            {
                Timeout = timeout.Value;
            }
        }

        public async Task<int> StartAsync(CancellationToken cancellationToken = default)
        {
            using ServerMutex mutex = new ServerMutex(_pipeName);

            if (!mutex.IsLocked)
            {
                return 1;
            }

            using CancellationTokenSource cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            CancellationToken token = cancellationTokenSource.Token;

            while (!token.IsCancellationRequested)
            {
                // server will dispose stream too.
                NamedPipeServerStream serverStream = GetStream(_pipeName);
                await serverStream.WaitForConnectionAsync(token).ConfigureAwait(false);

                // Connected! Refresh timeout for incoming request
                cancellationTokenSource.CancelAfter(Timeout);

                _ = HandleClientAsync(serverStream, token).ConfigureAwait(false);
            }

            return 0;
        }

        private async Task HandleClientAsync(Stream serverStream, CancellationToken cancellationToken = default)
        {
            using JsonRpc server = GetRpcServer(serverStream, _resolveAssemblyReferenceTaskHandler);
            server.StartListening();

            try
            {
                await server.Completion.WithCancellation(cancellationToken).ConfigureAwait(false);
            }
            catch (ConnectionLostException)
            {
                // Some problem with connection, let's ignore it.
                // All other exceptions are issue though
            }
        }

        private JsonRpc GetRpcServer(Stream stream, IResolveAssemblyReferenceTaskHandler handler)
        {
            IJsonRpcMessageHandler serverHandler = RpcUtils.GetRarMessageHandler(stream);
            JsonRpc rpc = new JsonRpc(serverHandler, handler);
            return rpc;
        }

        /// <summary>
        /// Instantiates an endpoint to act as a client
        /// </summary>
        /// <param name="pipeName">The name of the pipe to which we should connect.</param>
        private NamedPipeServerStream GetStream(string pipeName)
        {
            ErrorUtilities.VerifyThrowArgumentLength(pipeName, "pipeName");
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
                // NOTE: There has to be PipeAccessRights.CreateNewInstance, without it we can't create new pipes
                PipeAccessRule rule = new PipeAccessRule(identifier, PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance, AccessControlType.Allow);
                security.AddAccessRule(rule);
                security.SetOwner(identifier);

                return new NamedPipeServerStream
                    (
                    pipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances, // Only allow one connection at a time.
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous,
                    PipeBufferSize, // Default input buffer
                    PipeBufferSize,  // Default output buffer
                    security,
                    HandleInheritability.None
                );
            }
            else
#endif
            {
                return new NamedPipeServerStream
                (
                    pipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous,
                    PipeBufferSize, // Default input buffer
                    PipeBufferSize  // Default output buffer
                );
            }
        }
    }
}
