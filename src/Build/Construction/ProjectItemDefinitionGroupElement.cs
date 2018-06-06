// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Definition of ProjectItemDefinitionGroupElement class.</summary>
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
    /// ProjectItemDefinitionGroupElement represents the ItemGroup element in the MSBuild project.
    /// </summary>
    [DebuggerDisplay("#ItemDefinitions={Count} Condition={Condition} Label={Label}")]
    public class ProjectItemDefinitionGroupElement : ProjectElementContainer
    {
        /// <summary>
        /// Initialize a parented ProjectItemDefinitionGroupElement
        /// </summary>
        internal ProjectItemDefinitionGroupElement(XmlElement xmlElement, ProjectRootElement parent, ProjectRootElement containingProject)
            : base(xmlElement, parent, containingProject)
        {
            ErrorUtilities.VerifyThrowArgumentNull(parent, "parent");
        }

        /// <summary>
        /// Initialize an unparented ProjectItemDefinitionGroupElement
        /// </summary>
        private ProjectItemDefinitionGroupElement(XmlElement xmlElement, ProjectRootElement containingProject)
            : base(xmlElement, null, containingProject)
        {
        }

        /// <summary>
        /// Get a list of child item definitions.
        /// </summary>
        public ICollection<ProjectItemDefinitionElement> ItemDefinitions
        {
            get
            {
                return new ReadOnlyCollection<ProjectItemDefinitionElement>(Children.OfType<ProjectItemDefinitionElement>());
            }
        }

        /// <summary>
        /// Convenience method that picks a location based on a heuristic:
        /// Adds a new item definition after the last child.
        /// </summary>
        public ProjectItemDefinitionElement AddItemDefinition(string itemType)
        {
            ProjectItemDefinitionElement itemDefinition = ContainingProject.CreateItemDefinitionElement(itemType);

            AppendChild(itemDefinition);

            return itemDefinition;
        }

        /// <summary>
        /// Creates an unparented ProjectItemDefinitionGroupElement, wrapping an unparented XmlElement.
        /// Caller should then ensure the element is added to a parent
        /// </summary>
        internal static ProjectItemDefinitionGroupElement CreateDisconnected(ProjectRootElement containingProject)
        {
            XmlElementWithLocation element = containingProject.CreateElement(XMakeElements.itemDefinitionGroup);

            return new ProjectItemDefinitionGroupElement(element, containingProject);
        }

        /// <summary>
        /// Overridden to verify that the potential parent and siblings
        /// are acceptable. Throws InvalidOperationException if they are not.
        /// </summary>
        internal override void VerifyThrowInvalidOperationAcceptableLocation(ProjectElementContainer parent, ProjectElement previousSibling, ProjectElement nextSibling)
        {
            ErrorUtilities.VerifyThrowInvalidOperation(parent is ProjectRootElement, "OM_CannotAcceptParent");
        }

        /// <inheritdoc />
        protected override ProjectElement CreateNewInstance(ProjectRootElement owner)
        {
            return owner.CreateItemDefinitionGroupElement();
        }
    }
}
