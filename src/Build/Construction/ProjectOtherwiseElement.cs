﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Build.ObjectModelRemoting;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Construction
{
    /// <summary>
    /// ProjectOtherwiseElement represents the Otherwise element in the MSBuild project.
    /// </summary>
    [DebuggerDisplay("#Children={Count}")]
    public class ProjectOtherwiseElement : ProjectElementContainer
    {
        /// <summary>
        /// External projects support
        /// </summary>
        internal ProjectOtherwiseElement(ProjectOtherwiseElementLink link)
            : base(link)
        {
        }

        /// <summary>
        /// Initialize a parented ProjectOtherwiseElement
        /// </summary>
        internal ProjectOtherwiseElement(XmlElementWithLocation xmlElement, ProjectElementContainer parent, ProjectRootElement project)
            : base(xmlElement, parent, project)
        {
            ErrorUtilities.VerifyThrowArgumentNull(parent, nameof(parent));
        }

        /// <summary>
        /// Initialize an unparented ProjectOtherwiseElement
        /// </summary>
        private ProjectOtherwiseElement(XmlElementWithLocation xmlElement, ProjectRootElement project)
            : base(xmlElement, null, project)
        {
        }

        /// <summary>
        /// Condition should never be set, but the getter returns null instead of throwing 
        /// because a nonexistent condition is implicitly true
        /// </summary>
        public override string Condition
        {
            get => null;
            set => ErrorUtilities.ThrowInvalidOperation("OM_CannotGetSetCondition");
        }

        #region ChildEnumerators
        /// <summary>
        /// Get an enumerator over any child item groups
        /// </summary>
        public ICollection<ProjectItemGroupElement> ItemGroups => GetChildrenOfType<ProjectItemGroupElement>();

        /// <summary>
        /// Get an enumerator over any child property groups
        /// </summary>
        public ICollection<ProjectPropertyGroupElement> PropertyGroups => GetChildrenOfType<ProjectPropertyGroupElement>();

        /// <summary>
        /// Get an enumerator over any child chooses
        /// </summary>
        public ICollection<ProjectChooseElement> ChooseElements => GetChildrenOfType<ProjectChooseElement>();

        #endregion

        /// <summary>
        /// This does not allow conditions, so it should not be called.
        /// </summary>
        public override ElementLocation ConditionLocation
        {
            get
            {
                ErrorUtilities.ThrowInternalError("Should not evaluate this");
                return null;
            }
        }

        /// <summary>
        /// Creates an unparented ProjectOtherwiseElement, wrapping an unparented XmlElement.
        /// Caller should then ensure the element is added to a parent.
        /// </summary>
        internal static ProjectOtherwiseElement CreateDisconnected(ProjectRootElement containingProject)
        {
            XmlElementWithLocation element = containingProject.CreateElement(XMakeElements.otherwise);

            return new ProjectOtherwiseElement(element, containingProject);
        }

        /// <summary>
        /// Overridden to verify that the potential parent and siblings
        /// are acceptable. Throws InvalidOperationException if they are not.
        /// </summary>
        internal override void VerifyThrowInvalidOperationAcceptableLocation(ProjectElementContainer parent, ProjectElement previousSibling, ProjectElement nextSibling)
        {
            ErrorUtilities.VerifyThrowInvalidOperation(parent is ProjectChooseElement, "OM_CannotAcceptParent");
            ErrorUtilities.VerifyThrowInvalidOperation(!(nextSibling is ProjectWhenElement) && !(previousSibling is ProjectOtherwiseElement) && !(nextSibling is ProjectOtherwiseElement), "OM_NoOtherwiseBeforeWhenOrOtherwise");
        }

        /// <inheritdoc />
        protected override ProjectElement CreateNewInstance(ProjectRootElement owner)
        {
            return owner.CreateOtherwiseElement();
        }
    }
}
