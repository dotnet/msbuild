// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.NET.Build.Tasks.ConflictResolution
{
    public class ResolvePackageFileConflicts : TaskBase
    {
        private HashSet<ITaskItem> referenceConflicts = new HashSet<ITaskItem>();
        private HashSet<ITaskItem> copyLocalConflicts = new HashSet<ITaskItem>();
        private HashSet<ConflictItem> allConflicts = new HashSet<ConflictItem>();

        public ITaskItem[] References { get; set; }

        public ITaskItem[] ReferenceCopyLocalPaths { get; set; }

        public ITaskItem[] OtherRuntimeItems { get; set; }

        public ITaskItem[] PlatformManifests { get; set; }

        public ITaskItem[] TargetFrameworkDirectories { get; set; }

        /// <summary>
        /// NuGet3 and later only.  In the case of a conflict with identical file version information a file from the most preferred package will be chosen.
        /// </summary>
        public string[] PreferredPackages { get; set; }

        /// <summary>
        /// A collection of items that contain information of which packages get overridden
        /// by which packages before doing any other conflict resolution.
        /// </summary>
        /// <remarks>
        /// This is an optimizaiton so AssemblyVersions, FileVersions, etc. don't need to be read
        /// in the default cases where platform packages (Microsoft.NETCore.App) should override specific packages
        /// (System.Console v4.3.0).
        /// </remarks>
        public ITaskItem[] PackageOverrides { get; set; }

        [Output]
        public ITaskItem[] ReferencesWithoutConflicts { get; set; }

        [Output]
        public ITaskItem[] ReferenceCopyLocalPathsWithoutConflicts { get; set; }

        [Output]
        public ITaskItem[] Conflicts { get; set; }

        protected override void ExecuteCore()
        {
            var log = new MSBuildLog(Log);
            var packageRanks = new PackageRank(PreferredPackages);
            var packageOverrides = new PackageOverrideResolver<ConflictItem>(PackageOverrides);

            //  Treat assemblies from FrameworkList.xml as platform assemblies that also get considered at compile time
            IEnumerable<ConflictItem> compilePlatformItems = null;
            if (TargetFrameworkDirectories != null && TargetFrameworkDirectories.Any())
            {
                var frameworkListReader = new FrameworkListReader(BuildEngine4);

                compilePlatformItems = TargetFrameworkDirectories.SelectMany(tfd =>
                {
                    return frameworkListReader.GetConflictItems(Path.Combine(tfd.ItemSpec, "RedistList", "FrameworkList.xml"), log);
                });
            }

            // resolve conflicts at compile time
            var referenceItems = GetConflictTaskItems(References, ConflictItemType.Reference).ToArray();

            var compileConflictScope = new ConflictResolver<ConflictItem>(packageRanks, packageOverrides, log);

            compileConflictScope.ResolveConflicts(referenceItems,
                ci => ItemUtilities.GetReferenceFileName(ci.OriginalItem),
                HandleCompileConflict);

            if (compilePlatformItems != null)
            {
                compileConflictScope.ResolveConflicts(compilePlatformItems,
                    ci => ci.FileName,
                    HandleCompileConflict);
            }

            // resolve conflicts that class in output
            var runtimeConflictScope = new ConflictResolver<ConflictItem>(packageRanks, packageOverrides, log);

            runtimeConflictScope.ResolveConflicts(referenceItems,
                ci => ItemUtilities.GetReferenceTargetPath(ci.OriginalItem),
                HandleRuntimeConflict);

            var copyLocalItems = GetConflictTaskItems(ReferenceCopyLocalPaths, ConflictItemType.CopyLocal).ToArray();

            runtimeConflictScope.ResolveConflicts(copyLocalItems,
                ci => ItemUtilities.GetTargetPath(ci.OriginalItem),
                HandleRuntimeConflict);

            var otherRuntimeItems = GetConflictTaskItems(OtherRuntimeItems, ConflictItemType.Runtime).ToArray();

            runtimeConflictScope.ResolveConflicts(otherRuntimeItems,
                ci => ItemUtilities.GetTargetPath(ci.OriginalItem),
                HandleRuntimeConflict);


            // resolve conflicts with platform (eg: shared framework) items
            // we only commit the platform items since its not a conflict if other items share the same filename.
            var platformConflictScope = new ConflictResolver<ConflictItem>(packageRanks, packageOverrides, log);
            var platformItems = PlatformManifests?.SelectMany(pm => PlatformManifestReader.LoadConflictItems(pm.ItemSpec, log)) ?? Enumerable.Empty<ConflictItem>();

            if (compilePlatformItems != null)
            {
                platformItems = platformItems.Concat(compilePlatformItems);
            }

            platformConflictScope.ResolveConflicts(platformItems, pi => pi.FileName, pi => { });
            platformConflictScope.ResolveConflicts(referenceItems.Where(ri => !referenceConflicts.Contains(ri.OriginalItem)),
                                                   ri => ItemUtilities.GetReferenceTargetFileName(ri.OriginalItem),
                                                   HandleRuntimeConflict,
                                                   commitWinner:false);
            platformConflictScope.ResolveConflicts(copyLocalItems.Where(ci => !copyLocalConflicts.Contains(ci.OriginalItem)),
                                                   ri => ri.FileName,
                                                   HandleRuntimeConflict,
                                                   commitWinner: false);
            platformConflictScope.ResolveConflicts(otherRuntimeItems,
                                                   ri => ri.FileName,
                                                   HandleRuntimeConflict,
                                                   commitWinner: false);

            ReferencesWithoutConflicts = RemoveConflicts(References, referenceConflicts);
            ReferenceCopyLocalPathsWithoutConflicts = RemoveConflicts(ReferenceCopyLocalPaths, copyLocalConflicts);
            Conflicts = CreateConflictTaskItems(allConflicts);
        }

        private ITaskItem[] CreateConflictTaskItems(ICollection<ConflictItem> conflicts)
        {
            var conflictItems = new ITaskItem[conflicts.Count];

            int i = 0;
            foreach(var conflict in conflicts)
            {
                conflictItems[i++] = CreateConflictTaskItem(conflict);
            }

            return conflictItems;
        }

        private ITaskItem CreateConflictTaskItem(ConflictItem conflict)
        {
            var item = new TaskItem(conflict.SourcePath);

            if (conflict.PackageId != null)
            {
                item.SetMetadata(nameof(ConflictItemType), conflict.ItemType.ToString());
            }

            return item;
        }

        private IEnumerable<ConflictItem> GetConflictTaskItems(ITaskItem[] items, ConflictItemType itemType)
        {
            return (items != null) ? items.Select(i => new ConflictItem(i, itemType)) : Enumerable.Empty<ConflictItem>();
        }

        private void HandleCompileConflict(ConflictItem conflictItem)
        {
            if (conflictItem.ItemType == ConflictItemType.Reference)
            {
                referenceConflicts.Add(conflictItem.OriginalItem);
            }
            allConflicts.Add(conflictItem);
        }

        private void HandleRuntimeConflict(ConflictItem conflictItem)
        {
            if (conflictItem.ItemType == ConflictItemType.Reference)
            {
                conflictItem.OriginalItem.SetMetadata(MetadataNames.Private, "False");
            }
            else if (conflictItem.ItemType == ConflictItemType.CopyLocal)
            {
                copyLocalConflicts.Add(conflictItem.OriginalItem);
            }
            allConflicts.Add(conflictItem);
        }

        /// <summary>
        /// Filters conflicts from original, maintaining order.
        /// </summary>
        /// <param name="original"></param>
        /// <param name="conflicts"></param>
        /// <returns></returns>
        private ITaskItem[] RemoveConflicts(ITaskItem[] original, ICollection<ITaskItem> conflicts)
        {
            if (conflicts.Count == 0)
            {
                return original;
            }

            var result = new ITaskItem[original.Length - conflicts.Count];
            int index = 0;

            foreach(var originalItem in original)
            {
                if (!conflicts.Contains(originalItem))
                {
                    if (index >= result.Length)
                    {
                        throw new ArgumentException($"Items from {nameof(conflicts)} were missing from {nameof(original)}");
                    }
                    result[index++] = originalItem;
                }
            }

            return result;
        }
    }
}
