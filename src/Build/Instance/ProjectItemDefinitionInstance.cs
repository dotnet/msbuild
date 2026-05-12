// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Execution
{
    /// <summary>
    /// An evaluated item definition for a particular item-type, divested of all references to XML.
    /// Immutable.
    /// </summary>
    [DebuggerDisplay("{_itemType} #Metadata={MetadataCount}")]
    public class ProjectItemDefinitionInstance : IKeyed, IMetadataTable, IItemDefinition<ProjectMetadataInstance>, ITranslatable, IItemTypeDefinition
    {
        /// <summary>
        /// Item type, for example "Compile", that this item definition applies to
        /// </summary>
        private string _itemType;

        /// <summary>
        /// Collection of metadata that link the XML metadata and instance metadata
        /// Since evaluation has occurred, this is an unordered collection.
        /// </summary>
        private ImmutableDictionary<string, string> _metadata;

        /// <summary>
        /// Constructs an empty project item definition instance.
        /// </summary>
        /// <param name="itemType">The type of item this definition object represents.</param>
        internal ProjectItemDefinitionInstance(string itemType)
        {
            ErrorUtilities.VerifyThrowArgumentNull(itemType);

            _itemType = itemType;
        }

        /// <summary>
        /// Called when a ProjectInstance is created.
        /// </summary>
        /// <remarks>
        /// Assumes that the itemType string originated in a ProjectItemDefinitionElement and therefore
        /// was already validated.
        /// </remarks>
        internal ProjectItemDefinitionInstance(ProjectItemDefinition itemDefinition)
            : this(itemDefinition.ItemType)
        {
            if (itemDefinition.MetadataCount > 0)
            {
                IEnumerable<KeyValuePair<string, string>> projectMetadataInstances = itemDefinition.Metadata.Select(originalMetadata
                        => new KeyValuePair<string, string>(originalMetadata.Name, originalMetadata.EvaluatedValueEscaped));
                _metadata = ImmutableDictionaryExtensions.EmptyMetadata
                    .SetItems(projectMetadataInstances, ProjectMetadataInstance.VerifyThrowReservedName);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectItemDefinitionInstance"/> class.
        /// </summary>
        /// <param name="itemType">The type of item this definition object represents.</param>
        /// <param name="metadata">A (possibly null) collection of the metadata associated with this item definition.</param>
        internal ProjectItemDefinitionInstance(string itemType, ImmutableDictionary<string, string> metadata)
            : this(itemType)
        {
            _metadata = metadata;
        }

        private ProjectItemDefinitionInstance()
        {
        }

        /// <summary>
        /// Type of this item definition.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public string ItemType
        {
            [DebuggerStepThrough]
            get
            { return _itemType; }
        }

        /// <summary>
        /// Metadata on the item definition.
        /// If there is no metadata, returns empty collection.
        /// This is a read-only collection.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1721:PropertyNamesShouldNotMatchGetMethods", Justification = "This is a reasonable choice. API review approved")]
        public ICollection<ProjectMetadataInstance> Metadata
        {
            get
            {
                if (_metadata == null)
                {
                    return ReadOnlyEmptyCollection<ProjectMetadataInstance>.Instance;
                }

                IEnumerable<ProjectMetadataInstance> metadata = _metadata.Select(kvp => new ProjectMetadataInstance(kvp.Key, kvp.Value, allowItemSpecModifiers: true));
                return new ReadOnlyCollection<ProjectMetadataInstance>(metadata);
            }
        }

        /// <summary>
        /// Number of pieces of metadata on this item definition.
        /// </summary>
        public int MetadataCount
        {
            get { return (_metadata == null) ? 0 : _metadata.Count; }
        }

        /// <summary>
        /// Names of all metadata on this item definition
        /// </summary>
        public IEnumerable<string> MetadataNames => _metadata == null ? [] : _metadata.Keys;

        /// <summary>
        /// Implementation of IKeyed exposing the item type, so these
        /// can be put in a dictionary conveniently.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        string IKeyed.Key
        {
            get { return ItemType; }
        }

        /// <summary>
        /// The backing metadata dictionary for copy-on-write cloning.
        /// </summary>
        internal ImmutableDictionary<string, string> BackingMetadata => _metadata ?? ImmutableDictionaryExtensions.EmptyMetadata;

        /// <summary>
        /// Get any metadata in the item that has the specified name,
        /// otherwise returns null
        /// </summary>
        [DebuggerStepThrough]
        public ProjectMetadataInstance GetMetadata(string name)
        {
            return _metadata?.TryGetValue(name, out string value) ?? false
                ? new ProjectMetadataInstance(name, value, allowItemSpecModifiers: true)
                : null;
        }

        #region IMetadataTable Members

        /// <summary>
        /// Returns the specified metadata.
        /// </summary>
        /// <param name="name">The metadata name.</param>
        /// <returns>The metadata value, or an empty string if none exists.</returns>
        string IMetadataTable.GetEscapedValue(string name)
        {
            return ((IMetadataTable)this).GetEscapedValue(null, name);
        }

        /// <summary>
        /// Returns the metadata for the specified item type.
        /// </summary>
        /// <param name="specifiedItemType">The item type.</param>
        /// <param name="name">The metadata name.</param>
        /// <returns>The metadata value, or an empty string if none exists.</returns>
        string IMetadataTable.GetEscapedValue(string specifiedItemType, string name)
        {
            return ((IMetadataTable)this).GetEscapedValueIfPresent(specifiedItemType, name) ?? String.Empty;
        }

        /// <summary>
        /// Returns the metadata for the specified item type.
        /// </summary>
        /// <param name="specifiedItemType">The item type.</param>
        /// <param name="name">The metadata name.</param>
        /// <returns>The metadata value, or an null if none exists.</returns>
        string IMetadataTable.GetEscapedValueIfPresent(string specifiedItemType, string name)
        {
            if (_metadata == null)
            {
                return null;
            }

            bool matchesItemType = specifiedItemType == null || String.Equals(_itemType, specifiedItemType, StringComparison.OrdinalIgnoreCase);
            return matchesItemType && _metadata.TryGetValue(name, out string value)
                ? value
                : null;
        }

        #endregion

        /// <summary>
        /// Sets a new metadata value.  Called by the evaluator only.
        /// Discards predecessor as this information is only useful at design time.
        /// </summary>
        ProjectMetadataInstance IItemDefinition<ProjectMetadataInstance>.SetMetadata(ProjectMetadataElement xml, string evaluatedValue, ProjectMetadataInstance predecessor)
        {
            // No mutability check as this is used during creation (evaluation)
            _metadata ??= ImmutableDictionaryExtensions.EmptyMetadata;

            ProjectMetadataInstance metadatum = new ProjectMetadataInstance(xml.Name, evaluatedValue);
            _metadata = _metadata.SetItem(xml.Name, metadatum.EvaluatedValueEscaped);

            return metadatum;
        }

        /// <summary>
        /// Creates a ProjectItemDefinitionElement representing this instance.
        /// </summary>
        internal ProjectItemDefinitionElement ToProjectItemDefinitionElement(ProjectElementContainer parent)
        {
            ProjectItemDefinitionElement element = parent.ContainingProject.CreateItemDefinitionElement(ItemType);
            parent.AppendChild(element);
            foreach (var kvp in _metadata)
            {
                element.AddMetadata(kvp.Key, EscapingUtilities.UnescapeAll(kvp.Value));
            }

            return element;
        }

        void ITranslatable.Translate(ITranslator translator)
        {
            translator.Translate(ref _itemType);
            translator.TranslateDictionary(ref _metadata, MSBuildNameIgnoreCaseComparer.Default);
        }

        internal static ProjectItemDefinitionInstance FactoryForDeserialization(ITranslator translator)
        {
            var instance = new ProjectItemDefinitionInstance();
            ((ITranslatable)instance).Translate(translator);

            return instance;
        }

        string IItemTypeDefinition.ItemType => _itemType;
    }
}
