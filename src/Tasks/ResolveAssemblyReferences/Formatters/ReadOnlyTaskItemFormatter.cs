using MessagePack;
using MessagePack.Formatters;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Contract;
using System.Collections.Generic;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Formatters
{
    internal sealed class ReadOnlyTaskItemFormatter : IMessagePackFormatter<ReadOnlyTaskItem>
    {
        public void Serialize(ref MessagePackWriter writer, ReadOnlyTaskItem value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            IFormatterResolver formatterResolver = options.Resolver;
            writer.WriteArrayHeader(2);
            writer.Write(value.ItemSpec);
            formatterResolver.GetFormatter<Dictionary<string, string>>().Serialize(ref writer, value.MetadataNameToValue, options);
        }

        public ReadOnlyTaskItem Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                return null;
            }

            options.Security.DepthStep(ref reader);
            IFormatterResolver formatterResolver = options.Resolver;
            int length = reader.ReadArrayHeader();
            string itemSpec = null;
            Dictionary<string, string> metadataNameToValue = null;

            for (int i = 0; i < length; i++)
            {
                var key = i;

                switch (key)
                {
                    case 0:
                        itemSpec = reader.ReadString();
                        break;
                    case 1:
                        metadataNameToValue = formatterResolver.GetFormatter<Dictionary<string, string>>().Deserialize(ref reader, options);
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            ReadOnlyTaskItem result = new ReadOnlyTaskItem(itemSpec)
            {
                ItemSpec = itemSpec,
                MetadataNameToValue = metadataNameToValue
            };
            reader.Depth--;
            return result;
        }
    }
}
