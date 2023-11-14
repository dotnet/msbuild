// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Forward a list of items from input to output. This allows dynamic item lists.
    /// </summary>
    public class CreateItem : TaskExtension
    {
        #region Properties

        [Output]
        public ITaskItem[] Include { get; set; }

        public ITaskItem[] Exclude { get; set; }

        /// <summary>
        /// Only apply the additional metadata is none already exists
        /// </summary>
        public bool PreserveExistingMetadata { get; set; } = false;

        /// <summary>
        /// A list of metadata name/value pairs to apply to the output items.
        /// A typical input: "metadataname1=metadatavalue1", "metadataname2=metadatavalue2", ...
        /// </summary>
        /// <remarks>
        ///   <format type="text/markdown"><![CDATA[
        ///     ## Remarks
        ///     The fact that this is a `string[]` makes the following illegal:
        ///         `<CreateItem AdditionalMetadata="TargetPath=@(OutputPathItem)" />`
        ///     The engine fails on this because it doesn't like item lists being concatenated with string
        ///     constants when the data is being passed into an array parameter.  So the workaround is to
        ///     write this in the project file:
        ///         `<CreateItem AdditionalMetadata="@(OutputPathItem-&gt;'TargetPath=%(Identity)')" />`
        ///     ]]>
        ///   </format>
        /// </remarks>
        public string[] AdditionalMetadata { get; set; }

        #endregion

        #region ITask Members
        /// <summary>
        /// Execute.
        /// </summary>
        public override bool Execute()
        {
            if (Include == null)
            {
                Include = Array.Empty<ITaskItem>();
                return true;
            }

            // Expand wild cards.
            (Include, bool expandedInclude) = TryExpandingWildcards(Include, XMakeAttributes.include);
            (Exclude, bool expandedExclude) = TryExpandingWildcards(Exclude, XMakeAttributes.exclude);

            // Execution stops if wildcard expansion fails due to drive enumeration and related env var is set.
            if (!(expandedInclude && expandedExclude))
            {
                return false;
            }

            // Simple case:  no additional attribute to add and no Exclude.  In this case the
            // ouptuts are simply the inputs.
            if (AdditionalMetadata == null && Exclude == null)
            {
                return true;
            }

            // Parse the global properties into a hashtable.
            if (!PropertyParser.GetTable(Log, "AdditionalMetadata", AdditionalMetadata, out Dictionary<string, string> metadataTable))
            {
                return false;
            }

            // Build a table of unique items.
            Dictionary<string, string> excludeItems = GetUniqueItems(Exclude);

            // Produce the output items, add attribute and honor exclude.
            List<ITaskItem> outputItems = CreateOutputItems(metadataTable, excludeItems);

            Include = outputItems.ToArray();

            return !Log.HasLoggedErrors;
        }

        /// <summary>
        /// Create the list of output items.
        /// </summary>
        private List<ITaskItem> CreateOutputItems(Dictionary<string, string> metadataTable, Dictionary<string, string> excludeItems)
        {
            var outputItems = new List<ITaskItem>();

            foreach (ITaskItem i in Include)
            {
                if (
                    (excludeItems.Count == 0) ||        // minor perf optimization
                    (!excludeItems.ContainsKey(i.ItemSpec)))
                {
                    ITaskItem newItem = i;
                    if (metadataTable != null)
                    {
                        foreach (KeyValuePair<string, string> nameAndValue in metadataTable)
                        {
                            // 1. If we have been asked to not preserve existing metadata then overwrite
                            // 2. If there is no existing metadata then apply the new
                            if ((!PreserveExistingMetadata) || String.IsNullOrEmpty(newItem.GetMetadata(nameAndValue.Key)))
                            {
                                if (FileUtilities.ItemSpecModifiers.IsItemSpecModifier(nameAndValue.Key))
                                {
                                    // Explicitly setting built-in metadata, is not allowed.
                                    Log.LogErrorWithCodeFromResources("CreateItem.AdditionalMetadataError", nameAndValue.Key);
                                    break;
                                }

                                newItem.SetMetadata(nameAndValue.Key, nameAndValue.Value);
                            }
                        }
                    }
                    outputItems.Add(newItem);
                }
            }
            return outputItems;
        }

        /// <summary>
        /// Attempts to expand wildcards and logs warnings or errors for attempted drive enumeration.
        /// </summary>
        private (ITaskItem[] Element, bool NoLoggedErrors) TryExpandingWildcards(ITaskItem[] expand, string attributeType)
        {
            const string CreateItemTask = nameof(CreateItem);

            string fileSpec;
            FileMatcher.SearchAction searchAction;

            (expand, searchAction, fileSpec) = ExpandWildcards(expand);

            // Log potential drive enumeration glob anomalies when applicable.
            if (searchAction == FileMatcher.SearchAction.LogDriveEnumeratingWildcard)
            {
                Log.LogWarningWithCodeFromResources(
                    "WildcardResultsInDriveEnumeration",
                    EscapingUtilities.UnescapeAll(fileSpec),
                    attributeType,
                    CreateItemTask,
                    BuildEngine.ProjectFileOfTaskNode);
            }
            else if (searchAction == FileMatcher.SearchAction.FailOnDriveEnumeratingWildcard)
            {
                Log.LogErrorWithCodeFromResources(
                    "WildcardResultsInDriveEnumeration",
                    EscapingUtilities.UnescapeAll(fileSpec),
                    attributeType,
                    CreateItemTask,
                    BuildEngine.ProjectFileOfTaskNode);
            }

            return (expand, !Log.HasLoggedErrors);
        }

        /// <summary>
        /// Expand wildcards in the item list.
        /// </summary>
        private static (ITaskItem[] Element, FileMatcher.SearchAction Action, string FileSpec) ExpandWildcards(ITaskItem[] expand)
        {
            // Used to detect and log drive enumerating wildcard patterns.
            string[] files;
            FileMatcher.SearchAction action = FileMatcher.SearchAction.None;
            string itemSpec = string.Empty;

            if (expand == null)
            {
                return (null, action, itemSpec);
            }
            else
            {
                var expanded = new List<ITaskItem>();
                foreach (ITaskItem i in expand)
                {
                    if (FileMatcher.HasWildcards(i.ItemSpec))
                    {
                        (files, action, _) = FileMatcher.Default.GetFiles(null /* use current directory */, i.ItemSpec);
                        itemSpec = i.ItemSpec;
                        if (action == FileMatcher.SearchAction.FailOnDriveEnumeratingWildcard)
                        {
                            return (expanded.ToArray(), action, itemSpec);
                        }

                        foreach (string file in files)
                        {
                            TaskItem newItem = new TaskItem(i) { ItemSpec = file };

                            // Compute the RecursiveDir portion.
                            FileMatcher.Result match = FileMatcher.Default.FileMatch(i.ItemSpec, file);
                            if (match.isLegalFileSpec && match.isMatch)
                            {
                                if (!string.IsNullOrEmpty(match.wildcardDirectoryPart))
                                {
                                    newItem.SetMetadata(FileUtilities.ItemSpecModifiers.RecursiveDir, match.wildcardDirectoryPart);
                                }
                            }

                            expanded.Add(newItem);
                        }
                    }
                    else
                    {
                        expanded.Add(i);
                    }
                }
                return (expanded.ToArray(), action, itemSpec);
            }
        }

        /// <summary>
        /// Create a table of unique items
        /// </summary>
        private static Dictionary<string, string> GetUniqueItems(ITaskItem[] items)
        {
            var uniqueItems = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (items != null)
            {
                foreach (ITaskItem item in items)
                {
                    uniqueItems[item.ItemSpec] = String.Empty;
                }
            }
            return uniqueItems;
        }

        #endregion
    }
}
