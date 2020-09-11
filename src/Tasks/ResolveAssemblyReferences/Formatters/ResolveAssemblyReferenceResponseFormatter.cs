// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MessagePack;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Contract;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Formatters
{
    internal sealed class ResolveAssemblyReferenceResponseFormatter : MessagePack.Formatters.IMessagePackFormatter<ResolveAssemblyReferenceResponse>
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
            ReadOnlyTaskItem[] copyLocalFiles = default;
            string dependsOnNETStandard = default;
            string dependsOnSystemRuntime = default;
            ReadOnlyTaskItem[] filesWritten = default;
            ReadOnlyTaskItem[] relatedFiles = default;
            ReadOnlyTaskItem[] resolvedDependencyFiles = default;
            ReadOnlyTaskItem[] resolvedFiles = default;
            ReadOnlyTaskItem[] satelliteFiles = default;
            ReadOnlyTaskItem[] scatterFiles = default;
            ReadOnlyTaskItem[] serializationAssemblyFiles = default;
            ReadOnlyTaskItem[] suggestedRedirects = default;

            for (int i = 0; i < length; i++)
            {
                int key = i;

                switch (key)
                {
                    case 0:
                        copyLocalFiles = formatterResolver.GetFormatter<ReadOnlyTaskItem[]>().Deserialize(ref reader, options);
                        break;
                    case 1:
                        dependsOnNETStandard = reader.ReadString();
                        break;
                    case 2:
                        dependsOnSystemRuntime = reader.ReadString();
                        break;
                    case 3:
                        filesWritten = formatterResolver.GetFormatter<ReadOnlyTaskItem[]>().Deserialize(ref reader, options);
                        break;
                    case 4:
                        relatedFiles = formatterResolver.GetFormatter<ReadOnlyTaskItem[]>().Deserialize(ref reader, options);
                        break;
                    case 5:
                        resolvedDependencyFiles = formatterResolver.GetFormatter<ReadOnlyTaskItem[]>().Deserialize(ref reader, options);
                        break;
                    case 6:
                        resolvedFiles = formatterResolver.GetFormatter<ReadOnlyTaskItem[]>().Deserialize(ref reader, options);
                        break;
                    case 7:
                        satelliteFiles = formatterResolver.GetFormatter<ReadOnlyTaskItem[]>().Deserialize(ref reader, options);
                        break;
                    case 8:
                        scatterFiles = formatterResolver.GetFormatter<ReadOnlyTaskItem[]>().Deserialize(ref reader, options);
                        break;
                    case 9:
                        serializationAssemblyFiles = formatterResolver.GetFormatter<ReadOnlyTaskItem[]>().Deserialize(ref reader, options);
                        break;
                    case 10:
                        suggestedRedirects = formatterResolver.GetFormatter<ReadOnlyTaskItem[]>().Deserialize(ref reader, options);
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            ResolveAssemblyReferenceResponse result = new ResolveAssemblyReferenceResponse
            {
                CopyLocalFiles = copyLocalFiles,
                DependsOnNETStandard = dependsOnNETStandard,
                DependsOnSystemRuntime = dependsOnSystemRuntime,
                FilesWritten = filesWritten,
                RelatedFiles = relatedFiles,
                ResolvedDependencyFiles = resolvedDependencyFiles,
                ResolvedFiles = resolvedFiles,
                SatelliteFiles = satelliteFiles,
                ScatterFiles = scatterFiles,
                SerializationAssemblyFiles = serializationAssemblyFiles,
                SuggestedRedirects = suggestedRedirects,
            };
            reader.Depth--;
            return result;
        }
    }
}

