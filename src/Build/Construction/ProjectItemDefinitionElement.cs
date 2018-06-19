// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Definition of ProjectItemDefinitionElement class.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Xml;
using System.Diagnostics;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Collections;

using ProjectXmlUtilities = Microsoft.Build.Internal.ProjectXmlUtilities;

namespace Microsoft.Build.Construction
{
    /// <summary>
    /// ProjectItemDefinitionElement class represents the Item Definition element in the MSBuild project.
    /// </summary>
    [DebuggerDisplay("{ItemType} #Metadata={Count} Condition={Condition}")]
    public class ProjectItemDefinitionElement : ProjectElementContainer
    {
        /// <summary>
        /// Initialize a ProjectItemDefinitionElement instance from a node read from a project file
        /// </summary>
        internal ProjectItemDefinitionElement(XmlElement xmlElement, ProjectItemDefinitionGroupElement parent, ProjectRootElement containingProject)
            : base(xmlElement, parent, containingProject)
        {
            ErrorUtilities.VerifyThrowArgumentNull(parent, "parent");
        }

        /// <summary>
        /// Initialize a ProjectItemDefinitionElement instance from a node read from a project file
        /// </summary>
        private ProjectItemDefinitionElement(XmlElement xmlElement, ProjectRootElement containingProject)
            : base(xmlElement, null, containingProject)
        {
        }

        /// <summary>
        /// Gets the definition's type.
        /// </summary>
        public string ItemType
        {
            get { return XmlElement.Name; }
        }

        /// <summary>
        /// Get any child metadata definitions.
        /// </summary>
        public ICollection<ProjectMetadataElement> Metadata
        {
            get
            {
                return new ReadOnlyCollection<ProjectMetadataElement>(Children.OfType<ProjectMetadataElement>());
            }
        }

        /// <summary>
        /// Convenience method to add a piece of metadata to this item definition.
        /// Adds after any existing metadata. Does not modify any existing metadata.
        /// </summary>
        public ProjectMetadataElement AddMetadata(string name, string unevaluatedValue)
        {
            ErrorUtilities.VerifyThrowArgumentLength(name, "name");
            ErrorUtilities.VerifyThrowArgumentNull(unevaluatedValue, "unevaluatedValue");

            ProjectMetadataElement metadata = ContainingProject.CreateMetadataElement(name);
            metadata.Value = unevaluatedValue;

            AppendChild(metadata);

            return metadata;
        }

        /// <summary>
        /// Creates an unparented ProjectItemDefinitionElement, wrapping an unparented XmlElement.
        /// Caller should then ensure the element is added to a parent.
        /// </summary>
        internal static ProjectItemDefinitionElement CreateDisconnected(string itemType, ProjectRootElement containingProject)
        {
            XmlUtilities.VerifyThrowArgumentValidElementName(itemType);

            // Orcas inadvertently did not check for reserved item types (like "Choose") in item definitions,
            // as we do for item types in item groups. So we do not have a check here.
            // Although we could perhaps add one, as such item definitions couldn't be used 
            // since no items can have the reserved itemType.
            XmlElementWithLocation element = containingProject.CreateElement(itemType);

            return new ProjectItemDefinitionElement(element, containingProject);
        }

        /// <summary>
        /// Overridden to verify that the potential parent and siblings
        /// are acceptable. Throws InvalidOperationException if they are not.
        /// </summary>
        internal override void VerifyThrowInvalidOperationAcceptableLocation(ProjectElementContainer parent, ProjectElement previousSibling, ProjectElement nextSibling)
        {
            ErrorUtilities.VerifyThrowInvalidOperation(parent is ProjectItemDefinitionGroupElement, "OM_CannotAcceptParent");
        }

        /// <inheritdoc />
        protected override ProjectElement CreateNewInstance(ProjectRootElement owner)
        {
            return owner.CreateItemDefinitionElement(this.ItemType);
        }
    }
}
