// Licensed to the .NET Foundation under one or more agreements.
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
    /// ProjectWhenElement represents the When element in the MSBuild project.
    /// </summary>
    [DebuggerDisplay("#Children={Count} Condition={Condition}")]
    public class ProjectWhenElement : ProjectElementContainer
    {
        /// <summary>
        /// External projects support
        /// </summary>
        internal ProjectWhenElement(ProjectWhenElementLink link)
            : base(link)
        {
        }

        /// <summary>
        /// Initialize a parented ProjectWhenElement
        /// </summary>
        internal ProjectWhenElement(XmlElement xmlElement, ProjectChooseElement parent, ProjectRootElement containingProject)
            : base(xmlElement, parent, containingProject)
        {
            ErrorUtilities.VerifyThrowArgumentNull(parent, nameof(parent));
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
        public ICollection<ProjectChooseElement> ChooseElements => GetChildrenOfType<ProjectChooseElement>();

        /// <summary>
        /// Get an enumerator over any child item groups
        /// </summary>
        public ICollection<ProjectItemGroupElement> ItemGroups => GetChildrenOfType<ProjectItemGroupElement>();

        /// <summary>
        /// Get an enumerator over any child property groups
        /// </summary>
        public ICollection<ProjectPropertyGroupElement> PropertyGroups => GetChildrenOfType<ProjectPropertyGroupElement>();

        #endregion

        /// <summary>
        /// Creates an unparented ProjectPropertyGroupElement, wrapping an unparented XmlElement.
        /// Caller should then ensure the element is added to a parent
        /// </summary>
        internal static ProjectWhenElement CreateDisconnected(string condition, ProjectRootElement containingProject)
        {
            XmlElementWithLocation element = containingProject.CreateElement(XMakeElements.when);

            var when = new ProjectWhenElement(element, containingProject) { Condition = condition };

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
            return owner.CreateWhenElement(Condition);
        }
    }
}
