using Microsoft.Build.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.NET.Build.Tasks
{
    public class CheckForDuplicateItemsToRemove : TaskBase
    {
        [Required]
        public ITaskItem [] Items { get; set; }

        [Required]
        public string MetadataKeyForItemsToRemove { get; set; }

        [Output]
        public ITaskItem[] ItemsToRemove { get; set; }

        protected override void ExecuteCore()
        {
            var duplicateItems = Items.GroupBy(i => i.ItemSpec).Where(g => g.Count() > 1);
            var duplicateItemsToRemove = duplicateItems.SelectMany(g => g.Where(
                item => item.GetMetadata(MetadataKeyForItemsToRemove).Equals("true", StringComparison.OrdinalIgnoreCase)));

            ItemsToRemove = duplicateItemsToRemove.ToArray();
        }
    }
}
