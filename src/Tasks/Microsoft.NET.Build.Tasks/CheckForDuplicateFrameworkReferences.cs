// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks
{
    public class CheckForDuplicateFrameworkReferences : TaskBase
    {
        [Required]
        public ITaskItem[] FrameworkReferences { get; set; }

        [Required]
        public string MoreInformationLink { get; set; }

        [Output]
        public ITaskItem[] ItemsToAdd { get; set; }

        [Output]
        public ITaskItem[] ItemsToRemove { get; set; }


        protected override void ExecuteCore()
        {
            if (FrameworkReferences == null || FrameworkReferences.Length == 0)
            {
                return;
            }

            var duplicateItems = FrameworkReferences.GroupBy(i => i.ItemSpec, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1);

            if (duplicateItems.Any())
            {
                List<ITaskItem> itemsToAdd = new();
                List<ITaskItem> itemsToRemove = new();

                foreach (var duplicateItemGroup in duplicateItems)
                {
                    int remainingItems = 0;
                    foreach (var item in duplicateItemGroup)
                    {
                        if (item.GetMetadata(MetadataKeys.IsImplicitlyDefined).Equals("true", StringComparison.OrdinalIgnoreCase))
                        {
                            itemsToRemove.Add(item);

                            Log.LogWarning(Strings.FrameworkReferenceOverrideWarning, item.ItemSpec, MoreInformationLink);
                        }
                        else
                        {
                            remainingItems++;
                            itemsToAdd.Add(item);
                        }
                    }
                    if (remainingItems > 1)
                    {
                        Log.LogError(Strings.FrameworkReferenceDuplicateError, duplicateItemGroup.Key);
                    }
                }

                ItemsToAdd = itemsToAdd.ToArray();
                ItemsToRemove = itemsToRemove.ToArray();
            }
        }
    }
}
