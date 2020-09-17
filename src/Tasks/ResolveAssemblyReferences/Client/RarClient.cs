// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.Build.Eventing;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Contract;
using StreamJsonRpc;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Client
{
    internal sealed class RarClient : IDisposable
    {
        /// <summary>
        /// Default connection timeout for connection to the pipe. Timeout is in millisecond.
        /// </summary>
        private const int DefaultConnectionTimeout = 300;
        private readonly IRarBuildEngine _rarBuildEngine;
        private Stream _clientStream;

        public RarClient(IRarBuildEngine rarBuildEngine)
        {
            _rarBuildEngine = rarBuildEngine;
        }

        internal bool Connect() => Connect(DefaultConnectionTimeout);

        internal bool Connect(int timeout)
        {
            if (_clientStream != null)
                return true;

            string pipeName = _rarBuildEngine.GetRarPipeName();
              
            MSBuildEventSource.Log.ResolveAssemblyReferenceNodeConnectStart();
            Stream stream = _rarBuildEngine.GetRarClientStream(pipeName, timeout);
            MSBuildEventSource.Log.ResolveAssemblyReferenceNodeConnectStop();

            if (stream == null)
                return false; // We couldn't connect

            _clientStream = stream;
            return true;
        }

        internal bool CreateNode()
        {
            return _rarBuildEngine.CreateRarNode();
        }

        internal ResolveAssemblyReferenceResult Execute(ResolveAssemblyReferenceRequest request)
        {
            var client = GetRpcClient();
            client.StartListening();
            // TODO: Find out if there is any possibility of awaiting it.
            try
            {
                return client.InvokeAsync<ResolveAssemblyReferenceResult>(nameof(IResolveAssemblyReferenceTaskHandler.ExecuteAsync), request).GetAwaiter().GetResult();
            }
            catch (ConnectionLostException e)
            {
                throw new InternalErrorException("Request failed", e);
            }
        }

        private JsonRpc GetRpcClient()
        {
            ErrorUtilities.VerifyThrowInternalErrorUnreachable(_clientStream != null);

            IJsonRpcMessageHandler handler = RpcUtils.GetRarMessageHandler(_clientStream);
            return new JsonRpc(handler);
        }

        public void Dispose()
        {
            _clientStream?.Dispose();
        }
    }
}
