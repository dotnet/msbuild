// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Contract;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Services;
using Microsoft.VisualStudio.Threading;

using StreamJsonRpc;

#nullable enable
namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Server
{
    internal sealed class RarController : IRarController
    {
        /// <summary>
        /// The amount of time to wait for the validation of connection.
        /// </summary>
        private const int ValidationTimeout = 60000;

        /// <summary>
        /// Name of <see cref="NamedPipeServerStream"/>
        /// </summary>
        private readonly string _pipeName;

        /// <summary>
        /// Handshake used for validation of incoming connections
        /// </summary>
        private readonly Handshake _handshake;


        /// <summary>
        /// Factory callback to NamedPipeUtils.CreateNamedPipeServer
        /// 1. arg: pipe name
        /// 2. arg: input buffer size
        /// 3. arg: input buffer size
        /// 4. arg. number of allow clients
        /// 5. arg. add right to CreateNewInstance
        /// </summary>
        private readonly Func<string, int?, int?, int, bool, NamedPipeServerStream> _namedPipeServerFactory;

        /// <summary>
        /// Callback to validate the handshake.
        /// 1. arg: expected handshake
        /// 2. arg: named pipe over which we should validate the handshake
        /// 3. arg: timeout for validation
        /// </summary>
        private readonly Func<Handshake, NamedPipeServerStream, int, bool> _validateHandshakeCallback;

        /// <summary>
        /// Handler for all incoming tasks
        /// </summary>
        private readonly IResolveAssemblyReferenceTaskHandler _resolveAssemblyReferenceTaskHandler;

        /// <summary>
        /// Timeout for incoming connections
        /// </summary>
        private readonly TimeSpan Timeout = TimeSpan.FromMinutes(15);

        public RarController(
            string pipeName,
            Handshake handshake,
            Func<string, int?, int?, int, bool, NamedPipeServerStream> namedPipeServerFactory,
            Func<Handshake, NamedPipeServerStream, int, bool> validateHandshakeCallback,
            TimeSpan? timeout = null)
            : this(pipeName,
                  handshake,
                  namedPipeServerFactory,
                  validateHandshakeCallback,
                  timeout: timeout,
                  resolveAssemblyReferenceTaskHandler: new RarTaskHandler())
        {
        }

        internal RarController(
            string pipeName,
            Handshake handshake,
            Func<string, int?, int?, int, bool, NamedPipeServerStream> namedPipeServerFactory,
            Func<Handshake, NamedPipeServerStream, int, bool> validateHandshakeCallback,
            IResolveAssemblyReferenceTaskHandler resolveAssemblyReferenceTaskHandler,
            TimeSpan? timeout = null)
        {
            _pipeName = pipeName;
            _handshake = handshake;
            _namedPipeServerFactory = namedPipeServerFactory;
            _validateHandshakeCallback = validateHandshakeCallback;
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

                if (!_validateHandshakeCallback(_handshake, serverStream, ValidationTimeout))
                    continue;

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
            ErrorUtilities.VerifyThrow(_namedPipeServerFactory != null, "Stream factory is not set");

            return _namedPipeServerFactory!(pipeName,
                null, // Use default size
                null, // Use default size
                NamedPipeServerStream.MaxAllowedServerInstances,
                true);
        }
    }
}
