// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Definition of ProjectPropertyGroupElement class.</summary>
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
    /// ProjectPropertyGroupElement represents the PropertyGroup element in the MSBuild project.
    /// </summary>
    [DebuggerDisplay("#Properties={Count} Condition={Condition} Label={Label}")]
    public class ProjectPropertyGroupElement : ProjectElementContainer
    {
        /// <summary>
        /// Initialize a parented ProjectPropertyGroupElement
        /// </summary>
        internal ProjectPropertyGroupElement(XmlElementWithLocation xmlElement, ProjectElementContainer parent, ProjectRootElement containingProject)
            : base(xmlElement, parent, containingProject)
        {
            ErrorUtilities.VerifyThrowArgumentNull(parent, "parent");
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
        public ICollection<ProjectPropertyElement> Properties
        {
            get
            {
                return new ReadOnlyCollection<ProjectPropertyElement>(Children.OfType<ProjectPropertyElement>());
            }
        }

        /// <summary>
        /// Get any contained properties.
        /// </summary>
        public ICollection<ProjectPropertyElement> PropertiesReversed
        {
            get
            {
                return new ReadOnlyCollection<ProjectPropertyElement>(ChildrenReversed.OfType<ProjectPropertyElement>());
            }
        }

        /// <summary>
        /// Convenience method that picks a location based on a heuristic:
        /// Adds a new property after the last property in this property group.
        /// </summary>
        public ProjectPropertyElement AddProperty(string name, string unevaluatedValue)
        {
            ErrorUtilities.VerifyThrowArgumentLength(name, "name");
            ErrorUtilities.VerifyThrowArgumentNull(unevaluatedValue, "unevaluatedValue");

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
            ErrorUtilities.VerifyThrowArgumentLength(name, "name");
            ErrorUtilities.VerifyThrowArgumentNull(unevaluatedValue, "unevaluatedValue");

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
