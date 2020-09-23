// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MessagePack;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Contract;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Formatters
{
    internal sealed class ResponseFormatter : MessagePack.Formatters.IMessagePackFormatter<ResolveAssemblyReferenceResponse>
    {
        internal const int MemberCount = 11;

        public void Serialize(ref MessagePackWriter writer, ResolveAssemblyReferenceResponse value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            IFormatterResolver formatterResolver = options.Resolver;
            writer.WriteArrayHeader(MemberCount);
            formatterResolver.GetFormatter<ITaskItem[]>().Serialize(ref writer, value.CopyLocalFiles, options);
            writer.Write(value.DependsOnNETStandard);
            writer.Write(value.DependsOnSystemRuntime);
            formatterResolver.GetFormatter<ITaskItem[]>().Serialize(ref writer, value.FilesWritten, options);
            formatterResolver.GetFormatter<ITaskItem[]>().Serialize(ref writer, value.RelatedFiles, options);
            formatterResolver.GetFormatter<ITaskItem[]>().Serialize(ref writer, value.ResolvedDependencyFiles, options);
            formatterResolver.GetFormatter<ITaskItem[]>().Serialize(ref writer, value.ResolvedFiles, options);
            formatterResolver.GetFormatter<ITaskItem[]>().Serialize(ref writer, value.SatelliteFiles, options);
            formatterResolver.GetFormatter<ITaskItem[]>().Serialize(ref writer, value.ScatterFiles, options);
            formatterResolver.GetFormatter<ITaskItem[]>().Serialize(ref writer, value.SerializationAssemblyFiles, options);
            formatterResolver.GetFormatter<ITaskItem[]>().Serialize(ref writer, value.SuggestedRedirects, options);
        }

        public ResolveAssemblyReferenceResponse Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                return null;
            }

            options.Security.DepthStep(ref reader);
            IFormatterResolver formatterResolver = options.Resolver;
            int _ = reader.ReadArrayHeader(); // Content starts with this
            ResolveAssemblyReferenceResponse result = new ResolveAssemblyReferenceResponse();

            result.CopyLocalFiles = formatterResolver.GetFormatter<ITaskItem[]>().Deserialize(ref reader, options);
            result.DependsOnNETStandard = reader.ReadString();
            result.DependsOnSystemRuntime = reader.ReadString();
            result.FilesWritten = formatterResolver.GetFormatter<ITaskItem[]>().Deserialize(ref reader, options);
            result.RelatedFiles = formatterResolver.GetFormatter<ITaskItem[]>().Deserialize(ref reader, options);
            result.ResolvedDependencyFiles = formatterResolver.GetFormatter<ITaskItem[]>().Deserialize(ref reader, options);
            result.ResolvedFiles = formatterResolver.GetFormatter<ITaskItem[]>().Deserialize(ref reader, options);
            result.SatelliteFiles = formatterResolver.GetFormatter<ITaskItem[]>().Deserialize(ref reader, options);
            result.ScatterFiles = formatterResolver.GetFormatter<ITaskItem[]>().Deserialize(ref reader, options);
            result.SerializationAssemblyFiles = formatterResolver.GetFormatter<ITaskItem[]>().Deserialize(ref reader, options);
            result.SuggestedRedirects = formatterResolver.GetFormatter<ITaskItem[]>().Deserialize(ref reader, options);

            reader.Depth--;
            return result;
        }
    }
}

