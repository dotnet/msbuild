// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security;
#if FEATURE_SECURITY_PERMISSIONS
using System.Security.Permissions;
#endif

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Collections;

namespace Microsoft.Build.Utilities
{
    /// <summary>
    /// This class represents a single item of the project, as it is passed into a task. TaskItems do not exactly correspond to
    /// item elements in project files, because then tasks would have access to data that wasn't explicitly passed into the task
    /// via the project file. It's not a security issue, but more just an issue with project file clarity and transparency.
    /// 
    /// Note: This class has to be sealed.  It has to be sealed because the engine instantiates it's own copy of this type and
    /// thus if someone were to extend it, they would not get the desired behavior from the engine.  
    /// </summary>
    /// <comment>
    /// Surprisingly few of these Utilities TaskItems are created: typically several orders of magnitude fewer than the number of engine TaskItems.
    /// </comment>
    public sealed class TaskItem :
#if FEATURE_APPDOMAIN
        MarshalByRefObject,
#endif
        ITaskItem, ITaskItem2
    {
        #region Member Data

        // This is the final evaluated item specification.  Stored in escaped form. 
        private string _itemSpec;

        // These are the user-defined metadata on the item, specified in the
        // project file via XML child elements of the item element.  These have
        // no meaning to MSBuild, but tasks may use them.
        // Values are stored in escaped form.
        private CopyOnWriteDictionary<string> _metadata;

        // cache of the fullpath value
        private string _fullPath;

        /// <summary>
        /// May be defined if we're copying this item from a pre-existing one.  Otherwise, 
        /// we simply don't know enough to set it properly, so it will stay null. 
        /// </summary>
        private readonly string _definingProject;

        #endregion

        #region Constructors

        /// <summary>
        /// Default constructor -- we need it so this type is COM-createable.
        /// </summary>
        public TaskItem()
        {
            _itemSpec = string.Empty;
        }

        /// <summary>
        /// This constructor creates a new task item, given the item spec.
        /// </summary>
        /// <comments>Assumes the itemspec passed in is escaped.</comments>
        /// <param name="itemSpec">The item-spec string.</param>
        public TaskItem
        (
            string itemSpec
        )
        {
            ErrorUtilities.VerifyThrowArgumentNull(itemSpec, nameof(itemSpec));

            _itemSpec = FileUtilities.FixFilePath(itemSpec);
        }

        /// <summary>
        /// This constructor creates a new TaskItem, using the given item spec and metadata.
        /// </summary>
        /// <comments>
        /// Assumes the itemspec passed in is escaped, and also that any escapable metadata values
        /// are passed in escaped form.
        /// </comments>
        /// <param name="itemSpec">The item-spec string.</param>
        /// <param name="itemMetadata">Custom metadata on the item.</param>
        public TaskItem
        (
            string itemSpec,
            IDictionary itemMetadata
        ) :
            this(itemSpec)
        {
            ErrorUtilities.VerifyThrowArgumentNull(itemMetadata, nameof(itemMetadata));

            if (itemMetadata.Count > 0)
            {
                _metadata = new CopyOnWriteDictionary<string>(MSBuildNameIgnoreCaseComparer.Default);

                foreach (DictionaryEntry singleMetadata in itemMetadata)
                {
                    // don't import metadata whose names clash with the names of reserved metadata
                    string key = (string)singleMetadata.Key;
                    if (!FileUtilities.ItemSpecModifiers.IsDerivableItemSpecModifier(key))
                    {
                        _metadata[key] = (string)singleMetadata.Value ?? string.Empty;
                    }
                }
            }
        }

        /// <summary>
        /// This constructor creates a new TaskItem, using the given ITaskItem.
        /// </summary>
        /// <param name="sourceItem">The item to copy.</param>
        public TaskItem
        (
            ITaskItem sourceItem
        )
        {
            ErrorUtilities.VerifyThrowArgumentNull(sourceItem, nameof(sourceItem));

            // Attempt to preserve escaped state
            if (!(sourceItem is ITaskItem2 sourceItemAsITaskItem2))
            {
                _itemSpec = EscapingUtilities.Escape(sourceItem.ItemSpec);
                _definingProject = EscapingUtilities.EscapeWithCaching(sourceItem.GetMetadata(FileUtilities.ItemSpecModifiers.DefiningProjectFullPath));
            }
            else
            {
                _itemSpec = sourceItemAsITaskItem2.EvaluatedIncludeEscaped;
                _definingProject = sourceItemAsITaskItem2.GetMetadataValueEscaped(FileUtilities.ItemSpecModifiers.DefiningProjectFullPath);
            }

            sourceItem.CopyMetadataTo(this);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the item-spec.
        /// </summary>
        /// <comments>
        /// This one is a bit tricky.  Orcas assumed that the value being set was escaped, but 
        /// that the value being returned was unescaped.  Maintain that behaviour here.  To get
        /// the escaped value, use ITaskItem2.EvaluatedIncludeEscaped. 
        /// </comments>
        /// <value>The item-spec string.</value>
        public string ItemSpec
        {
            get => _itemSpec == null ? string.Empty : EscapingUtilities.UnescapeAll(_itemSpec);

            set
            {
                ErrorUtilities.VerifyThrowArgumentNull(value, nameof(ItemSpec));

                _itemSpec = FileUtilities.FixFilePath(value);
                _fullPath = null;
            }
        }

        /// <summary>
        /// Gets or sets the escaped include, or "name", for the item.
        /// </summary>
        /// <remarks>
        /// Taking the opportunity to fix the property name, although this doesn't
        /// make it obvious it's an improvement on ItemSpec.
        /// </remarks>
        string ITaskItem2.EvaluatedIncludeEscaped
        {
            // It's already escaped
            get => _itemSpec;

            set
            {
                _itemSpec = FileUtilities.FixFilePath(value);
                _fullPath = null;
            }
        }

        /// <summary>
        /// Gets the names of all the item's metadata.
        /// </summary>
        /// <value>List of metadata names.</value>
        public ICollection MetadataNames
        {
            get
            {
                var metadataNames = new List<string>(_metadata?.Keys ?? Array.Empty<string>());
                metadataNames.AddRange(FileUtilities.ItemSpecModifiers.All);

                return metadataNames;
            }
        }

        /// <summary>
        /// Gets the number of metadata set on the item.
        /// </summary>
        /// <value>Count of metadata.</value>
        public int MetadataCount => (_metadata?.Count ?? 0) + FileUtilities.ItemSpecModifiers.All.Length;

        /// <summary>
        /// Gets the metadata dictionary
        /// Property is required so that we can access the metadata dictionary in an item from 
        /// another appdomain, as the CLR has implemented remoting policies that disallow accessing 
        /// private fields in remoted items. 
        /// </summary>
        private CopyOnWriteDictionary<string> Metadata
        {
            get
            {
                return _metadata;
            }
            set
            {
                _metadata = value;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Removes one of the arbitrary metadata on the item.
        /// </summary>
        /// <param name="metadataName">Name of metadata to remove.</param>
        public void RemoveMetadata(string metadataName)
        {
            ErrorUtilities.VerifyThrowArgumentNull(metadataName, nameof(metadataName));
            ErrorUtilities.VerifyThrowArgument(!FileUtilities.ItemSpecModifiers.IsItemSpecModifier(metadataName),
                "Shared.CannotChangeItemSpecModifiers", metadataName);

            _metadata?.Remove(metadataName);
        }

        /// <summary>
        /// Sets one of the arbitrary metadata on the item.
        /// </summary>
        /// <comments>
        /// Assumes that the value being passed in is in its escaped form. 
        /// </comments>
        /// <param name="metadataName">Name of metadata to set or change.</param>
        /// <param name="metadataValue">Value of metadata.</param>
        public void SetMetadata
        (
            string metadataName,
            string metadataValue
        )
        {
            ErrorUtilities.VerifyThrowArgumentLength(metadataName, nameof(metadataName));

            // Non-derivable metadata can only be set at construction time.
            // That's why this is IsItemSpecModifier and not IsDerivableItemSpecModifier.
            ErrorUtilities.VerifyThrowArgument(!FileUtilities.ItemSpecModifiers.IsDerivableItemSpecModifier(metadataName),
                "Shared.CannotChangeItemSpecModifiers", metadataName);

            _metadata ??= new CopyOnWriteDictionary<string>(MSBuildNameIgnoreCaseComparer.Default);

            _metadata[metadataName] = metadataValue ?? string.Empty;
        }

        /// <summary>
        /// Retrieves one of the arbitrary metadata on the item.
        /// If not found, returns empty string.
        /// </summary>
        /// <comments>
        /// Returns the unescaped value of the metadata requested. 
        /// </comments>
        /// <param name="metadataName">The name of the metadata to retrieve.</param>
        /// <returns>The metadata value.</returns>
        public string GetMetadata(string metadataName)
        {
            string metadataValue = (this as ITaskItem2).GetMetadataValueEscaped(metadataName);
            return EscapingUtilities.UnescapeAll(metadataValue);
        }

        /// <summary>
        /// Copy the metadata (but not the ItemSpec) to destinationItem. If a particular metadata already exists on the
        /// destination item, then it is not overwritten -- the original value wins.
        /// </summary>
        /// <param name="destinationItem">The item to copy metadata to.</param>
        public void CopyMetadataTo(ITaskItem destinationItem)
        {
            ErrorUtilities.VerifyThrowArgumentNull(destinationItem, nameof(destinationItem));

            // also copy the original item-spec under a "magic" metadata -- this is useful for tasks that forward metadata
            // between items, and need to know the source item where the metadata came from
            string originalItemSpec = destinationItem.GetMetadata("OriginalItemSpec");
            ITaskItem2 destinationAsITaskItem2 = destinationItem as ITaskItem2;

            if (_metadata != null)
            {
                // Avoid a copy if we can
                if (destinationItem is TaskItem destinationAsTaskItem && destinationAsTaskItem.Metadata == null)
                {
                    destinationAsTaskItem.Metadata = _metadata.Clone(); // Copy on write!
                }
                else
                {
                    foreach (KeyValuePair<string, string> entry in _metadata)
                    {
                        string value;

                        if (destinationAsITaskItem2 != null)
                        {
                            value = destinationAsITaskItem2.GetMetadataValueEscaped(entry.Key);

                            if (string.IsNullOrEmpty(value))
                            {
                                destinationAsITaskItem2.SetMetadata(entry.Key, entry.Value);
                            }
                        }
                        else
                        {
                            value = destinationItem.GetMetadata(entry.Key);

                            if (string.IsNullOrEmpty(value))
                            {
                                destinationItem.SetMetadata(entry.Key, EscapingUtilities.Escape(entry.Value));
                            }
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(originalItemSpec))
            {
                if (destinationAsITaskItem2 != null)
                {
                    destinationAsITaskItem2.SetMetadata("OriginalItemSpec", ((ITaskItem2)this).EvaluatedIncludeEscaped);
                }
                else
                {
                    destinationItem.SetMetadata("OriginalItemSpec", EscapingUtilities.Escape(ItemSpec));
                }
            }
        }

        /// <summary>
        /// Get the collection of custom metadata. This does not include built-in metadata.
        /// </summary>
        /// <remarks>
        /// RECOMMENDED GUIDELINES FOR METHOD IMPLEMENTATIONS:
        /// 1) this method should return a clone of the metadata
        /// 2) writing to this dictionary should not be reflected in the underlying item.
        /// </remarks>
        /// <comments>
        /// Returns an UNESCAPED version of the custom metadata. For the escaped version (which 
        /// is how it is stored internally), call ITaskItem2.CloneCustomMetadataEscaped. 
        /// </comments>
        public IDictionary CloneCustomMetadata()
        {
            var dictionary = new CopyOnWriteDictionary<string>(MSBuildNameIgnoreCaseComparer.Default);

            if (_metadata != null)
            {
                foreach (KeyValuePair<string, string> entry in _metadata)
                {
                    dictionary.Add(entry.Key, EscapingUtilities.UnescapeAll(entry.Value));
                }
            }

            return dictionary;
        }

        /// <summary>
        /// Gets the item-spec.
        /// </summary>
        /// <returns>The item-spec string.</returns>
        public override string ToString() => _itemSpec;

#if FEATURE_APPDOMAIN
        /// <summary>
        /// Overridden to give this class infinite lease time. Otherwise we end up with a limited
        /// lease (5 minutes I think) and instances can expire if they take long time processing.
        /// </summary>
        [SecurityCritical]
        public override object InitializeLifetimeService() => null; // null means infinite lease time
#endif

        #endregion

        #region Operators

        /// <summary>
        /// This allows an explicit typecast from a "TaskItem" to a "string", returning the escaped ItemSpec for this item.
        /// </summary>
        /// <param name="taskItemToCast">The item to operate on.</param>
        /// <returns>The item-spec of the item.</returns>
        public static explicit operator string(TaskItem taskItemToCast)
        {
            ErrorUtilities.VerifyThrowArgumentNull(taskItemToCast, nameof(taskItemToCast));
            return taskItemToCast.ItemSpec;
        }

        #endregion

        #region ITaskItem2 implementation

        /// <summary>
        /// Returns the escaped value of the metadata with the specified key.
        /// </summary>
        string ITaskItem2.GetMetadataValueEscaped(string metadataName)
        {
            ErrorUtilities.VerifyThrowArgumentNull(metadataName, nameof(metadataName));

            string metadataValue = null;

            if (FileUtilities.ItemSpecModifiers.IsDerivableItemSpecModifier(metadataName))
            {
                // FileUtilities.GetItemSpecModifier is expecting escaped data, which we assume we already are.
                // Passing in a null for currentDirectory indicates we are already in the correct current directory
                metadataValue = FileUtilities.ItemSpecModifiers.GetItemSpecModifier(null, _itemSpec, _definingProject, metadataName, ref _fullPath);
            }
            else
            {
                _metadata?.TryGetValue(metadataName, out metadataValue);
            }

            return metadataValue ?? string.Empty;
        }

        /// <summary>
        /// Sets the escaped value of the metadata with the specified name.
        /// </summary>
        /// <comments>
        /// Assumes the value is passed in unescaped. 
        /// </comments>
        void ITaskItem2.SetMetadataValueLiteral(string metadataName, string metadataValue) => SetMetadata(metadataName, EscapingUtilities.Escape(metadataValue));

        /// <summary>
        /// ITaskItem2 implementation which returns a clone of the metadata on this object.
        /// Values returned are in their original escaped form. 
        /// </summary>
        /// <returns>The cloned metadata.</returns>
        IDictionary ITaskItem2.CloneCustomMetadataEscaped() => _metadata == null
            ? new CopyOnWriteDictionary<string>(MSBuildNameIgnoreCaseComparer.Default)
            : _metadata.Clone();

        #endregion
    }
}
