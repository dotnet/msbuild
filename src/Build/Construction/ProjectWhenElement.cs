// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Definition of ProjectWhenElement class.</summary>
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
    /// ProjectWhenElement represents the When element in the MSBuild project.
    /// </summary>
    [DebuggerDisplay("#Children={Count} Condition={Condition}")]
    public class ProjectWhenElement : ProjectElementContainer
    {
        /// <summary>
        /// Initialize a parented ProjectWhenElement
        /// </summary>
        internal ProjectWhenElement(XmlElement xmlElement, ProjectChooseElement parent, ProjectRootElement containingProject)
            : base(xmlElement, parent, containingProject)
        {
            ErrorUtilities.VerifyThrowArgumentNull(parent, "parent");
        }

        /// <summary>
        /// Initialize an unparented ProjectWhenElement
        /// </summary>
        private ProjectWhenElement(XmlElement xmlElement, ProjectRootElement containingProject)
            : base(xmlElement, null, containingProject)
        {
        }

        #region ChildEnumerators
        /// <summary>
        /// Get an enumerator over any child chooses
        /// </summary>
        public ICollection<ProjectChooseElement> ChooseElements
        {
            get
            {
                return new ReadOnlyCollection<ProjectChooseElement>(Children.OfType<ProjectChooseElement>());
            }
        }

        /// <summary>
        /// Get an enumerator over any child item groups
        /// </summary>
        public ICollection<ProjectItemGroupElement> ItemGroups
        {
            get
            {
                return new ReadOnlyCollection<ProjectItemGroupElement>(Children.OfType<ProjectItemGroupElement>());
            }
        }

        /// <summary>
        /// Get an enumerator over any child property groups
        /// </summary>
        public ICollection<ProjectPropertyGroupElement> PropertyGroups
        {
            get
            {
                return new ReadOnlyCollection<ProjectPropertyGroupElement>(Children.OfType<ProjectPropertyGroupElement>());
            }
        }
        #endregion

        /// <summary>
        /// Creates an unparented ProjectPropertyGroupElement, wrapping an unparented XmlElement.
        /// Caller should then ensure the element is added to a parent
        /// </summary>
        internal static ProjectWhenElement CreateDisconnected(string condition, ProjectRootElement containingProject)
        {
            XmlElementWithLocation element = containingProject.CreateElement(XMakeElements.when);

            ProjectWhenElement when = new ProjectWhenElement(element, containingProject);
            when.Condition = condition;

            return when;
        }

        /// <summary>
        /// Overridden to verify that the potential parent and siblings
        /// are acceptable. Throws InvalidOperationException if they are not.
        /// </summary>
        internal override void VerifyThrowInvalidOperationAcceptableLocation(ProjectElementContainer parent, ProjectElement previousSibling, ProjectElement nextSibling)
        {
            ErrorUtilities.VerifyThrowInvalidOperation(parent is ProjectChooseElement, "OM_CannotAcceptParent");
            ErrorUtilities.VerifyThrowInvalidOperation(!(previousSibling is ProjectOtherwiseElement), "OM_NoOtherwiseBeforeWhenOrOtherwise");
        }

        /// <inheritdoc />
        protected override ProjectElement CreateNewInstance(ProjectRootElement owner)
        {
            return owner.CreateWhenElement(this.Condition);
        }
    }
}
