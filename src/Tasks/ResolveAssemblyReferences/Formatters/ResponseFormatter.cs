// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MessagePack;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Contract;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Formatters
{
    internal sealed class ResponseFormatter : MessagePack.Formatters.IMessagePackFormatter<ResolveAssemblyReferenceResponse>
    {
        public void Serialize(ref MessagePackWriter writer, ResolveAssemblyReferenceResponse value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            IFormatterResolver formatterResolver = options.Resolver;
            writer.WriteArrayHeader(11);
            formatterResolver.GetFormatter<ReadOnlyTaskItem[]>().Serialize(ref writer, value.CopyLocalFiles, options);
            writer.Write(value.DependsOnNETStandard);
            writer.Write(value.DependsOnSystemRuntime);
            formatterResolver.GetFormatter<ReadOnlyTaskItem[]>().Serialize(ref writer, value.FilesWritten, options);
            formatterResolver.GetFormatter<ReadOnlyTaskItem[]>().Serialize(ref writer, value.RelatedFiles, options);
            formatterResolver.GetFormatter<ReadOnlyTaskItem[]>().Serialize(ref writer, value.ResolvedDependencyFiles, options);
            formatterResolver.GetFormatter<ReadOnlyTaskItem[]>().Serialize(ref writer, value.ResolvedFiles, options);
            formatterResolver.GetFormatter<ReadOnlyTaskItem[]>().Serialize(ref writer, value.SatelliteFiles, options);
            formatterResolver.GetFormatter<ReadOnlyTaskItem[]>().Serialize(ref writer, value.ScatterFiles, options);
            formatterResolver.GetFormatter<ReadOnlyTaskItem[]>().Serialize(ref writer, value.SerializationAssemblyFiles, options);
            formatterResolver.GetFormatter<ReadOnlyTaskItem[]>().Serialize(ref writer, value.SuggestedRedirects, options);
        }

        public ResolveAssemblyReferenceResponse Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                return null;
            }

            options.Security.DepthStep(ref reader);
            IFormatterResolver formatterResolver = options.Resolver;
            int length = reader.ReadArrayHeader();
            ResolveAssemblyReferenceResponse result = new ResolveAssemblyReferenceResponse();

            for (int i = 0; i < length; i++)
            {
                switch (i)
                {
                    case 0:
                        result.CopyLocalFiles = formatterResolver.GetFormatter<ReadOnlyTaskItem[]>().Deserialize(ref reader, options);
                        break;
                    case 1:
                        result.DependsOnNETStandard = reader.ReadString();
                        break;
                    case 2:
                        result.DependsOnSystemRuntime = reader.ReadString();
                        break;
                    case 3:
                        result.FilesWritten = formatterResolver.GetFormatter<ReadOnlyTaskItem[]>().Deserialize(ref reader, options);
                        break;
                    case 4:
                        result.RelatedFiles = formatterResolver.GetFormatter<ReadOnlyTaskItem[]>().Deserialize(ref reader, options);
                        break;
                    case 5:
                        result.ResolvedDependencyFiles = formatterResolver.GetFormatter<ReadOnlyTaskItem[]>().Deserialize(ref reader, options);
                        break;
                    case 6:
                        result.ResolvedFiles = formatterResolver.GetFormatter<ReadOnlyTaskItem[]>().Deserialize(ref reader, options);
                        break;
                    case 7:
                        result.SatelliteFiles = formatterResolver.GetFormatter<ReadOnlyTaskItem[]>().Deserialize(ref reader, options);
                        break;
                    case 8:
                        result.ScatterFiles = formatterResolver.GetFormatter<ReadOnlyTaskItem[]>().Deserialize(ref reader, options);
                        break;
                    case 9:
                        result.SerializationAssemblyFiles = formatterResolver.GetFormatter<ReadOnlyTaskItem[]>().Deserialize(ref reader, options);
                        break;
                    case 10:
                        result.SuggestedRedirects = formatterResolver.GetFormatter<ReadOnlyTaskItem[]>().Deserialize(ref reader, options);
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

