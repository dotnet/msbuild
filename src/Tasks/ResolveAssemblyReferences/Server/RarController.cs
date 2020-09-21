// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.IO.Pipes;
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
        /// Factory callback to NamedPipeUtils.CreateNamedPipeServer
        /// 1. arg: pipe name
        /// 2. arg: input buffer size
        /// 3. arg: input buffer size
        /// 4. arg. number of allow clients
        /// 5. arg. add right to CreateNewInstance
        /// </summary>
        private readonly Func<string, int?, int?, int, bool, Stream>? _streamFactory;

        /// <summary>
        /// Callback to validate the handshake.
        /// 1. arg: expected handshake
        /// 2. arg: named pipe over which we should validate the handshake
        /// 3. arg: timeout for validation
        /// </summary>
        private readonly Func<NamedPipeServerStream, int, bool> _validateHandshakeCallback;

        /// <summary>
        /// Handler for all incoming tasks
        /// </summary>
        private readonly IResolveAssemblyReferenceTaskHandler _resolveAssemblyReferenceTaskHandler;

        /// <summary>
        /// Timeout for incoming connections
        /// </summary>
        private readonly TimeSpan Timeout = TimeSpan.FromMinutes(15);

        /// <summary>
        /// Constructor for <see cref="RarController"/>
        /// </summary>
        /// <param name="pipeName">Name of pipe over which all communication should go</param>
        /// <param name="streamFactory">Factory for stream used in connection</param>
        /// <param name="validateHandshakeCallback">Callback to validation of connection</param>
        /// <param name="timeout">Timeout which should be used for communication</param>
        public RarController(
            string pipeName,
            Func<string, int?, int?, int, bool, Stream> streamFactory,
            Func<NamedPipeServerStream, int, bool> validateHandshakeCallback,
            TimeSpan? timeout = null)
            : this(pipeName,
                  streamFactory,
                  validateHandshakeCallback,
                  timeout: timeout,
                  resolveAssemblyReferenceTaskHandler:
                      new ResolveAssemblyReferenceHandler())
        {
        }

        internal RarController(
            string pipeName,
            Func<string, int?, int?, int, bool, Stream> streamFactory,
            Func<NamedPipeServerStream, int, bool> validateHandshakeCallback,
            IResolveAssemblyReferenceTaskHandler resolveAssemblyReferenceTaskHandler,
            TimeSpan? timeout = null)
        {
            _pipeName = pipeName;
            _streamFactory = streamFactory;
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
                Stream? serverStream = await ConnectAsync(token).ConfigureAwait(false);

                if (serverStream == null)
                    continue;

                // Connected! Refresh timeout for incoming request
                cancellationTokenSource.CancelAfter(Timeout);

                _ = HandleClientAsync(serverStream, token).ConfigureAwait(false);
            }

            return 0;
        }

        private async Task<Stream?> ConnectAsync(CancellationToken cancellationToken = default)
        {
            Stream serverStream = GetStream(_pipeName);

            if (!(serverStream is NamedPipeServerStream pipeServerStream))
            {
                return serverStream;
            }

            await pipeServerStream.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);

            if (_validateHandshakeCallback(pipeServerStream, ValidationTimeout))
            {
                return pipeServerStream;
            }

            // We couldn't validate connection, so don't use this connection at all.
            pipeServerStream.Dispose();
            return null;

        }

        internal async Task HandleClientAsync(Stream serverStream, CancellationToken cancellationToken = default)
        {
            JsonRpc server = GetRpcServer(serverStream, _resolveAssemblyReferenceTaskHandler);
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
        private Stream GetStream(string pipeName)
        {
            ErrorUtilities.VerifyThrow(_streamFactory != null, "Stream factory is not set");

            return _streamFactory!(pipeName,
                null, // Use default size
                null, // Use default size
                NamedPipeServerStream.MaxAllowedServerInstances,
                true);
        }
    }
}
