// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;
using Microsoft.Build.Framework;
using Nerdbank.Streams;
using StreamJsonRpc;

#nullable enable
namespace Microsoft.Build.Tasks.ResolveAssemblyReferences
{
    internal static class RpcUtils
    {
        internal static IJsonRpcMessageHandler GetRarMessageHandler(Stream stream)
        {
            MessagePackFormatter formatter = new MessagePackFormatter();

            IFormatterResolver resolver = CompositeResolver.Create(
                new IMessagePackFormatter[]
                {
                    BuildEventArgsFormatter<CustomBuildEventArgs>.CustomFormatter,
                    BuildEventArgsFormatter<BuildErrorEventArgs>.ErrorFormatter,
                    BuildEventArgsFormatter<BuildWarningEventArgs>.WarningFormatter,
                    BuildEventArgsFormatter<BuildMessageEventArgs>.MessageFormatter
                },
                new[]
                {
                    StandardResolver.Instance
                }
            );
            MessagePackSerializerOptions options = MessagePackSerializerOptions.Standard.WithResolver(resolver);

            formatter.SetMessagePackSerializerOptions(options);
            return new LengthHeaderMessageHandler(stream.UsePipe(), formatter);
        }
    }
}
