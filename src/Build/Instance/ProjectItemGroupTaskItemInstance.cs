// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Collections;
using Microsoft.Build.Shared;
using Microsoft.Build.Evaluation;

using Microsoft.Build.Construction;

namespace Microsoft.Build.Execution
{
    /// <summary>
    /// Wraps an unevaluated item under an itemgroup in a target.
    /// Immutable.
    /// </summary>
    [DebuggerDisplay("{_itemType} Include={_include} Exclude={_exclude} Remove={_remove} Condition={_condition}")]
    public class ProjectItemGroupTaskItemInstance: ITranslatable
    {
        /// <summary>
        /// Item type, for example "Compile"
        /// </summary>
        private string _itemType;

        /// <summary>
        /// Unevaluated include
        /// </summary>
        private string _include;

        /// <summary>
        /// Unevaluated exclude
        /// </summary>
        private string _exclude;

        /// <summary>
        /// Unevaluated remove
        /// </summary>
        private string _remove;

        /// <summary>
        /// The list of metadata Remove should match on.
        /// </summary>
        private string _matchOnMetadata;

        /// <summary>
        /// The options for MatchOnMetadata.
        /// </summary>
        private string _matchOnMetadataOptions;

        /// <summary>
        /// The list of metadata to keep.
        /// </summary>
        private string _keepMetadata;

        /// <summary>
        /// The list of metadata to remove.
        /// </summary>
        private string _removeMetadata;

        /// <summary>
        /// True to remove duplicates during the add.
        /// </summary>
        private string _keepDuplicates;

        /// <summary>
        /// Unevaluated condition
        /// </summary>
        private string _condition;

        /// <summary>
        /// Location of this element
        /// </summary>
        private ElementLocation _location;

        /// <summary>
        /// Location of the include, if any
        /// </summary>
        private ElementLocation _includeLocation;

        /// <summary>
        /// Location of the exclude, if any
        /// </summary>
        private ElementLocation _excludeLocation;

        /// <summary>
        /// Location of the remove, if any
        /// </summary>
        private ElementLocation _removeLocation;

        /// <summary>
        /// Location of matchOnMetadata, if any
        /// </summary>
        private ElementLocation _matchOnMetadataLocation;

        /// <summary>
        /// Location of metadataMatchingSchema, if any
        /// </summary>
        private ElementLocation _matchOnMetadataOptionsLocation;

        /// <summary>
        /// Location of keepMetadata, if any
        /// </summary>
        private ElementLocation _keepMetadataLocation;

        /// <summary>
        /// Location of removeMetadata, if any
        /// </summary>
        private ElementLocation _removeMetadataLocation;

        /// <summary>
        /// Location of keepDuplicates, if any
        /// </summary>
        private ElementLocation _keepDuplicatesLocation;

        /// <summary>
        /// Location of the condition, if any
        /// </summary>
        private ElementLocation _conditionLocation;

        /// <summary>
        /// Ordered collection of unevaluated metadata.
        /// May be null.
        /// </summary>
        /// <remarks>
        /// There is no need for a PropertyDictionary here as the build always
        /// walks through all metadata sequentially.
        /// Lazily created, as so many items have no metadata at all.
        /// </remarks>
        private List<ProjectItemGroupTaskMetadataInstance> _metadata;

