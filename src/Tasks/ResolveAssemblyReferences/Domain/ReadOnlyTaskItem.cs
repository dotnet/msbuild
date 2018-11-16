using System.Collections;
using System.Collections.Generic;

using Microsoft.Build.Collections;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Domain
{
    public partial class ReadOnlyTaskItem : ITaskItem2
    {
        public ICollection MetadataNames { get; }

        public int MetadataCount => MetadataNameToValue.Count;

        public string EvaluatedIncludeEscaped
        {
            get => EscapingUtilities.UnescapeAll(ItemSpec);

            set => throw new System.NotImplementedException();
        }

        internal ReadOnlyTaskItem(string itemSpec, int capacity = 0)
        {
            ItemSpec = itemSpec;
            _metadataNameToValue = new Dictionary<string, string>(capacity, MSBuildNameIgnoreCaseComparer.Default);
        }

        internal ReadOnlyTaskItem(string itemSpec, Dictionary<string, string> metadataNameToValue)
        {
            ItemSpec = itemSpec;
            _metadataNameToValue = metadataNameToValue;
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
            throw new System.NotImplementedException();
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
            throw new System.NotImplementedException();
        }

        public string GetMetadataValueEscaped(string metadataName)
        {
            bool isFound = MetadataNameToValue.TryGetValue(metadataName, out string metadataValue);
            return isFound ? metadataValue : string.Empty;
        }

        public void SetMetadataValueLiteral(string metadataName, string metadataValue)
        {
            throw new System.NotImplementedException();
        }

        public IDictionary CloneCustomMetadataEscaped()
        {
            throw new System.NotImplementedException();
        }

        internal void AddResponseField(TaskItemField field)
        {
            ResponseFieldIds |= (int)field;
        }

        internal bool IsResponseField(TaskItemField field)
        {
            return (ResponseFieldIds & (int) field) == (int) field;
        }
    }
}
