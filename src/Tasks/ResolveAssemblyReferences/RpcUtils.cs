// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;
using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Contract;
using Nerdbank.Streams;
using StreamJsonRpc;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences
{
    internal static class RpcUtils
    {
        private readonly static IFormatterResolver _resolver;
        private readonly static MessagePackSerializerOptions _options;

        static RpcUtils()
        {
            _resolver = ResolveAssemblyReferneceResolver.Instance;
            _options = MessagePackSerializerOptions.Standard.WithResolver(_resolver);
        }

        internal static IJsonRpcMessageHandler GetRarMessageHandler(Stream stream)
        {
            MessagePackFormatter formatter = new MessagePackFormatter();

            formatter.SetMessagePackSerializerOptions(_options);

            return new LengthHeaderMessageHandler(stream.UsePipe(), formatter);
        }
    }
}
