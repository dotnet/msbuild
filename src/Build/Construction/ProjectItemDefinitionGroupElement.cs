﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Xml;
using Microsoft.Build.ObjectModelRemoting;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Construction
{
    /// <summary>
    /// ProjectItemDefinitionGroupElement represents the ItemGroup element in the MSBuild project.
    /// </summary>
    [DebuggerDisplay("#ItemDefinitions={Count} Condition={Condition} Label={Label}")]
    public class ProjectItemDefinitionGroupElement : ProjectElementContainer
    {
        /// <summary>
        /// External projects support
        /// </summary>
        internal ProjectItemDefinitionGroupElement(ProjectItemDefinitionGroupElementLink link)
            : base(link)
        {
        }

        /// <summary>
        /// Initialize a parented ProjectItemDefinitionGroupElement
        /// </summary>
        internal ProjectItemDefinitionGroupElement(XmlElement xmlElement, ProjectElementContainer parent, ProjectRootElement containingProject)
            : base(xmlElement, parent, containingProject)
        {
            ErrorUtilities.VerifyThrowArgumentNull(parent, nameof(parent));
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
        public ICollection<ProjectItemDefinitionElement> ItemDefinitions => GetChildrenOfType<ProjectItemDefinitionElement>();

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
