using System;
using System.Buffers;
using System.Collections.Generic;
using MessagePack;
using MessagePack.Formatters;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Contract;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Formatters
{
    internal sealed class ResolveAssemblyReferenceResultFormatter : IMessagePackFormatter<ResolveAssemblyReferenceResult>
    {
        public void Serialize(ref MessagePackWriter writer, ResolveAssemblyReferenceResult value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            IFormatterResolver formatterResolver = options.Resolver;
            writer.WriteArrayHeader(8);
            writer.Write(value.TaskResult);
            formatterResolver.GetFormatter<ResolveAssemblyReferenceResponse>().Serialize(ref writer, value.Response, options);
            writer.Write(value.EventCount);
            formatterResolver.GetFormatter<List<CustomBuildEventArgs>>().Serialize(ref writer, value.CustomBuildEvents, options);
            formatterResolver.GetFormatter<List<BuildErrorEventArgs>>().Serialize(ref writer, value.BuildErrorEvents, options);
            formatterResolver.GetFormatter<List<BuildMessageEventArgs>>().Serialize(ref writer, value.BuildMessageEvents, options);
            formatterResolver.GetFormatter<List<BuildWarningEventArgs>>().Serialize(ref writer, value.BuildWarningEvents, options);
            formatterResolver.GetFormatter<ResolveAssemblyReferenceRequest>().Serialize(ref writer, value.Request, options);
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
            List<BuildErrorEventArgs> buildErrorEvents = default;
            List<BuildMessageEventArgs> buildMessageEvents = default;
            List<BuildWarningEventArgs> buildWarningEvents = default;
            List<CustomBuildEventArgs> customBuildEvents = default;
            int eventCount = default;
            ResolveAssemblyReferenceResponse response = default;
            ResolveAssemblyReferenceRequest request = default;
            bool taskResult = default;

            for (int i = 0; i < length; i++)
            {
                int key = i;

                switch (key)
                {
                    case 4:
                        buildErrorEvents = formatterResolver.GetFormatter<List<BuildErrorEventArgs>>().Deserialize(ref reader, options);
                        break;
                    case 5:
                        buildMessageEvents = formatterResolver.GetFormatter<List<BuildMessageEventArgs>>().Deserialize(ref reader, options);
                        break;
                    case 6:
                        buildWarningEvents = formatterResolver.GetFormatter<List<BuildWarningEventArgs>>().Deserialize(ref reader, options);
                        break;
                    case 3:
                        customBuildEvents = formatterResolver.GetFormatter<List<CustomBuildEventArgs>>().Deserialize(ref reader, options);
                        break;
                    case 2:
                        eventCount = reader.ReadInt32();
                        break;
                    case 1:
                        response = formatterResolver.GetFormatter<ResolveAssemblyReferenceResponse>().Deserialize(ref reader, options);
                        break;
                    case 7:
                        request = formatterResolver.GetFormatter<ResolveAssemblyReferenceRequest>().Deserialize(ref reader, options);
                        break;
                    case 0:
                        taskResult = reader.ReadBoolean();
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            ResolveAssemblyReferenceResult result = new ResolveAssemblyReferenceResult
            {
                BuildErrorEvents = buildErrorEvents,
                BuildMessageEvents = buildMessageEvents,
                BuildWarningEvents = buildWarningEvents,
                CustomBuildEvents = customBuildEvents,
                EventCount = eventCount,
                Response = response,
                TaskResult = taskResult,
                Request = request
            };
            reader.Depth--;
            return result;
        }
    }
}
