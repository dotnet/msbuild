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
    public class ProjectItemGroupTaskItemInstance: INodePacketTranslatable
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
            string keepMetadata,
            string removeMetadata,
            string keepDuplicates,
            string condition,
            ElementLocation location,
            ElementLocation includeLocation,
            ElementLocation excludeLocation,
            ElementLocation removeLocation,
            ElementLocation keepMetadataLocation,
            ElementLocation removeMetadataLocation,
            ElementLocation keepDuplicatesLocation,
            ElementLocation conditionLocation,
            List<ProjectItemGroupTaskMetadataInstance> metadata
            )
        {
            ErrorUtilities.VerifyThrowInternalNull(itemType, "itemType");
            ErrorUtilities.VerifyThrowInternalNull(include, "include");
            ErrorUtilities.VerifyThrowInternalNull(exclude, "exclude");
            ErrorUtilities.VerifyThrowInternalNull(remove, "remove");
            ErrorUtilities.VerifyThrowInternalNull(keepMetadata, "keepMetadata");
            ErrorUtilities.VerifyThrowInternalNull(removeMetadata, "removeMetadata");
            ErrorUtilities.VerifyThrowInternalNull(keepDuplicates, "keepDuplicates");
            ErrorUtilities.VerifyThrowInternalNull(condition, "condition");
            ErrorUtilities.VerifyThrowInternalNull(location, "location");

            _itemType = itemType;
            _include = include;
            _exclude = exclude;
            _remove = remove;
            _keepMetadata = keepMetadata;
            _removeMetadata = removeMetadata;
            _keepDuplicates = keepDuplicates;
            _condition = condition;
            _location = location;
            _includeLocation = includeLocation;
            _excludeLocation = excludeLocation;
            _removeLocation = removeLocation;
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

        void INodePacketTranslatable.Translate(INodePacketTranslator translator)
        {
            translator.Translate(ref _itemType);
            translator.Translate(ref _include);
            translator.Translate(ref _exclude);
            translator.Translate(ref _remove);
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

        internal static ProjectItemGroupTaskItemInstance FactoryForDeserialization(INodePacketTranslator translator)
        {
            var instance = new ProjectItemGroupTaskItemInstance();
            ((INodePacketTranslatable)instance).Translate(translator);

            return instance;
        }
    }
}
