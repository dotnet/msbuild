// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
#if FEATURE_APPDOMAIN
using System.Runtime.Remoting;
#endif
#if FEATURE_SECURITY_PERMISSIONS
using System.Security;
#endif

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Collections;
using System.Collections.Immutable;

#nullable disable

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
        ITaskItem2,
        IMetadataContainer // expose direct underlying metadata for fast access in binary logger
    {
        #region Member Data

        // This is the final evaluated item specification.  Stored in escaped form.
        private string _itemSpec;

        // These are the user-defined metadata on the item, specified in the
        // project file via XML child elements of the item element.  These have
        // no meaning to MSBuild, but tasks may use them.
        // Values are stored in escaped form.
        private ImmutableDictionary<string, string> _metadata;

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
        /// Default constructor -- do not use.
        /// </summary>
        /// <remarks>
        /// This constructor exists only so that the type is COM-creatable. Prefer <see cref="TaskItem(string, bool)"/>.
        /// </remarks>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public TaskItem()
        {
            _itemSpec = string.Empty;
        }

        /// <summary>
        /// This constructor creates a new task item, given the item spec.
        /// </summary>
        /// <comments>Assumes the itemspec passed in is escaped and represents a file path. </comments>
        /// <param name="itemSpec">The item-spec string.</param>
        public TaskItem(string itemSpec)
            : this(itemSpec, treatAsFilePath: true) { }

        /// <summary>
        /// This constructor creates a new task item, given the item spec.
        /// </summary>
        /// <comments>
        /// Assumes the itemspec passed in is escaped.
        /// If <see name="treatAsFilePath" /> is set to <see langword="true" />, the value in <see name="itemSpec" />
        /// will be fixed up as a path by having any backslashes replaced with slashes.
        /// </comments>
        /// <param name="itemSpec">The item-spec string.</param>
        /// <param name="treatAsFilePath">
        /// Specifies whether or not to treat the value in <see name="itemSpec" />
        /// as a file path and attempt to normalize it.
        /// </param>
        public TaskItem(
            string itemSpec,
            bool treatAsFilePath)
        {
            ErrorUtilities.VerifyThrowArgumentNull(itemSpec);

            _itemSpec = treatAsFilePath ? FileUtilities.FixFilePath(itemSpec) : itemSpec;
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
        public TaskItem(
            string itemSpec,
            IDictionary itemMetadata) :
            this(itemSpec)
        {
            ErrorUtilities.VerifyThrowArgumentNull(itemMetadata);

            if (itemMetadata.Count > 0)
            {
                ImmutableDictionary<string, string>.Builder builder = ImmutableDictionaryExtensions.EmptyMetadata.ToBuilder();

                foreach (DictionaryEntry singleMetadata in itemMetadata)
                {
                    // don't import metadata whose names clash with the names of reserved metadata
                    string key = (string)singleMetadata.Key;
                    if (!FileUtilities.ItemSpecModifiers.IsDerivableItemSpecModifier(key))
                    {
                        builder[key] = (string)singleMetadata.Value ?? string.Empty;
                    }
                }

                _metadata = builder.ToImmutable();
            }
        }

        /// <summary>
        /// This constructor creates a new TaskItem, using the given ITaskItem.
        /// </summary>
        /// <param name="sourceItem">The item to copy.</param>
        public TaskItem(
            ITaskItem sourceItem)
        {
            ErrorUtilities.VerifyThrowArgumentNull(sourceItem);

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
                int count = (_metadata?.Count ?? 0) + FileUtilities.ItemSpecModifiers.All.Length;

                var metadataNames = new List<string>(capacity: count);

                if (_metadata is not null)
                {
                    metadataNames.AddRange(_metadata.Keys);
                }

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
        /// Gets the backing metadata dictionary in a serializable wrapper.
        /// </summary>
        SerializableMetadata IMetadataContainer.BackingMetadata => Metadata;

        /// <summary>
        /// Gets a value indicating whether indicates whether the item has any custom metadata.
        /// </summary>
        bool IMetadataContainer.HasCustomMetadata => _metadata?.Count > 0;

        /// <summary>
        /// Gets the metadata dictionary
        /// Property is required so that we can access the metadata dictionary in an item from
        /// another appdomain, as the CLR has implemented remoting policies that disallow accessing
        /// private fields in remoted items.
        /// </summary>
        private SerializableMetadata Metadata
        {
            get => new(_metadata);
            set => _metadata = value.Dictionary;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Removes one of the arbitrary metadata on the item.
        /// </summary>
        /// <param name="metadataName">Name of metadata to remove.</param>
        public void RemoveMetadata(string metadataName)
        {
            ErrorUtilities.VerifyThrowArgumentNull(metadataName);
            ErrorUtilities.VerifyThrowArgument(!FileUtilities.ItemSpecModifiers.IsItemSpecModifier(metadataName),
                "Shared.CannotChangeItemSpecModifiers", metadataName);

            _metadata = _metadata?.Remove(metadataName);
        }

        /// <summary>
        /// Removes any metadata matching the given names.
        /// </summary>
        /// <param name="metadataNames">The metadata names to remove.</param>
        void IMetadataContainer.RemoveMetadataRange(IEnumerable<string> metadataNames)
        {
            if (_metadata == null || _metadata.IsEmpty)
            {
                return;
            }

            _metadata = _metadata.RemoveRange(metadataNames);
        }

        /// <summary>
        /// Sets one of the arbitrary metadata on the item.
        /// </summary>
        /// <comments>
        /// Assumes that the value being passed in is in its escaped form.
        /// </comments>
        /// <param name="metadataName">Name of metadata to set or change.</param>
        /// <param name="metadataValue">Value of metadata.</param>
        public void SetMetadata(
            string metadataName,
            string metadataValue)
        {
            ErrorUtilities.VerifyThrowArgumentLength(metadataName);

            // Non-derivable metadata can only be set at construction time.
            // That's why this is IsItemSpecModifier and not IsDerivableItemSpecModifier.
            ErrorUtilities.VerifyThrowArgument(!FileUtilities.ItemSpecModifiers.IsDerivableItemSpecModifier(metadataName),
                "Shared.CannotChangeItemSpecModifiers", metadataName);

            _metadata ??= ImmutableDictionaryExtensions.EmptyMetadata;

            _metadata = _metadata.SetItem(metadataName, metadataValue ?? string.Empty);
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
            ErrorUtilities.VerifyThrowArgumentNull(destinationItem);

            // also copy the original item-spec under a "magic" metadata -- this is useful for tasks that forward metadata
            // between items, and need to know the source item where the metadata came from
            string originalItemSpec = destinationItem.GetMetadata("OriginalItemSpec");
            ITaskItem2 destinationAsITaskItem2 = destinationItem as ITaskItem2;
            IMetadataContainer destinationAsMetadataContainer = destinationItem as IMetadataContainer;

            if (_metadata != null)
            {
                if (destinationItem is TaskItem destinationAsTaskItem)
                {
                    ImmutableDictionary<string, string> copiedMetadata;
                    ImmutableDictionary<string, string> destinationMetadata = destinationAsTaskItem.Metadata.Dictionary;

                    // Avoid a copy if we can, and if not, minimize the number of items we have to set.
                    if (!destinationAsMetadataContainer.HasCustomMetadata)
                    {
                        copiedMetadata = _metadata;
                    }
                    else if (destinationMetadata.Count < _metadata.Count)
                    {
                        copiedMetadata = _metadata.SetItems(destinationMetadata.Where(entry => !String.IsNullOrEmpty(entry.Value)));
                    }
                    else
                    {
                        copiedMetadata = destinationMetadata.SetItems(_metadata.Where(entry => !destinationMetadata.TryGetValue(entry.Key, out string val) || String.IsNullOrEmpty(val)));
                    }

                    // Wrap in SerializableMetadata since ImmutableDictionary is not serializable.
                    destinationAsTaskItem.Metadata = new SerializableMetadata(copiedMetadata);
                }
                else if (destinationAsITaskItem2 != null && destinationAsMetadataContainer != null)
                {
                    // Most likely the destination item is a ProjectItemInstance.TaskItem.
                    // Defer any LINQ queries. If the destination supports copy-on-write cloning, we may be able to pass
                    // a direct reference to the metadata.
                    IEnumerable<KeyValuePair<string, string>> metadataToImport = _metadata;

                    // If the destination already has existing metadata, only import values which are not already set.
                    if (destinationAsMetadataContainer.HasCustomMetadata)
                    {
                        metadataToImport = metadataToImport
                            .Where(metadatum => string.IsNullOrEmpty(destinationAsITaskItem2.GetMetadataValueEscaped(metadatum.Key)));
                    }

#if FEATURE_APPDOMAIN
                    if (RemotingServices.IsTransparentProxy(destinationItem))
                    {
                        // Linq is not serializable so materialize the collection before making the call.
                        metadataToImport = metadataToImport.ToList();
                    }
#endif

                    destinationAsMetadataContainer.ImportMetadata(metadataToImport);
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
            ErrorUtilities.VerifyThrowArgumentNull(taskItemToCast);
            return taskItemToCast.ItemSpec;
        }

        #endregion

        #region ITaskItem2 implementation

        /// <summary>
        /// Returns the escaped value of the metadata with the specified key.
        /// </summary>
        string ITaskItem2.GetMetadataValueEscaped(string metadataName)
        {
            ErrorUtilities.VerifyThrowArgumentNull(metadataName);

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
            : new CopyOnWriteDictionary<string>(_metadata);

        #endregion

        IEnumerable<KeyValuePair<string, string>> IMetadataContainer.EnumerateMetadata()
        {
#if FEATURE_APPDOMAIN
            // Can't send a yield-return iterator across AppDomain boundaries
            // so have to allocate
            if (!AppDomain.CurrentDomain.IsDefaultAppDomain())
            {
                return EnumerateMetadataEager();
            }
#endif

            // In general case we want to return an iterator without allocating a collection
            // to hold the result, so we can stream the items directly to the consumer.
            return EnumerateMetadataLazy();
        }

        void IMetadataContainer.ImportMetadata(IEnumerable<KeyValuePair<string, string>> metadata)
        {
            if ((_metadata == null || _metadata.IsEmpty) && metadata is ImmutableDictionary<string, string> immutableMetadata)
            {
                _metadata = immutableMetadata;
                return;
            }

            _metadata ??= ImmutableDictionaryExtensions.EmptyMetadata;
            _metadata = _metadata.SetItems(metadata.Select(kvp => new KeyValuePair<string, string>(kvp.Key, kvp.Value ?? string.Empty)));
        }

#if FEATURE_APPDOMAIN
        private IEnumerable<KeyValuePair<string, string>> EnumerateMetadataEager()
        {
            if (_metadata == null)
            {
                return [];
            }

            int count = _metadata.Count;
            int index = 0;
            var result = new KeyValuePair<string, string>[count];
            foreach (var kvp in _metadata)
            {
                var unescaped = new KeyValuePair<string, string>(kvp.Key, EscapingUtilities.UnescapeAll(kvp.Value));
                result[index++] = unescaped;
            }

            return result;
        }
#endif

        private IEnumerable<KeyValuePair<string, string>> EnumerateMetadataLazy()
        {
            if (_metadata == null)
            {
                yield break;
            }

            foreach (var kvp in _metadata)
            {
                var unescaped = new KeyValuePair<string, string>(kvp.Key, EscapingUtilities.UnescapeAll(kvp.Value));
                yield return unescaped;
            }
        }
    }
}
