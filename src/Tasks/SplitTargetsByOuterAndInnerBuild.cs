// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks
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
                
                var entryTargets = EntryTargets.ToDictionary(k => k.ItemSpec, v => new EntryTargetInfo(v));

                foreach (var projectReferenceTarget in ProjectReferenceTargets)
                {
                    var metadata = projectReferenceTarget.CloneCustomMetadata();

                    var isInnerBuild = metadata.Contains("InnerBuild") && ((string) metadata["InnerBuild"]).Equals("true", StringComparison.OrdinalIgnoreCase);

                    ErrorUtilities.VerifyThrowArgument(
                        metadata.Contains(ItemMetadataNames.ProjectReferenceTargetsMetadataName),
                        "PRT spec must contain Targets");

                    var specificationTargets = SplitBySemicolon((string) metadata[ItemMetadataNames.ProjectReferenceTargetsMetadataName]);

                    foreach (var specificationTarget in specificationTargets)
                    {
                        if (entryTargets.ContainsKey(specificationTarget))
                        {
                            var entryTargetInfo = entryTargets[specificationTarget];

                            if (entryTargetInfo.FoundInProjectReferenceTargets)
                            {
                                ErrorUtilities.VerifyThrowArgument(entryTargetInfo.IsInnerBuild == isInnerBuild, "all spec targets must agree on whether a target inner build or not");
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

        private void CheckAllEntryTargetsAreAccountedFor(Dictionary<string, EntryTargetInfo> entryTargets)
        {
            foreach (var entryTargetInfo in entryTargets.Values)
            {
                if (!entryTargetInfo.FoundInProjectReferenceTargets)
                {
                    Log.LogError("Entry target {0} not found in {1} item", entryTargetInfo.TaskItem.ItemSpec, ItemTypeNames.ProjectReferenceTargetsItemType);
                }
            }
        }

        private static string[] SplitBySemicolon(string aString)
        {
            return aString.Split(SemicolonArray, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray();
        }
    }
}
