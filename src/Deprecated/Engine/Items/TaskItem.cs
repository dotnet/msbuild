// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Security;

using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// This class wraps a project item, and provides a "view" on the item's BuildItem class that is suitable to expose to tasks.
    /// </summary>
    /// <owner>SumedhK</owner>
    internal sealed class TaskItem : MarshalByRefObject, ITaskItem
    {
        /// <summary>
        /// Private default constructor disallows parameterless instantiation.
        /// </summary>
        /// <owner>SumedhK</owner>
        private TaskItem()
        {
            // do nothing
        }

        /// <summary>
        /// Creates an instance of this class given the item-spec.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="itemSpec"></param>
        internal TaskItem(string itemSpec)
        {
            ErrorUtilities.VerifyThrow(itemSpec != null, "Need to specify item-spec.");

            item = new BuildItem(null, itemSpec);
        }

        /// <summary>
        /// Creates an instance of this class given the backing item.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="item"></param>
        internal TaskItem(BuildItem item)
        {
            ErrorUtilities.VerifyThrow(item != null, "Need to specify backing item.");

            this.item = item.VirtualClone();
        }

        /// <summary>
        /// Gets or sets the item-spec for the item.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <value>Item-spec string.</value>
        public string ItemSpec
        {
            get
            {
                return item.FinalItemSpec;
            }

            set
            {
                ErrorUtilities.VerifyThrowArgumentNull(value, "ItemSpec");
                item.SetFinalItemSpecEscaped(EscapingUtilities.Escape(value));
            }
        }

        /// <summary>
        /// Gets the names of metadata on the item -- also includes the pre-defined/reserved item-spec modifiers.
        /// </summary>
        /// <owner>SumedhK, JomoF</owner>
        /// <value>Collection of name strings.</value>
        public ICollection MetadataNames
        {
            get
            {
                // Add all the custom metadata.
                return item.MetadataNames;
            }
        }

        /// <summary>
        /// Gets the number of metadata set on the item.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <value>Count of metadata.</value>
        public int MetadataCount
        {
            get
            {
                return item.MetadataCount;
            }
        }

        /// <summary>
        /// Gets the names of custom metadata on the item
        /// </summary>
        /// <value>Collection of name strings.</value>
        public ICollection CustomMetadataNames
        {
            get
            {
                // All the custom metadata.
                return item.CustomMetadataNames;
            }
        }

        /// <summary>
        /// Gets the number of custom metadata set on the item.
        /// </summary>
        /// <value>Count of metadata.</value>
        public int CustomMetadataCount
        {
            get
            {
                return item.CustomMetadataCount;
            }
        }
        
        /// <summary>
        /// Looks up the value of the given custom metadata.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="metadataName"></param>
        /// <returns>value of metadata</returns>
        public string GetMetadata(string metadataName)
        {
            ErrorUtilities.VerifyThrowArgumentNull(metadataName, nameof(metadataName));

            // Return the unescaped data to the task.
            return item.GetEvaluatedMetadata(metadataName);
        }

        /// <summary>
        /// Sets the value of the specified custom metadata.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="metadataName"></param>
        /// <param name="metadataValue"></param>
        public void SetMetadata(string metadataName, string metadataValue)
        {
            ErrorUtilities.VerifyThrowArgumentLength(metadataName, nameof(metadataName));
            ErrorUtilities.VerifyThrowArgumentNull(metadataValue, nameof(metadataValue));

            item.SetMetadata(metadataName, EscapingUtilities.Escape(metadataValue));
        }

        /// <summary>
        /// Removes the specified custom metadata.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="metadataName"></param>
        public void RemoveMetadata(string metadataName)
        {
            ErrorUtilities.VerifyThrowArgumentNull(metadataName, nameof(metadataName));

            item.RemoveMetadata(metadataName);
        }

        /// <summary>
        /// Copy the metadata (but not the ItemSpec or other built-in metadata) to destinationItem. If a particular metadata
        /// already exists on the destination item, then it is not overwritten -- the original value wins.
        /// </summary>
        /// <owner>JomoF</owner>
        /// <param name="destinationItem"></param>
        public void CopyMetadataTo
        (
            ITaskItem destinationItem
        )
        {
            ErrorUtilities.VerifyThrowArgumentNull(destinationItem, nameof(destinationItem));

            // Intentionally not _computed_ properties. These are slow and don't really
            // apply anyway.
            foreach (DictionaryEntry entry in item.GetAllCustomEvaluatedMetadata())
            {
                string key = (string)entry.Key;

                string destinationValue = destinationItem.GetMetadata(key);

                if (string.IsNullOrEmpty(destinationValue))
                {
                    destinationItem.SetMetadata(key, EscapingUtilities.UnescapeAll((string)entry.Value));
                }
            }

            // also copy the original item-spec under a "magic" metadata -- this is useful for tasks that forward metadata
            // between items, and need to know the source item where the metadata came from
            string originalItemSpec = destinationItem.GetMetadata("OriginalItemSpec");

            if (string.IsNullOrEmpty(originalItemSpec))
            {
                destinationItem.SetMetadata("OriginalItemSpec", ItemSpec);
            }
        }

        /// <summary>
        /// Get the collection of metadata. This does not include built-in metadata.
        /// </summary>
        /// <remarks>
        /// RECOMMENDED GUIDELINES FOR METHOD IMPLEMENTATIONS:
        /// 1) this method should return a clone of the metadata
        /// 2) writing to this dictionary should not be reflected in the underlying item.
        /// </remarks>
        /// <owner>JomoF</owner>
        public IDictionary CloneCustomMetadata()
        {
            IDictionary backingItemMetadata = item.CloneCustomMetadata(); 

            // Go through and escape the metadata as necessary.
            string[] keys = new string[backingItemMetadata.Count];
            backingItemMetadata.Keys.CopyTo(keys, 0);
            foreach (string singleMetadataName in keys)
            {
                string singleMetadataValue = (string) backingItemMetadata[singleMetadataName];

                bool unescapingWasNecessary;
                string singleMetadataValueUnescaped = EscapingUtilities.UnescapeAll(singleMetadataValue, out unescapingWasNecessary);

                // It's very important for perf not to touch this IDictionary unless we really need to.  Touching
                // it in any way causes it to get cloned (in the implementation of CopyOnWriteHashtable).
                if (unescapingWasNecessary)
                {
                    backingItemMetadata[singleMetadataName] = singleMetadataValueUnescaped;
                }
            }

            return backingItemMetadata;
        }

        /// <summary>
        /// Produce a string representation.
        /// </summary>
        /// <owner>JomoF</owner>
        public override string ToString()
        {
            return ItemSpec;
        }

        /// <summary>
        /// Overriden to give this class infinite lease time. Otherwise we end up with a limited
        /// lease (5 minutes I think) and instances can expire if they take long time processing.
        /// </summary>
        [SecurityCritical]
        public override object InitializeLifetimeService()
        {
            // null means infinite lease time
            return null;
        }

        // the backing item
        internal BuildItem item;
        
        #region Operators

        /// <summary>
        /// This allows an explicit typecast from a "TaskItem" to a "string", returning the ItemSpec for this item.
        /// </summary>
        /// <owner>RGoel</owner>
        /// <param name="taskItemToCast">The item to operate on.</param>
        /// <returns>The item-spec of the item.</returns>
        public static explicit operator string
        (
            TaskItem taskItemToCast
        )
        {
            return taskItemToCast.ItemSpec;
        }

        #endregion
    }
}
