// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks
{
    public class CheckForImplicitPackageReferenceOverrides : TaskBase
    {
        [Required]
        public ITaskItem [] PackageReferenceItems { get; set; }

        [Required]
        public string MoreInformationLink { get; set; }

        [Output]
        public ITaskItem[] ItemsToRemove { get; set; }

        [Output]
        public ITaskItem[] ItemsToAdd { get; set; }

        protected override void ExecuteCore()
        {
            var duplicateItems = PackageReferenceItems.GroupBy(i => i.ItemSpec, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1);

            if (duplicateItems.Any())
            {
                List<ITaskItem> itemsToRemove = new List<ITaskItem>();
                List<ITaskItem> itemsToAdd = new List<ITaskItem>();
                foreach (var duplicateItemGroup in duplicateItems)
                {
                    foreach (var item in duplicateItemGroup)
                    {
                        if (item.GetMetadata(MetadataKeys.IsImplicitlyDefined).Equals("true", StringComparison.OrdinalIgnoreCase))
                        {
                            itemsToRemove.Add(item);
  
                            Log.LogWarning(Strings.PackageReferenceOverrideWarning, item.ItemSpec, MoreInformationLink);
                        }
                        else
                        {
                            //  For the explicit items, we want to add metadata to them so that the ApplyImplicitVersions task
                            //  won't generate another error.  The easiest way to do this is to add them both to a list of
                            //  items to remove, and then a list of items which gets added back.
                            itemsToRemove.Add(item);
                            item.SetMetadata(MetadataKeys.AllowExplicitVersion, "true");
                            itemsToAdd.Add(item);
                        }
                    }
                }

                ItemsToRemove = itemsToRemove.ToArray();
                ItemsToAdd = itemsToAdd.ToArray();
            }
        }
    }
}
