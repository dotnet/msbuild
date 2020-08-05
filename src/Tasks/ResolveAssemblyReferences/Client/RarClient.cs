using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Contract;
using Microsoft.VisualStudio.Threading;
using StreamJsonRpc;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Client
{
    internal sealed class RarClient : IDisposable
    {
        private const int ConnectionTimeout = 30;
        private readonly IRarBuildEngine _rarBuildEngine;
        private NamedPipeClientStream _clientStream;

        public RarClient(IRarBuildEngine rarBuildEngine)
        {
            _rarBuildEngine = rarBuildEngine;
        }


        internal bool Connect()
        {
            var pipeName = _rarBuildEngine.GetRarPipeName();
            var stream = _rarBuildEngine.GetRarClientStream(pipeName, ConnectionTimeout);

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
            using var client = GetRpcClient();
            // TODO: Find out if there is any possibility of awaiting it.
            return client.GetNumber(parameter).GetAwaiter().GetResult();
        }

        private IResolveAssemblyReferenceTaskHandler GetRpcClient()
        {
            ErrorUtilities.VerifyThrowArgumentNull(_clientStream, nameof(_clientStream));

            var handler = RpcUtils.GetRarMessageHandler(_clientStream);
            return JsonRpc.Attach<IResolveAssemblyReferenceTaskHandler>(handler);
        }

        public void Dispose()
        {
            if (_clientStream != null)
                ((IDisposable)_clientStream).Dispose();
        }
    }
}
