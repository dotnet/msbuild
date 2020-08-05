using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nerdbank.Streams;
using StreamJsonRpc;

#nullable enable
namespace Microsoft.Build.Tasks.ResolveAssemblyReferences
{
    internal static class RpcUtils
    {
        internal static IJsonRpcMessageHandler GetRarMessageHandler(Stream stream)
        {
            var formatter = new MessagePackFormatter();
            return new LengthHeaderMessageHandler(stream.UsePipe(), formatter);
        }
    }
}