        /// <summary>
        /// Constructor called by the Evaluator.
        /// Metadata may be null, indicating no metadata.
        /// Metadata collection is ordered.
        /// Assumes ProjectItemGroupTaskMetadataInstance is an immutable type.
        /// </summary>
        internal ProjectItemGroupTaskItemInstance
            (
            string itemType,
            string include,
            string exclude,
            string remove,
            string matchOnMetadata,
            string matchOnMetadataOptions,
            string keepMetadata,
            string removeMetadata,
            string keepDuplicates,
            string condition,
            ElementLocation location,
            ElementLocation includeLocation,
            ElementLocation excludeLocation,
            ElementLocation removeLocation,
            ElementLocation matchOnMetadataLocation,
            ElementLocation matchOnMetadataOptionsLocation,
            ElementLocation keepMetadataLocation,
            ElementLocation removeMetadataLocation,
            ElementLocation keepDuplicatesLocation,
            ElementLocation conditionLocation,
            List<ProjectItemGroupTaskMetadataInstance> metadata
            )
        {
            ErrorUtilities.VerifyThrowInternalNull(itemType, nameof(itemType));
            ErrorUtilities.VerifyThrowInternalNull(include, nameof(include));
            ErrorUtilities.VerifyThrowInternalNull(exclude, nameof(exclude));
            ErrorUtilities.VerifyThrowInternalNull(remove, nameof(remove));
            ErrorUtilities.VerifyThrowInternalNull(keepMetadata, nameof(keepMetadata));
            ErrorUtilities.VerifyThrowInternalNull(removeMetadata, nameof(removeMetadata));
            ErrorUtilities.VerifyThrowInternalNull(keepDuplicates, nameof(keepDuplicates));
            ErrorUtilities.VerifyThrowInternalNull(condition, nameof(condition));
            ErrorUtilities.VerifyThrowInternalNull(location, nameof(location));

            _itemType = itemType;
            _include = include;
            _exclude = exclude;
            _remove = remove;
            _matchOnMetadata = matchOnMetadata;
            _matchOnMetadataOptions = matchOnMetadataOptions;
            _keepMetadata = keepMetadata;
            _removeMetadata = removeMetadata;
            _keepDuplicates = keepDuplicates;
            _condition = condition;
            _location = location;
            _includeLocation = includeLocation;
            _excludeLocation = excludeLocation;
            _removeLocation = removeLocation;
            _matchOnMetadataLocation = matchOnMetadataLocation;
            _matchOnMetadataOptionsLocation = matchOnMetadataOptionsLocation;
            _keepMetadataLocation = keepMetadataLocation;
            _removeMetadataLocation = removeMetadataLocation;
            _keepDuplicatesLocation = keepDuplicatesLocation;
            _conditionLocation = conditionLocation;
            _metadata = metadata;
        }

        private ProjectItemGroupTaskItemInstance()
        {

        }

        /// <summary>
        /// Cloning constructor
        /// </summary>
        private ProjectItemGroupTaskItemInstance(ProjectItemGroupTaskItemInstance that)
        {
            // All fields are immutable
            _itemType = that._itemType;
            _include = that._include;
            _exclude = that._exclude;
            _remove = that._remove;
            _matchOnMetadata = that._matchOnMetadata;
            _matchOnMetadataOptions = that._matchOnMetadataOptions;
            _keepMetadata = that._keepMetadata;
            _removeMetadata = that._removeMetadata;
            _keepDuplicates = that._keepDuplicates;
            _condition = that._condition;
            _metadata = that._metadata;
        }

        /// <summary>
        /// Item type, for example "Compile"
        /// </summary>
        public string ItemType
        {
            [DebuggerStepThrough]
            get
            { return _itemType; }
        }

        /// <summary>
        /// Unevaluated include value
        /// </summary>
        public string Include
        {
            [DebuggerStepThrough]
            get
            { return _include; }
        }

        /// <summary>
        /// Unevaluated exclude value
        /// </summary>
        public string Exclude
        {
            [DebuggerStepThrough]
            get
            { return _exclude; }
        }

        /// <summary>
        /// Unevaluated remove value
        /// </summary>
        public string Remove
        {
            [DebuggerStepThrough]
            get
            { return _remove; }
        }

        /// <summary>
        /// Unevaluated MatchOnMetadata value
        /// </summary>
        public string MatchOnMetadata
        {
            [DebuggerStepThrough]
            get
            { return _matchOnMetadata; }
        }

        /// <summary>
        /// Unevaluated MatchOnMetadataOptions value
        /// </summary>
        public string MatchOnMetadataOptions
        {
            [DebuggerStepThrough]
            get
            { return _matchOnMetadataOptions; }
        }

        /// <summary>
        /// Unevaluated keepMetadata value
        /// </summary>
        public string KeepMetadata
        {
            [DebuggerStepThrough]
            get
            { return _keepMetadata; }
        }

        /// <summary>
        /// Unevaluated removeMetadata value
        /// </summary>
        public string RemoveMetadata
        {
            [DebuggerStepThrough]
            get
            { return _removeMetadata; }
        }

        /// <summary>
        /// Unevaluated keepDuplicates value
        /// </summary>
        public string KeepDuplicates
        {
            [DebuggerStepThrough]
            get
            { return _keepDuplicates; }
        }

        /// <summary>
        /// Unevaluated condition value
        /// </summary>
        public string Condition
        {
            [DebuggerStepThrough]
            get
            { return _condition; }
        }

