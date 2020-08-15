// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;

using Nerdbank.Streams;
using StreamJsonRpc;

#nullable enable
namespace Microsoft.Build.Tasks.ResolveAssemblyReferences
{
    internal static class RpcUtils
    {
        internal static IJsonRpcMessageHandler GetRarMessageHandler(Stream stream)
        {
            return new LengthHeaderMessageHandler(stream.UsePipe(), new MessagePackFormatter());
        }
    }
}
