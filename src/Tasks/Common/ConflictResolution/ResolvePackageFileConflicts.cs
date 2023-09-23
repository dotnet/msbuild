// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

//Microsoft.NET.Build.Extensions.Tasks (net7.0) has nullables disabled
#pragma warning disable IDE0240 // Remove redundant nullable directive
#nullable disable
#pragma warning restore IDE0240 // Remove redundant nullable directive

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Build.Tasks.ConflictResolution
{
    public class ResolvePackageFileConflicts : TaskWithAssemblyResolveHooks
    {
        private HashSet<ITaskItem> referenceConflicts = new();
        private HashSet<ITaskItem> analyzerConflicts = new();
        private HashSet<ITaskItem> copyLocalConflicts = new();
        private HashSet<ConflictItem> compilePlatformWinners = new();
        private HashSet<ConflictItem> allConflicts = new();

        public ITaskItem[] References { get; set; }

        public ITaskItem[] Analyzers { get; set; }

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
        /// This is an optimization so AssemblyVersions, FileVersions, etc. don't need to be read
        /// in the default cases where platform packages (Microsoft.NETCore.App) should override specific packages
        /// (System.Console v4.3.0).
        /// </remarks>
        public ITaskItem[] PackageOverrides { get; set; }

        [Output]
        public ITaskItem[] ReferencesWithoutConflicts { get; set; }

        [Output]
        public ITaskItem[] AnalyzersWithoutConflicts { get; set; }


        [Output]
        public ITaskItem[] ReferenceCopyLocalPathsWithoutConflicts { get; set; }

        [Output]
        public ITaskItem[] Conflicts { get; set; }

        protected override void ExecuteCore()
        {
            var log = Log;
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
                }).ToArray();
            }

            // resolve conflicts at compile time
            var referenceItems = GetConflictTaskItems(References, ConflictItemType.Reference).ToArray();

            using (var compileConflictScope = new ConflictResolver<ConflictItem>(packageRanks, packageOverrides, log))
            {
                compileConflictScope.ResolveConflicts(referenceItems,
                    ci => ItemUtilities.GetReferenceFileName(ci.OriginalItem),
                    HandleCompileConflict);

                if (compilePlatformItems != null)
                {
                    compileConflictScope.ResolveConflicts(compilePlatformItems,
                        ci => ci.FileName,
                        HandleCompileConflict);
                }
            }

            //  Remove platform items which won a conflict with a reference but subsequently lost to something else
            compilePlatformWinners.ExceptWith(allConflicts);

            // resolve analyzer conflicts
            var analyzerItems = GetConflictTaskItems(Analyzers, ConflictItemType.Analyzer).ToArray();

            using (var analyzerConflictScope = new ConflictResolver<ConflictItem>(packageRanks, packageOverrides, log))
            {
                analyzerConflictScope.ResolveConflicts(analyzerItems, ci => ci.FileName, HandleAnalyzerConflict);
            }

            // resolve conflicts that clash in output
            IEnumerable<ConflictItem> copyLocalItems;
            IEnumerable<ConflictItem> otherRuntimeItems;
            using (var runtimeConflictScope = new ConflictResolver<ConflictItem>(packageRanks, packageOverrides, log))
            {
                runtimeConflictScope.ResolveConflicts(referenceItems,
                    ci => ItemUtilities.GetReferenceTargetPath(ci.OriginalItem),
                    HandleRuntimeConflict);

                copyLocalItems = GetConflictTaskItems(ReferenceCopyLocalPaths, ConflictItemType.CopyLocal).ToArray();

                runtimeConflictScope.ResolveConflicts(copyLocalItems,
                    ci => ItemUtilities.GetTargetPath(ci.OriginalItem),
                    HandleRuntimeConflict);

                otherRuntimeItems = GetConflictTaskItems(OtherRuntimeItems, ConflictItemType.Runtime).ToArray();

                runtimeConflictScope.ResolveConflicts(otherRuntimeItems,
                    ci => ItemUtilities.GetTargetPath(ci.OriginalItem),
                    HandleRuntimeConflict);
            }


            // resolve conflicts with platform (eg: shared framework) items
            // we only commit the platform items since its not a conflict if other items share the same filename.
            using (var platformConflictScope = new ConflictResolver<ConflictItem>(packageRanks, packageOverrides, log))
            {
                var platformItems = PlatformManifests?.SelectMany(pm => PlatformManifestReader.LoadConflictItems(pm.ItemSpec, log)) ?? Enumerable.Empty<ConflictItem>();

                if (compilePlatformItems != null)
                {
                    platformItems = platformItems.Concat(compilePlatformItems);
                }

                platformConflictScope.ResolveConflicts(platformItems, pi => pi.FileName, (winner, loser) => { });
                platformConflictScope.ResolveConflicts(referenceItems.Where(ri => !referenceConflicts.Contains(ri.OriginalItem)),
                                                       ri => ItemUtilities.GetReferenceTargetFileName(ri.OriginalItem),
                                                       HandleRuntimeConflict,
                                                       commitWinner: false);
                platformConflictScope.ResolveConflicts(copyLocalItems.Where(ci => !copyLocalConflicts.Contains(ci.OriginalItem)),
                                                       ri => ri.FileName,
                                                       HandleRuntimeConflict,
                                                       commitWinner: false);
                platformConflictScope.ResolveConflicts(otherRuntimeItems,
                                                       ri => ri.FileName,
                                                       HandleRuntimeConflict,
                                                       commitWinner: false);
            }

            ReferencesWithoutConflicts = RemoveConflicts(References, referenceConflicts);
            AnalyzersWithoutConflicts = RemoveConflicts(Analyzers, analyzerConflicts);
            ReferenceCopyLocalPathsWithoutConflicts = RemoveConflicts(ReferenceCopyLocalPaths, copyLocalConflicts);
            Conflicts = CreateConflictTaskItems(allConflicts);

            //  This handles the issue described here: https://github.com/dotnet/sdk/issues/2221
            //  The issue is that before conflict resolution runs, references to assemblies in the framework
            //  that also match an assembly coming from a NuGet package are either removed (in non-SDK targets
            //  via the ResolveNuGetPackageAssets target in Microsoft.NuGet.targets) or transformed to refer
            //  to the assembly coming from the NuGet package (for SDK projects in the ResolveLockFileReferences
            //  task).
            //  In either case, there ends up being no Reference item which will resolve to the DLL in
            //  the reference assemblies.  This is a problem if the platform item from the reference
            //  assemblies wins a conflict in the compile scope, as the reference to the assembly from
            //  the NuGet package will be removed, but there will be no reference to the Framework assembly
            //  passed to the compiler.
            //  So what we do is keep track of Platform items that win conflicts with Reference items in
            //  the compile scope, and explicitly add references to them here.

            var referenceItemSpecs = new HashSet<string>(ReferencesWithoutConflicts?.Select(r => r.ItemSpec) ?? Enumerable.Empty<string>(),
                                                                     StringComparer.OrdinalIgnoreCase);
            ReferencesWithoutConflicts = SafeConcat(ReferencesWithoutConflicts,
                //  The Reference item we create in this case should be without the .dll extension
                //  (which is added in FrameworkListReader in order to make the framework items
                //  correctly conflict with DLLs from NuGet packages)
                compilePlatformWinners.Select(c => Path.GetFileNameWithoutExtension(c.FileName))
                                      //  Don't add a reference if we already have one (especially in case the existing one has
                                      //  metadata we want to keep, such as aliases)
                                      .Where(simplename => !referenceItemSpecs.Contains(simplename))
                                      .Select(r => new TaskItem(r)));

        }

        //  Concatenate two things, either of which may be null.  Interpret null as empty,
        //  and return null if the result would be empty.
        private ITaskItem[] SafeConcat(ITaskItem[] first, IEnumerable<ITaskItem> second)
        {
            if (first == null || first.Length == 0)
            {
                return second?.ToArray();
            }

            if (second == null || !second.Any())
            {
                return first;
            }

            return first.Concat(second).ToArray();
        }

        private ITaskItem[] CreateConflictTaskItems(ICollection<ConflictItem> conflicts)
        {
            var conflictItems = new ITaskItem[conflicts.Count];

            int i = 0;
            foreach (var conflict in conflicts)
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
                item.SetMetadata(MetadataKeys.NuGetPackageId, conflict.PackageId);
            }

            return item;
        }

        private IEnumerable<ConflictItem> GetConflictTaskItems(ITaskItem[] items, ConflictItemType itemType)
        {
            return (items != null) ? items.Select(i => new ConflictItem(i, itemType)) : Enumerable.Empty<ConflictItem>();
        }

        private void HandleCompileConflict(ConflictItem winner, ConflictItem loser)
        {
            if (loser.ItemType == ConflictItemType.Reference)
            {
                referenceConflicts.Add(loser.OriginalItem);

                if (winner.ItemType == ConflictItemType.Platform)
                {
                    compilePlatformWinners.Add(winner);
                }
            }
            allConflicts.Add(loser);
        }

        private void HandleAnalyzerConflict(ConflictItem winner, ConflictItem loser)
        {
            analyzerConflicts.Add(loser.OriginalItem);
            allConflicts.Add(loser);
        }

        private void HandleRuntimeConflict(ConflictItem winner, ConflictItem loser)
        {
            if (loser.ItemType == ConflictItemType.Reference)
            {
                loser.OriginalItem.SetMetadata(MetadataNames.Private, "False");
            }
            else if (loser.ItemType == ConflictItemType.CopyLocal)
            {
                copyLocalConflicts.Add(loser.OriginalItem);
            }
            allConflicts.Add(loser);
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

            foreach (var originalItem in original)
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

            //  If there are duplicates in the original list, then our size calculation for the result will have been wrong.
            //  So we have to re-allocate the array with the right size.
            //  Duplicates can happen if there are duplicate Reference items that are joined with a reference from a package in ResolveLockFileReferences
            if (index != result.Length)
            {
                return result.Take(index).ToArray();
            }

            return result;
        }
    }
}