        /// <summary>
        /// Ordered collection of unevaluated metadata on the item.
        /// If there is no metadata, returns an empty collection.
        /// </summary>IEnumerable
        public ICollection<ProjectItemGroupTaskMetadataInstance> Metadata
        {
            [DebuggerStepThrough]
            get
            {
                return (_metadata == null) ?
                    (ICollection<ProjectItemGroupTaskMetadataInstance>)ReadOnlyEmptyCollection<ProjectItemGroupTaskMetadataInstance>.Instance :
                    new ReadOnlyCollection<ProjectItemGroupTaskMetadataInstance>(_metadata);
            }
        }

        /// <summary>
        /// Location of the element
        /// </summary>
        public ElementLocation Location
        {
            [DebuggerStepThrough]
            get
            { return _location; }
        }

        /// <summary>
        /// Location of the include attribute, if any
        /// </summary>
        public ElementLocation IncludeLocation
        {
            [DebuggerStepThrough]
            get
            { return _includeLocation; }
        }

        /// <summary>
        /// Location of the exclude attribute, if any
        /// </summary>
        public ElementLocation ExcludeLocation
        {
            [DebuggerStepThrough]
            get
            { return _excludeLocation; }
        }

        /// <summary>
        /// Location of the remove attribute, if any
        /// </summary>
        public ElementLocation RemoveLocation
        {
            [DebuggerStepThrough]
            get
            { return _removeLocation; }
        }

        /// <summary>
        /// Location of the matchOnMetadata attribute, if any
        /// </summary>
        public ElementLocation MatchOnMetadataLocation
        {
            [DebuggerStepThrough]
            get
            { return _matchOnMetadataLocation; }
        }

        /// <summary>
        /// Location of the matchOnMetadataOptions attribute, if any
        /// </summary>
        public ElementLocation MatchOnMetadataOptionsLocation
        {
            [DebuggerStepThrough]
            get
            { return _matchOnMetadataOptionsLocation; }
        }

        /// <summary>
        /// Location of the keepMetadata attribute, if any
        /// </summary>
        public ElementLocation KeepMetadataLocation
        {
            [DebuggerStepThrough]
            get
            { return _keepMetadataLocation; }
        }

        /// <summary>
        /// Location of the removeMetadata attribute, if any
        /// </summary>
        public ElementLocation RemoveMetadataLocation
        {
            [DebuggerStepThrough]
            get
            { return _removeMetadataLocation; }
        }

        /// <summary>
        /// Location of the keepDuplicates attribute, if any
        /// </summary>
        public ElementLocation KeepDuplicatesLocation
        {
            [DebuggerStepThrough]
            get
            { return _keepDuplicatesLocation; }
        }

        /// <summary>
        /// Location of the condition attribute if any
        /// </summary>
        public ElementLocation ConditionLocation
        {
            [DebuggerStepThrough]
            get
            { return _conditionLocation; }
        }

        /// <summary>
        /// Deep clone
        /// </summary>
        internal ProjectItemGroupTaskItemInstance DeepClone()
        {
            return new ProjectItemGroupTaskItemInstance(this);
        }

        void ITranslatable.Translate(ITranslator translator)
        {
            translator.Translate(ref _itemType);
            translator.Translate(ref _include);
            translator.Translate(ref _exclude);
            translator.Translate(ref _remove);
            translator.Translate(ref _matchOnMetadata);
            translator.Translate(ref _matchOnMetadataOptions);
            translator.Translate(ref _keepMetadata);
            translator.Translate(ref _removeMetadata);
            translator.Translate(ref _keepDuplicates);
            translator.Translate(ref _condition);
            translator.Translate(ref _location, ElementLocation.FactoryForDeserialization);
            translator.Translate(ref _includeLocation, ElementLocation.FactoryForDeserialization);
            translator.Translate(ref _excludeLocation, ElementLocation.FactoryForDeserialization);
            translator.Translate(ref _removeLocation, ElementLocation.FactoryForDeserialization);
            translator.Translate(ref _keepMetadataLocation, ElementLocation.FactoryForDeserialization);
            translator.Translate(ref _removeMetadataLocation, ElementLocation.FactoryForDeserialization);
            translator.Translate(ref _keepDuplicatesLocation, ElementLocation.FactoryForDeserialization);
            translator.Translate(ref _conditionLocation, ElementLocation.FactoryForDeserialization);
            translator.Translate(ref _metadata, ProjectItemGroupTaskMetadataInstance.FactoryForDeserialization);
        }

        internal static ProjectItemGroupTaskItemInstance FactoryForDeserialization(ITranslator translator)
        {
            var instance = new ProjectItemGroupTaskItemInstance();
            ((ITranslatable)instance).Translate(translator);

            return instance;
        }
    }
}
