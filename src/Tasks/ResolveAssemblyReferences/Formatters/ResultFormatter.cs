// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using MessagePack;
using MessagePack.Formatters;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Contract;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Formatters
{
    internal sealed class ResultFormatter : IMessagePackFormatter<ResolveAssemblyReferenceResult>
    {
        public void Serialize(ref MessagePackWriter writer, ResolveAssemblyReferenceResult value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            IFormatterResolver formatterResolver = options.Resolver;
            writer.WriteArrayHeader(3);
            formatterResolver.GetFormatter<List<BuildEventArgs>>().Serialize(ref writer, value.BuildEvents, options);
            formatterResolver.GetFormatter<ResolveAssemblyReferenceResponse>().Serialize(ref writer, value.Response, options);
            writer.Write(value.TaskResult);
        }

        public ResolveAssemblyReferenceResult Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                return null;
            }

            options.Security.DepthStep(ref reader);
            IFormatterResolver formatterResolver = options.Resolver;
            int length = reader.ReadArrayHeader();

            ResolveAssemblyReferenceResult result = new ResolveAssemblyReferenceResult();

            for (int i = 0; i < length; i++)
            {
                switch (i)
                {
                    case 2:
                        result.TaskResult = reader.ReadBoolean();
                        break;
                    case 1:
                        result.Response = formatterResolver.GetFormatter<ResolveAssemblyReferenceResponse>().Deserialize(ref reader, options);
                        break;
                    case 0:
                        result.BuildEvents = formatterResolver.GetFormatter<List<BuildEventArgs>>().Deserialize(ref reader, options);
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            reader.Depth--;
            return result;
        }
    }
}
