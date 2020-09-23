// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using MessagePack;
using MessagePack.Formatters;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Formatters
{
    internal sealed class TaskItemFormatter : IMessagePackFormatter<ITaskItem>
    {
        public void Serialize(ref MessagePackWriter writer, ITaskItem value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            string escapedItemSpec;
            IDictionary metadata;
            bool metadataIsEscaped;


            if (value is ITaskItem2 taskItem2)
            {
                escapedItemSpec = taskItem2.EvaluatedIncludeEscaped;
                metadata = taskItem2.CloneCustomMetadataEscaped();
                metadataIsEscaped = true;
            }
            else
            {
                // We know that the ITaskItem constructor expects an escaped string, and that ITaskItem.ItemSpec 
                // is expected to be unescaped, so make sure we give the constructor what it wants. 
                escapedItemSpec = EscapingUtilities.Escape(value.ItemSpec);
                metadata = value.CloneCustomMetadata();
                metadataIsEscaped = false;
            }

            if (!(metadata is Dictionary<string, string> escapedGenericMetadata))
            {
                escapedGenericMetadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (object key in metadata.Keys)
                {
                    string metdataValue = (string)metadata[key];

                    if (!metadataIsEscaped)
                    {
                        metdataValue = metdataValue == null ? metdataValue : EscapingUtilities.Escape(metdataValue);
                    }

                    escapedGenericMetadata.Add((string)key, metdataValue);
                }
            }
            else if (!metadataIsEscaped)
            {
                foreach (KeyValuePair<string, string> entry in escapedGenericMetadata)
                {
                    escapedGenericMetadata[entry.Key] = entry.Value == null ? entry.Value : EscapingUtilities.Escape(entry.Value);
                }
            }

            IFormatterResolver formatterResolver = options.Resolver;
            writer.WriteArrayHeader(2);
            writer.Write(escapedItemSpec);
            formatterResolver.GetFormatter<Dictionary<string, string>>().Serialize(ref writer, escapedGenericMetadata, options);
        }

        public ITaskItem Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                return null;
            }

            options.Security.DepthStep(ref reader);
            IFormatterResolver formatterResolver = options.Resolver;
            _ = reader.ReadArrayHeader();
            string itemSpec = reader.ReadString();
            Dictionary<string, string> metaData = formatterResolver.GetFormatter<Dictionary<string, string>>().Deserialize(ref reader, options);
            ITaskItem result = new TaskItem(itemSpec, metaData);

            reader.Depth--;
            return result;
        }
    }
}
