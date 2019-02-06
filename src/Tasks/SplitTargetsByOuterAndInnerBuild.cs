// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Experimental.Tasks
{
    /// <summary>
    ///     Task to separate entry targets for outer builds and inner builds
    /// </summary>
    public sealed class SplitTargetsByOuterAndInnerBuild : TaskExtension
    {
        private readonly struct EntryTargetInfo
        {
            public ITaskItem TaskItem { get; }

            public EntryTargetInfo(ITaskItem taskItem)
            {
                TaskItem = taskItem;
                IsInnerBuild = false;
                FoundInProjectReferenceTargets = false;
            }

            private EntryTargetInfo(ITaskItem taskItem, bool isInnerBuild, bool found)
            {
                TaskItem = taskItem;
                IsInnerBuild = isInnerBuild;
                FoundInProjectReferenceTargets = found;
            }

            public bool FoundInProjectReferenceTargets { get; }

            public bool IsInnerBuild { get; }

            public EntryTargetInfo AsFound(bool isInnerBuild)
            {
                return new EntryTargetInfo(TaskItem, isInnerBuild, true);
            }
        }

        private static readonly char[] SemicolonArray = {';'};
        public ITaskItem[] EntryTargets { get; set; }
        public ITaskItem[] ProjectReferenceTargets { get; set; }
        public ITaskItem[] DefaultTargets { get; set; }

        [Output]
        public ITaskItem[] EntryTargetsForOuterBuild { get; set; }

        [Output]
        public ITaskItem[] EntryTargetsForInnerBuilds { get; set; }

        public override bool Execute()
        {
            try
            {
                ErrorUtilities.VerifyThrowArgumentLength(EntryTargets, nameof(EntryTargets));
                ErrorUtilities.VerifyThrowArgumentLength(ProjectReferenceTargets, nameof(ProjectReferenceTargets));
                ErrorUtilities.VerifyThrowArgumentNull(DefaultTargets, nameof(DefaultTargets));

                var defaultTargets = DefaultTargets.Select(d => d.ItemSpec).ToList();
                var entryTargets = EntryTargets.ToDictionary(k => k.ItemSpec, v => new EntryTargetInfo(v));

                foreach (var projectReferenceTarget in ProjectReferenceTargets)
                {
                    var metadata = projectReferenceTarget.CloneCustomMetadata();

                    var isInnerBuild = metadata.Contains(ItemMetadataNames.ProjectReferenceTargetsInnerBuild) && ((string) metadata[ItemMetadataNames.ProjectReferenceTargetsInnerBuild]).Equals("true", StringComparison.OrdinalIgnoreCase);

                    if (!metadata.Contains(ItemMetadataNames.ProjectReferenceTargetsMetadataName))
                    {
                        throw new ArgumentException(ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("ProjectReferenceTargetsMustContainTargets",
                            ItemTypeNames.ProjectReferenceTargetsItemType,
                            ItemMetadataNames.ProjectReferenceTargetsMetadataName));
                    }

                    var specificationTargets = ComputeSpecificationTargets(
                        (string) metadata[ItemMetadataNames.ProjectReferenceTargetsMetadataName],
                        defaultTargets);

                    foreach (var specificationTarget in specificationTargets)
                    {
                        if (entryTargets.ContainsKey(specificationTarget))
                        {
                            var entryTargetInfo = entryTargets[specificationTarget];

                            if (entryTargetInfo.FoundInProjectReferenceTargets && entryTargetInfo.IsInnerBuild != isInnerBuild)
                            {
                                throw new ArgumentException(
                                    ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword(
                                        "MismatchedTargetMetadata",
                                        entryTargetInfo.TaskItem.ItemSpec,
                                        ItemTypeNames.ProjectReferenceTargetsItemType,
                                        ItemMetadataNames.ProjectReferenceTargetsInnerBuild)
                                    );
                            }
                            else
                            {
                                entryTargets[specificationTarget] = entryTargetInfo.AsFound(isInnerBuild);
                            }
                        }
                    }
                }

                CheckAllEntryTargetsAreAccountedFor(entryTargets);

                EntryTargetsForInnerBuilds = entryTargets.Values.Where(v => v.IsInnerBuild).Select(t => new TaskItem(t.TaskItem)).ToArray();
                EntryTargetsForOuterBuild = entryTargets.Values.Where(v => v.IsInnerBuild == false).Select(t => new TaskItem(t.TaskItem)).ToArray();
            }
            catch (ArgumentException e)
            {
                Log.LogErrorFromException(e);
            }

            return !Log.HasLoggedErrors;
        }

        private static IEnumerable<string> ComputeSpecificationTargets(string targetSpecification, List<string> defaultTargets)
        {
            var specificationTargets = SplitBySemicolon(targetSpecification);

            return ExpandDefaultTargets(specificationTargets, defaultTargets);
        }

        private void CheckAllEntryTargetsAreAccountedFor(Dictionary<string, EntryTargetInfo> entryTargets)
        {
            foreach (var entryTargetInfo in entryTargets.Values)
            {
                if (!entryTargetInfo.FoundInProjectReferenceTargets)
                {
                    Log.LogErrorFromResources("EntryTargetNotFoundInProjectReferenceTargets", entryTargetInfo.TaskItem.ItemSpec, ItemTypeNames.ProjectReferenceTargetsItemType);
                }
            }
        }

        private static ImmutableList<string> SplitBySemicolon(string aString)
        {
            return aString.Split(SemicolonArray, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToImmutableList();
        }

        /// <summary>
        /// Code duplication with Microsoft.Build.Experimental.Graph.ProjectGraph.ExpandDefaultTargets
        /// </summary>
        private static ImmutableList<string> ExpandDefaultTargets(ImmutableList<string> targets, List<string> defaultTargets)
        {
            int i = 0;
            while (i < targets.Count)
            {
                if (targets[i].Equals(MSBuildConstants.DefaultTargetsMarker, StringComparison.OrdinalIgnoreCase))
                {
                    targets = targets
                        .RemoveAt(i)
                        .InsertRange(i, defaultTargets);
                    i += defaultTargets.Count;
                }
                else
                {
                    i++;
                }
            }

            return targets;
        }
    }
}
