﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Build.ObjectModelRemoting;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Construction
{
    /// <summary>
    /// ProjectPropertyGroupElement represents the PropertyGroup element in the MSBuild project.
    /// </summary>
    [DebuggerDisplay("#Properties={Count} Condition={Condition} Label={Label}")]
    public class ProjectPropertyGroupElement : ProjectElementContainer
    {
        /// <summary>
        /// External projects support
        /// </summary>
        internal ProjectPropertyGroupElement(ProjectPropertyGroupElementLink link)
            : base(link)
        {
        }

        /// <summary>
        /// Initialize a parented ProjectPropertyGroupElement
        /// </summary>
        internal ProjectPropertyGroupElement(XmlElementWithLocation xmlElement, ProjectElementContainer parent, ProjectRootElement containingProject)
            : base(xmlElement, parent, containingProject)
        {
            ErrorUtilities.VerifyThrowArgumentNull(parent, nameof(parent));
        }

        /// <summary>
        /// Initialize an unparented ProjectPropertyGroupElement
        /// </summary>
        private ProjectPropertyGroupElement(XmlElementWithLocation xmlElement, ProjectRootElement containingProject)
            : base(xmlElement, null, containingProject)
        {
        }

        /// <summary>
        /// Get any contained properties.
        /// </summary>
        public ICollection<ProjectPropertyElement> Properties => GetChildrenOfType<ProjectPropertyElement>();

        /// <summary>
        /// Get any contained properties.
        /// </summary>
        public ICollection<ProjectPropertyElement> PropertiesReversed => GetChildrenReversedOfType<ProjectPropertyElement>();

        /// <summary>
        /// Convenience method that picks a location based on a heuristic:
        /// Adds a new property after the last property in this property group.
        /// </summary>
        public ProjectPropertyElement AddProperty(string name, string unevaluatedValue)
        {
            ErrorUtilities.VerifyThrowArgumentLength(name, nameof(name));
            ErrorUtilities.VerifyThrowArgumentNull(unevaluatedValue, nameof(unevaluatedValue));

            ProjectPropertyElement newProperty = ContainingProject.CreatePropertyElement(name);
            newProperty.Value = unevaluatedValue;
            AppendChild(newProperty);

            return newProperty;
        }

        /// <summary>
        /// Convenience method that picks a location based on a heuristic:
        /// If there is an existing property with the same name and no condition,
        /// updates its value. Otherwise it adds a new property after the last property.
        /// </summary>
        public ProjectPropertyElement SetProperty(string name, string unevaluatedValue)
        {
            ErrorUtilities.VerifyThrowArgumentLength(name, nameof(name));
            ErrorUtilities.VerifyThrowArgumentNull(unevaluatedValue, nameof(unevaluatedValue));

            foreach (ProjectPropertyElement property in Properties)
            {
                if (String.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase) && property.Condition.Length == 0)
                {
                    property.Value = unevaluatedValue;
                    return property;
                }
            }

            return AddProperty(name, unevaluatedValue);
        }

        /// <summary>
        /// Creates an unparented ProjectPropertyGroupElement, wrapping an unparented XmlElement.
        /// Caller should then ensure the element is added to a parent
        /// </summary>
        internal static ProjectPropertyGroupElement CreateDisconnected(ProjectRootElement containingProject)
        {
            XmlElementWithLocation element = containingProject.CreateElement(XMakeElements.propertyGroup);

            return new ProjectPropertyGroupElement(element, containingProject);
        }

        /// <summary>
        /// Overridden to verify that the potential parent and siblings
        /// are acceptable. Throws InvalidOperationException if they are not.
        /// </summary>
        internal override void VerifyThrowInvalidOperationAcceptableLocation(ProjectElementContainer parent, ProjectElement previousSibling, ProjectElement nextSibling)
        {
            ErrorUtilities.VerifyThrowInvalidOperation(parent is ProjectRootElement || parent is ProjectTargetElement || parent is ProjectWhenElement || parent is ProjectOtherwiseElement, "OM_CannotAcceptParent");
        }

        /// <inheritdoc />
        protected override ProjectElement CreateNewInstance(ProjectRootElement owner)
        {
            return owner.CreatePropertyGroupElement();
        }
    }
}
