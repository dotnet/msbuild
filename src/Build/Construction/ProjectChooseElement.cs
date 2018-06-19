// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Definition of ProjectChooseElement class.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Xml;
using System.Diagnostics;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Collections;

namespace Microsoft.Build.Construction
{
    /// <summary>
    /// ProjectChooseElement represents the Choose element in the MSBuild project.
    /// Currently it does not allow a Condition.
    /// </summary>
    [DebuggerDisplay("ProjectChooseElement (#Children={Count} HasOtherwise={OtherwiseElement != null})")]
    public class ProjectChooseElement : ProjectElementContainer
    {
        /// <summary>
        /// Initialize a parented ProjectChooseElement
        /// </summary>
        internal ProjectChooseElement(XmlElement xmlElement, ProjectElementContainer parent, ProjectRootElement containingProject)
            : base(xmlElement, parent, containingProject)
        {
            ErrorUtilities.VerifyThrowArgumentNull(parent, "parent");
        }

        /// <summary>
        /// Initialize an unparented ProjectChooseElement
        /// </summary>
        private ProjectChooseElement(XmlElement xmlElement, ProjectRootElement containingProject)
            : base(xmlElement, null, containingProject)
        {
        }

        /// <summary>
        /// Condition should never be set, but the getter returns null instead of throwing 
        /// because a nonexistent condition is implicitly true
        /// </summary>
        public override string Condition
        {
            get
            {
                return null;
            }

            set
            {
                ErrorUtilities.ThrowInvalidOperation("OM_CannotGetSetCondition");
            }
        }

        #region ChildEnumerators
        /// <summary>
        /// Get the When children.
        /// Will contain at least one entry.
        /// </summary>
        public ICollection<ProjectWhenElement> WhenElements
        {
            get
            {
                return new ReadOnlyCollection<ProjectWhenElement>(Children.OfType<ProjectWhenElement>());
            }
        }

        /// <summary>
        /// Get any Otherwise child.
        /// May be null.
        /// </summary>
        public ProjectOtherwiseElement OtherwiseElement
        {
            get
            {
                ProjectOtherwiseElement otherwise = (LastChild == null) ? null : LastChild as ProjectOtherwiseElement;

                return otherwise;
            }
        }
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
        /// Creates an unparented ProjectChooseElement, wrapping an unparented XmlElement.
        /// Caller should then ensure the element is added to a parent
        /// </summary>
        internal static ProjectChooseElement CreateDisconnected(ProjectRootElement containingProject)
        {
            XmlElementWithLocation element = containingProject.CreateElement(XMakeElements.choose);

            return new ProjectChooseElement(element, containingProject);
        }

        /// <summary>
        /// Sets the parent of this element if it is a valid parent,
        /// otherwise throws.
        /// </summary>
        internal override void VerifyThrowInvalidOperationAcceptableLocation(ProjectElementContainer parent, ProjectElement previousSibling, ProjectElement nextSibling)
        {
            ErrorUtilities.VerifyThrowInvalidOperation(parent is ProjectRootElement || parent is ProjectWhenElement || parent is ProjectOtherwiseElement, "OM_CannotAcceptParent");

            int nestingDepth = 0;
            ProjectElementContainer immediateParent = parent;

            while (parent != null)
            {
                parent = parent.Parent;

                nestingDepth++;

                // This should really be an OM error, with no error number. But it's so obscure, it's not worth a new string.
                ProjectErrorUtilities.VerifyThrowInvalidProject(nestingDepth <= ProjectParser.MaximumChooseNesting, immediateParent.Location, "ChooseOverflow", ProjectParser.MaximumChooseNesting);
            }
        }

        /// <inheritdoc />
        protected override ProjectElement CreateNewInstance(ProjectRootElement owner)
        {
            return owner.CreateChooseElement();
        }
    }
}
