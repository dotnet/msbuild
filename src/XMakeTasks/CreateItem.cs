// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Build.Shared;
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Resources;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Forward a list of items from input to output. This allows dynamic item lists.
    /// </summary>
    public class CreateItem : TaskExtension
    {
        #region Properties

        private ITaskItem[] _include;
        private ITaskItem[] _exclude;
        private string[] _additionalMetadata;
        private bool _preserveExistingMetadata = false;

        [Output]
        public ITaskItem[] Include
        {
            get
            {
                return _include;
            }

            set
            {
                _include = value;
            }
        }

        public ITaskItem[] Exclude
        {
            get
            {
                return _exclude;
            }

            set
            {
                _exclude = value;
            }
        }

        /// <summary>
        /// Only apply the additional metadata is none already exists
        /// </summary>
        public bool PreserveExistingMetadata
        {
            get
            {
                return _preserveExistingMetadata;
            }

            set
            {
                _preserveExistingMetadata = value;
            }
        }

        /// <summary>
        /// A list of metadata name/value pairs to apply to the output items.  
        /// A typical input: "metadataname1=metadatavalue1", "metadataname2=metadatavalue2", ...
        /// </summary>
        /// <remarks>
        /// The fact that this is a string[] makes the following illegal:
        ///     <CreateItem
        ///         AdditionalMetadata="TargetPath=@(OutputPathItem)" />
        /// The engine fails on this because it doesn't like item lists being concatenated with string
        /// constants when the data is being passed into an array parameter.  So the workaround is to 
        /// write this in the project file:
        ///     <CreateItem
        ///         AdditionalMetadata="@(OutputPathItem->'TargetPath=%(Identity)')" />
        /// </remarks>
        public string[] AdditionalMetadata
        {
            get
            {
                return _additionalMetadata;
            }
            set
            {
                _additionalMetadata = value;
            }
        }

        #endregion

        #region ITask Members
        /// <summary>
        /// Execute.
        /// </summary>
        public override bool Execute()
        {
            if (Include == null)
            {
                _include = new TaskItem[0];
                return true;
            }

            // Expand wild cards.
            Include = ExpandWildcards(Include);
            Exclude = ExpandWildcards(Exclude);

            // Simple case:  no additional attribute to add and no Exclude.  In this case the
            // ouptuts are simply the inputs.
            if (AdditionalMetadata == null && Exclude == null)
            {
                return true;
            }

            // Parse the global properties into a hashtable.
            Hashtable metadataTable;
            if (!PropertyParser.GetTable(Log, "AdditionalMetadata", this.AdditionalMetadata, out metadataTable))
            {
                return false;
            }


            // Build a table of unique items.
            Hashtable excludeItems = GetUniqueItems(Exclude);

            // Produce the output items, add attribute and honor exclude.
            ArrayList outputItems = CreateOutputItems(metadataTable, excludeItems);

            _include = (ITaskItem[])outputItems.ToArray(typeof(ITaskItem));

            return !Log.HasLoggedErrors;
        }

        /// <summary>
        /// Create the list of output items.
        /// </summary>
        /// <param name="needToSetAttributes">Whether attributes need to be set.</param>
        /// <param name="excludeItems">Items to exclude.</param>
        private ArrayList CreateOutputItems(Hashtable metadataTable, Hashtable excludeItems)
        {
            ArrayList outputItems = new ArrayList();

            for (int i = 0; i < Include.Length; i++)
            {
                if (
                    (excludeItems.Count == 0) ||        // minor perf optimization
                    (!excludeItems.ContainsKey(Include[i].ItemSpec))
                   )
                {
                    ITaskItem newItem = _include[i];
                    if (null != metadataTable)
                    {
                        foreach (DictionaryEntry nameAndValue in metadataTable)
                        {
                            // 1. If we have been asked to not preserve existing metadata then overwrite
                            // 2. If there is no existing metadata then apply the new
                            if ((!_preserveExistingMetadata) || String.IsNullOrEmpty(newItem.GetMetadata((string)nameAndValue.Key)))
                            {
                                if (FileUtilities.ItemSpecModifiers.IsItemSpecModifier((string)nameAndValue.Key))
                                {
                                    // Explicitly setting built-in metadata, is not allowed. 
                                    Log.LogErrorWithCodeFromResources("CreateItem.AdditionalMetadataError", (string)nameAndValue.Key);
                                    break;
                                }

                                newItem.SetMetadata((string)nameAndValue.Key, (string)nameAndValue.Value);
                            }
                        }
                    }
                    outputItems.Add(newItem);
                }
            }
            return outputItems;
        }

        /// <summary>
        /// Expand wildcards in the item list.
        /// </summary>
        /// <param name="expand"></param>
        /// <returns></returns>
        private static ITaskItem[] ExpandWildcards(ITaskItem[] expand)
        {
            if (expand == null)
            {
                return null;
            }
            else
            {
                ArrayList expanded = new ArrayList();
                foreach (ITaskItem i in expand)
                {
                    if (FileMatcher.HasWildcards(i.ItemSpec))
                    {
                        string[] files = FileMatcher.GetFiles(null /* use current directory */, i.ItemSpec);
                        foreach (string file in files)
                        {
                            TaskItem newItem = new TaskItem((ITaskItem)i);
                            newItem.ItemSpec = file;

                            // Compute the RecursiveDir portion.
                            FileMatcher.Result match = FileMatcher.FileMatch(i.ItemSpec, file);
                            if (match.isLegalFileSpec && match.isMatch)
                            {
                                if (match.wildcardDirectoryPart != null && match.wildcardDirectoryPart.Length > 0)
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
                return (ITaskItem[])expanded.ToArray(typeof(ITaskItem));
            }
        }

        /// <summary>
        /// Create a table of unique items
        /// </summary>
        /// <returns></returns>
        private static Hashtable GetUniqueItems(ITaskItem[] items)
        {
            Hashtable uniqueItems = new Hashtable(StringComparer.OrdinalIgnoreCase);

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
