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

        public ReadOnlyTaskItem(string itemSpec, IDictionary metadata)
        {
            ItemSpec = itemSpec;
            MetadataNameToValue = new Dictionary<string, string>((IDictionary<string, string>)metadata);
        }

        public string GetMetadata(string metadataName)
        {
            throw new NotImplementedException();
        }

        public void SetMetadata(string metadataName, string metadataValue)
        {
            throw new NotImplementedException();
        }

        public void RemoveMetadata(string metadataName)
        {
            throw new NotImplementedException();
        }

        public void CopyMetadataTo(ITaskItem destinationItem)
        {
            throw new NotImplementedException();
        }

        public IDictionary CloneCustomMetadata()
        {
            throw new NotImplementedException();
        }

        public string GetMetadataValueEscaped(string metadataName)
        {
            throw new NotImplementedException();

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

                ReadOnlyTaskItem readOnlyTaskItem = new ReadOnlyTaskItem(items[i].ItemSpec, items[i].CloneCustomMetadata());
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

                TaskItem item = new TaskItem(readOnlyTaskItems[i].ItemSpec, readOnlyTaskItems[i].MetadataNameToValue);
                items[i] = item;
            }

            return items;
        }

        /// <summary>
        /// This allows an explicit typecast from a "TaskItem" to a "string", returning the escaped ItemSpec for this item.
        /// </summary>
        /// <param name="taskItemToCast">The item to operate on.</param>
        /// <returns>The item-spec of the item.</returns>
        public static explicit operator string(ReadOnlyTaskItem taskItemToCast)
        {
            ErrorUtilities.VerifyThrowArgumentNull(taskItemToCast, nameof(taskItemToCast));
            return taskItemToCast.ItemSpec;
        }


        /// <summary>
        /// Gets the item-spec.
        /// </summary>
        /// <returns>The item-spec string.</returns>
        public override string ToString() => ItemSpec;
    }
}
