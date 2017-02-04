using Microsoft.Build.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Microsoft.NET.Build.Tasks
{
    public class CheckForImplicitPackageReferenceOverrides : TaskBase
    {
        const string MetadataKeyForItemsToRemove = "IsImplicitlyDefined";

        [Required]
        public ITaskItem [] PackageReferenceItems { get; set; }

        [Required]
        public string MoreInformationLink { get; set; }

        [Output]
        public ITaskItem[] ItemsToRemove { get; set; }

        protected override void ExecuteCore()
        {
            var duplicateItems = PackageReferenceItems.GroupBy(i => i.ItemSpec, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1);
            var duplicateItemsToRemove = duplicateItems.SelectMany(g => g.Where(
                item => item.GetMetadata(MetadataKeyForItemsToRemove).Equals("true", StringComparison.OrdinalIgnoreCase)));

            ItemsToRemove = duplicateItemsToRemove.ToArray();

            foreach (var itemToRemove in ItemsToRemove)
            {
                string message = string.Format(CultureInfo.CurrentCulture, Strings.PackageReferenceOverrideWarning,
                    itemToRemove.ItemSpec,
                    MoreInformationLink);

                Log.LogWarning(message);
            }
        }
    }
}
