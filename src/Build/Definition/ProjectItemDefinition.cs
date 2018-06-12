// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Represents a set of evaluated item definitions all applying to the same item-type.</summary>
//-----------------------------------------------------------------------

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;
using System.Collections.Generic;
using System;
using System.Linq;

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// An evaluated item definition for a particular item-type.
    /// </summary>
    /// <remarks>
    /// Note that these are somewhat different to items. Like items, they can have metadata; like properties, the metadata
    /// can override each other. So during evaluation all the item definitions for a type are rolled together (assuming
    /// their conditions are true) to create one ProjectItemDefinition for each type. For this reason, the ProjectItemDefinition
    /// often will not point to a single ProjectItemDefinitionElement. The metadata within, however, will each point to a single
    /// ProjectMetadataElement, and these can be added, removed, and modified.
    /// </remarks>
    [DebuggerDisplay("{_itemType} #Metadata={MetadataCount}")]
    public class ProjectItemDefinition : IKeyed, IMetadataTable, IItemDefinition<ProjectMetadata>, IProjectMetadataParent
    {
        /// <summary>
        /// Project that this item definition lives in.
        /// ProjectItemDefinitions always live in a project.
        /// Used to evaluate any updates to child metadata.
        /// </summary>
        private readonly Project _project;

        /// <summary>
        /// Item type, for example "Compile", that this item definition applies to
        /// </summary>
        private readonly string _itemType;

        /// <summary>
        /// Collection of metadata that link the XML metadata and instance metadata
        /// Since evaluation has occurred, this is an unordered collection.
        /// </summary>
        private PropertyDictionary<ProjectMetadata> _metadata;

        /// <summary>
        /// Called by the Evaluator during project evaluation.
        /// </summary>
        /// <remarks>
        /// Assumes that the itemType string originated in a ProjectItemDefinitionElement and therefore
        /// was already validated.
        /// </remarks>
        internal ProjectItemDefinition(Project project, string itemType)
        {
            ErrorUtilities.VerifyThrowInternalNull(project, "project");
            ErrorUtilities.VerifyThrowArgumentLength(itemType, "itemType");

            _project = project;
            _itemType = itemType;
            _metadata = null;
        }

        /// <summary>
        /// Project that this item lives in.
        /// ProjectDefinitions always live in a project.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public Project Project
        {
            [DebuggerStepThrough]
            get
            { return _project; }
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
        public IEnumerable<ProjectMetadata> Metadata => _metadata ?? Enumerable.Empty<ProjectMetadata>();

        /// <summary>
        /// Count of metadata on the item definition.
        /// </summary>
        public int MetadataCount
        {
            get { return (_metadata == null) ? 0 : _metadata.Count; }
        }

        /// <summary>
        /// Implementation of IKeyed exposing the item type, so these 
        /// can be put in a dictionary conveniently.
        /// </summary>
        string IKeyed.Key
        {
            get { return ItemType; }
        }

        /// <summary>
        /// Get any metadata in the item that has the specified name,
        /// otherwise returns null
        /// </summary>
        [DebuggerStepThrough]
        public ProjectMetadata GetMetadata(string name)
        {
            return (_metadata == null) ? null : _metadata[name];
        }

        /// <summary>
        /// Get the value of any metadata in the item that has the specified
        /// name, otherwise returns null
        /// </summary>
        public string GetMetadataValue(string name)
        {
            string escapedValue = (this as IMetadataTable).GetEscapedValue(name);

            return (escapedValue == null) ? null : EscapingUtilities.UnescapeAll(escapedValue);
        }

        /// <summary>
        /// Sets a new metadata value on the ItemDefinition.
        /// </summary>
        /// <remarks>Unevaluated value is assumed to be escaped as necessary</remarks>
        public ProjectMetadata SetMetadataValue(string name, string unevaluatedValue)
        {
            XmlUtilities.VerifyThrowArgumentValidElementName(name);
            ErrorUtilities.VerifyThrowArgument(!FileUtilities.ItemSpecModifiers.IsItemSpecModifier(name), "ItemSpecModifierCannotBeCustomMetadata", name);
            ErrorUtilities.VerifyThrowInvalidOperation(!XMakeElements.ReservedItemNames.Contains(name), "CannotModifyReservedItemMetadata", name);

            ProjectMetadata metadatum;

            if (_metadata != null)
            {
                metadatum = _metadata[name];

                if (metadatum != null)
                {
                    Project.VerifyThrowInvalidOperationNotImported(metadatum.Xml.ContainingProject);
                    metadatum.UnevaluatedValue = unevaluatedValue;
                    return metadatum;
                }
            }

            // We can't use the item definition that this object came from as a root, as it doesn't map directly 
            // to a single XML element. Instead, add a new one to the project. Best we can do.
            ProjectItemDefinitionElement itemDefinition = _project.Xml.AddItemDefinition(_itemType);

            ProjectMetadataElement metadatumXml = itemDefinition.AddMetadata(name, unevaluatedValue);

            _metadata = _metadata ?? new PropertyDictionary<ProjectMetadata>();

            string evaluatedValueEscaped = _project.ExpandMetadataValueBestEffortLeaveEscaped(this, unevaluatedValue, metadatumXml.Location);

            metadatum = new ProjectMetadata(this, metadatumXml, evaluatedValueEscaped, null /* predecessor unknown */);

            _metadata.Set(metadatum);

            return metadatum;
        }

        #region IItemDefinition Members

        /// <summary>
        /// Sets a new metadata value on the ItemDefinition.
        /// This is ONLY called during evaluation and does not affect the XML.
        /// </summary>
        ProjectMetadata IItemDefinition<ProjectMetadata>.SetMetadata(ProjectMetadataElement metadataElement, string evaluatedValue, ProjectMetadata predecessor)
        {
            _metadata = _metadata ?? new PropertyDictionary<ProjectMetadata>();

            ProjectMetadata metadatum = new ProjectMetadata(this, metadataElement, evaluatedValue, predecessor);
            _metadata.Set(metadatum);

            return metadatum;
        }

        #endregion

        #region IMetadataTable Members

        /// <summary>
        /// Retrieves the value of the named metadatum.
        /// </summary>
        /// <param name="name">The metadatum to retrieve.</param>
        /// <returns>The value, or an empty string if there is none by that name.</returns>
        string IMetadataTable.GetEscapedValue(string name)
        {
            return ((IMetadataTable)this).GetEscapedValue(null, name);
        }

        /// <summary>
        /// Retrieves the value of the named metadatum.
        /// </summary>
        /// <param name="specifiedItemType">The type of item.</param>
        /// <param name="name">The metadatum to retrieve.</param>
        /// <returns>The value, or an empty string if there is none by that name.</returns>
        string IMetadataTable.GetEscapedValue(string specifiedItemType, string name)
        {
            return ((IMetadataTable)this).GetEscapedValueIfPresent(specifiedItemType, name) ?? String.Empty;
        }

        /// <summary>
        /// Retrieves the value of the named metadatum, or null if it doesn't exist
        /// </summary>
        /// <param name="specifiedItemType">The type of item.</param>
        /// <param name="name">The metadatum to retrieve.</param>
        /// <returns>The value, or null if there is none by that name.</returns>
        string IMetadataTable.GetEscapedValueIfPresent(string specifiedItemType, string name)
        {
            if (_metadata == null)
            {
                return null;
            }

            if (specifiedItemType == null || String.Equals(_itemType, specifiedItemType, StringComparison.OrdinalIgnoreCase))
            {
                ProjectMetadata metadatum = GetMetadata(name);
                if (metadatum != null)
                {
                    return metadatum.EvaluatedValueEscaped;
                }
            }

            return null;
        }

        #endregion
    }
}
