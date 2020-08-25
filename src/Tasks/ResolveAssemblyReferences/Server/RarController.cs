// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Contract;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Services;
using Microsoft.VisualStudio.Threading;

using StreamJsonRpc;

#nullable enable
namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Server
{
    public sealed class RarController
    {
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
        private readonly Func<string, int?, int?, int, bool, Stream> _streamFactory;

        /// <summary>
        /// Handler for all incoming tasks
        /// </summary>
        private readonly IResolveAssemblyReferenceTaskHandler _resolveAssemblyReferenceTaskHandler;

        /// <summary>
        /// Timeout for incoming connections
        /// </summary>
        private readonly TimeSpan Timeout = TimeSpan.FromMinutes(15);

        /// <summary>
        /// Construcotr for <see cref="RarController"/>
        /// </summary>
        /// <param name="pipeName">Name of pipe over which all comunication should go</param>
        /// <param name="streamFactory">Factor for server stream</param>
        /// <param name="timeout">Timeout which should be used for communication</param>
        public RarController(
            string pipeName,
            Func<string, int?, int?, int, bool, Stream> streamFactory,
            TimeSpan? timeout = null)
            : this(pipeName, streamFactory, timeout: timeout, resolveAssemblyReferenceTaskHandler: new ResolveAssemblyReferenceSerializedTaskHandler())
        {
        }

        internal RarController(string pipeName, Func<string, int?, int?, int, bool, Stream> streamFactory, IResolveAssemblyReferenceTaskHandler resolveAssemblyReferenceTaskHandler, TimeSpan? timeout = null)
        {
            _pipeName = pipeName;
            _streamFactory = streamFactory;
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
                Stream serverStream = await ConnectAsync(token).ConfigureAwait(false);

                // Connected! Refresh timeout for incoming request
                cancellationTokenSource.CancelAfter(Timeout);

                _ = HandleClientAsync(serverStream, token).ConfigureAwait(false);
            }

            return 0;
        }

        private async Task<Stream> ConnectAsync(CancellationToken cancellationToken = default)
        {
            Stream serverStream = GetStream(_pipeName);

            if (serverStream is NamedPipeServerStream pipeServerStream)
            {
                await pipeServerStream.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                return pipeServerStream;
            }

            return serverStream;
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
            return _streamFactory(pipeName,
                null, // Use default size
                null, // Use default size
                NamedPipeServerStream.MaxAllowedServerInstances,
                true);
        }
    }
}
