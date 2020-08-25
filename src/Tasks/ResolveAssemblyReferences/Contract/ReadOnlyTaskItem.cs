using MessagePack;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Contract
{
    [MessagePackObject]
    public sealed class ReadOnlyTaskItem : ITaskItem2
    {
        [IgnoreMember]
        public ICollection MetadataNames { get; }

        [IgnoreMember]
        public int MetadataCount { get; set; }

        [Key(0)]
        public string ItemSpec { get; set; }

        [Key(1)]
        public Dictionary<string, string> MetadataNameToValue { get; set; }

      
        [IgnoreMember]
        public string EvaluatedIncludeEscaped
        {
            get => EscapingUtilities.UnescapeAll(ItemSpec);

            set => throw new NotImplementedException();
        }

       

        public ReadOnlyTaskItem(string itemSpec)
        {
            ItemSpec = itemSpec;
            MetadataNameToValue = new Dictionary<string, string>();
        }

        public string GetMetadata(string metadataName)
        {
            string metadataValue = GetMetadataValueEscaped(metadataName);
            return EscapingUtilities.UnescapeAll(metadataValue);
        }

        public void SetMetadata(string metadataName, string metadataValue)
        {
            MetadataNameToValue[metadataName] = metadataValue;
        }

        public void RemoveMetadata(string metadataName)
        {
            throw new NotImplementedException();
        }

        public void CopyMetadataTo(ITaskItem destinationItem)
        {
            foreach (KeyValuePair<string, string> metadataNameWithValue in MetadataNameToValue)
            {
                destinationItem.SetMetadata(metadataNameWithValue.Key, metadataNameWithValue.Value);
            }
        }

        public IDictionary CloneCustomMetadata()
        {
            throw new NotImplementedException();
        }

        public string GetMetadataValueEscaped(string metadataName)
        {
            bool isFound = MetadataNameToValue.TryGetValue(metadataName, out string metadataValue);
            return isFound ? metadataValue : string.Empty;
        }

        public void SetMetadataValueLiteral(string metadataName, string metadataValue)
        {
            throw new NotImplementedException();
        }

        public IDictionary CloneCustomMetadataEscaped()
        {
            throw new NotImplementedException();
        }

        internal static ReadOnlyTaskItem[] CreateArray(ITaskItem[] items)
        {
            if (items == null)
                return null;

            ReadOnlyTaskItem[] readOnlyTaskItems = new ReadOnlyTaskItem[items.Length];
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] == null)
                    continue;

                ReadOnlyTaskItem readOnlyTaskItem = new ReadOnlyTaskItem(items[i].ItemSpec);
                items[i].CopyMetadataTo(readOnlyTaskItem);
                readOnlyTaskItems[i] = readOnlyTaskItem;
            }

            return readOnlyTaskItems;
        }

        internal static ITaskItem[] ToTaskItem(ReadOnlyTaskItem[] readOnlyTaskItems)
        {
            if (readOnlyTaskItems == null)
                return null;

            ITaskItem[] items = new ITaskItem[readOnlyTaskItems.Length];
            for (int i = 0; i < items.Length; i++)
            {
                if (readOnlyTaskItems[i] == null)
                    continue;

                TaskItem item = new TaskItem(readOnlyTaskItems[i].ItemSpec);
                readOnlyTaskItems[i].CopyMetadataTo(item);
                items[i] = item;
            }

            return items;
        }
    }
}
