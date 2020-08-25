using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Contract;

namespace Microsoft.Build.Tasks.UnitTests.AssemblyDependency
{
    internal class ReadOnlyTaskItemComparer : BaseComparer<ReadOnlyTaskItem>
    {
        internal static IEqualityComparer<ReadOnlyTaskItem> Instance { get; } = new ReadOnlyTaskItemComparer();
        private ReadOnlyTaskItemComparer() { }

        public override bool Equals(ReadOnlyTaskItem x, ReadOnlyTaskItem y)
        {
            // Same reference or null
            if (x == y)
                return true;

            return
                //EqualityComparer<ICollection>.Default.Equals(x.MetadataNames.Count, y.MetadataNames) &&
               x.MetadataCount == y.MetadataCount &&
               x.ItemSpec == y.ItemSpec &&
               CollectionEquals(x.MetadataNameToValue, y.MetadataNameToValue, EqualityComparer<KeyValuePair<string, string>>.Default) &&
               x.EvaluatedIncludeEscaped == y.EvaluatedIncludeEscaped;
        }
    }
}
