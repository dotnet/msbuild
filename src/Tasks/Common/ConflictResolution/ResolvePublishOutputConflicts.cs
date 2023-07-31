// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

//Microsoft.NET.Build.Extensions.Tasks (net7.0) has nullables disabled
#pragma warning disable IDE0240 // Remove redundant nullable directive
#nullable disable
#pragma warning restore IDE0240 // Remove redundant nullable directive

using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks.ConflictResolution
{
    public class ResolveOverlappingItemGroupConflicts : TaskBase
    {
        [Required]
        public ITaskItem[] ItemGroup1 { get; set; }

        [Required]
        public ITaskItem[] ItemGroup2 { get; set; }

        public string[] PreferredPackages { get; set; }

        public ITaskItem[] PackageOverrides { get; set; }

        [Output]
        public ITaskItem[] RemovedItemGroup1 { get; set; }

        [Output]
        public ITaskItem[] RemovedItemGroup2 { get; set; }

        protected override void ExecuteCore()
        {
            var packageRanks = new PackageRank(PreferredPackages);
            var packageOverrides = new PackageOverrideResolver<ConflictItem>(PackageOverrides);
            var conflicts = new HashSet<ConflictItem>();

            var conflictItemGroup1 = GetConflictTaskItems(ItemGroup1, ConflictItemType.CopyLocal);
            var conflictItemGroup2 = GetConflictTaskItems(ItemGroup2, ConflictItemType.CopyLocal);

            using (var conflictResolver = new ConflictResolver<ConflictItem>(packageRanks, packageOverrides, Log))
            {
                var allConflicts = conflictItemGroup1.Concat(conflictItemGroup2);
                conflictResolver.ResolveConflicts(allConflicts,
                    ci => ItemUtilities.GetReferenceTargetPath(ci.OriginalItem),
                    (ConflictItem winner, ConflictItem loser) => { conflicts.Add(loser); });

                var conflictItems = conflicts.Select(i => i.OriginalItem);
                RemovedItemGroup1 = ItemGroup1.Intersect(conflictItems).ToArray();
                RemovedItemGroup2 = ItemGroup2.Intersect(conflictItems).ToArray();
            }
        }

        private IEnumerable<ConflictItem> GetConflictTaskItems(ITaskItem[] items, ConflictItemType itemType)
        {
            return (items != null) ? items.Select(i => new ConflictItem(i, itemType)) : Enumerable.Empty<ConflictItem>();
        }
    }
}
