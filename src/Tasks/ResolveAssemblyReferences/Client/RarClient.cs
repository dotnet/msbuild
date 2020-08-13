// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO.Pipes;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Contract;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Server;
using StreamJsonRpc;


namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Client
{
    internal sealed class RarClient : IDisposable
    {
        private const int ConnectionTimeout = 300;
        private readonly IRarBuildEngine _rarBuildEngine;
        private NamedPipeClientStream _clientStream;

        public RarClient(IRarBuildEngine rarBuildEngine)
        {
            _rarBuildEngine = rarBuildEngine;
        }

        internal bool Connect() => Connect(ConnectionTimeout);

        internal bool Connect(int timeout)
        {
            if (_clientStream != null)
                return true;

            string pipeName = _rarBuildEngine.GetRarPipeName();
            NamedPipeClientStream stream = _rarBuildEngine.GetRarClientStream(pipeName, timeout);

            if (stream == null)
                return false; // We couldn't connect

            _clientStream = stream;
            return true;
        }

        internal bool CreateNode()
        {
            return _rarBuildEngine.CreateRarNode();
        }

        internal int GetNumber(int parameter)
        {
            using IResolveAssemblyReferenceTaskHandler client = GetRpcClient();

            // TODO: Find out if there is any possibility of awaiting it.
            return client.GetNumber(parameter).GetAwaiter().GetResult();
        }

        private IResolveAssemblyReferenceTaskHandler GetRpcClient()
        {
            ErrorUtilities.VerifyThrowArgumentNull(_clientStream, nameof(_clientStream));

            IJsonRpcMessageHandler handler = RpcUtils.GetRarMessageHandler(_clientStream);
            return JsonRpc.Attach<IResolveAssemblyReferenceTaskHandler>(handler);
        }

        public void Dispose()
        {
            _clientStream?.Dispose();
        }
    }
}
