// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Resources;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Finds the app.config file, if any, in the provided lists.
    /// For compat reasons, it has to follow a particular arbitrary algorithm.
    /// It also adds the TargetPath metadata.
    /// </summary>
    public class FindAppConfigFile : TaskExtension
    {
        // The list to search through
        private ITaskItem[] _primaryList;
        private ITaskItem[] _secondaryList;

        // The target path metadata value to add to the found item
        private string _targetPath;

        // The item found, if any
        private ITaskItem _appConfigFileFound = null;

        // What we're looking for
        private const string appConfigFile = "app.config";

        /// <summary>
        /// The primary list to search through
        /// </summary>
        [Required]
        public ITaskItem[] PrimaryList
        {
            get
            {
                ErrorUtilities.VerifyThrowArgumentNull(_primaryList, "primaryList");
                return _primaryList;
            }
            set { _primaryList = value; }
        }

        /// <summary>
        /// The secondary list to search through
        /// </summary>
        [Required]
        public ITaskItem[] SecondaryList
        {
            get
            {
                ErrorUtilities.VerifyThrowArgumentNull(_secondaryList, "secondaryList");
                return _secondaryList;
            }
            set { _secondaryList = value; }
        }

        /// <summary>
        /// The value to add as TargetPath metadata
        /// </summary>
        [Required]
        public string TargetPath
        {
            get { return _targetPath; }
            set { _targetPath = value; }
        }

        /// <summary>
        /// The first matching item found in the list, if any
        /// </summary>
        [Output]
        public ITaskItem AppConfigFile
        {
            get { return _appConfigFileFound; }
            set { _appConfigFileFound = value; }
        }

        /// <summary>
        /// Find the app config
        /// </summary>
        public override bool Execute()
        {
            // Look at the whole item spec first -- ie,
            // we want to prefer app.config files that are directly in the project folder.
            if (ConsultLists(true))
            {
                return true;
            }

            // If that fails, fall back to app.config files anywhere in the project cone.
            if (ConsultLists(false))
            {
                return true;
            }

            // Not found
            return true;
        }

        private bool ConsultLists(bool matchWholeItemSpec)
        {
            // Look at primary list first, then secondary list
            // We walk backwards on the list to find the last match (for historical reasons)
            for (int i = PrimaryList.Length - 1; i >= 0; i--)
            {
                if (IsMatchingItem(PrimaryList[i], matchWholeItemSpec))
                {
                    return true;
                }
            }

            for (int i = SecondaryList.Length - 1; i >= 0; i--)
            {
                if (IsMatchingItem(SecondaryList[i], matchWholeItemSpec))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Examines the item to see if it matches what we are looking for.
        /// If it does, returns true.
        /// </summary>
        private bool IsMatchingItem(ITaskItem item, bool matchWholeItemSpec)
        {
            string filename;
            try
            {
                filename = (matchWholeItemSpec ? item.ItemSpec : Path.GetFileName(item.ItemSpec));

                if (String.Equals(filename, appConfigFile, StringComparison.OrdinalIgnoreCase))
                {
                    _appConfigFileFound = item;

                    // Originally the app.config was found in such a way that it's "OriginalItemSpec"
                    // metadata was cleared out. Although it doesn't really matter, for compatibility,
                    // we'll clear it out here.
                    _appConfigFileFound.SetMetadata("OriginalItemSpec", item.ItemSpec);

                    _appConfigFileFound.SetMetadata(ItemMetadataNames.targetPath, TargetPath);

                    Log.LogMessageFromResources(MessageImportance.Low, "FindInList.Found", _appConfigFileFound.ItemSpec);
                    return true;
                }
            }
            catch (ArgumentException ex)
            {
                // Just log this: presumably this item spec is not intended to be
                // a file path
                Log.LogMessageFromResources(MessageImportance.Low, "FindInList.InvalidPath", item.ItemSpec, ex.Message);
            }
            return false;
        }
    }
}
