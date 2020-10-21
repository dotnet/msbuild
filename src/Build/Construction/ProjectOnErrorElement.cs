// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using Microsoft.Build.ObjectModelRemoting;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Construction
{
    /// <summary>
    /// ProjectUsingTaskElement represents the Import element in the MSBuild project.
    /// </summary>
    [DebuggerDisplay("ExecuteTargetsAttribute={ExecuteTargetsAttribute}")]
    public class ProjectOnErrorElement : ProjectElement
    {
        /// <summary>
        /// External projects support
        /// </summary>
        internal ProjectOnErrorElement(ProjectOnErrorElementLink link)
            : base(link)
        {
        }

        /// <summary>
        /// Initialize a parented ProjectOnErrorElement
        /// </summary>
        internal ProjectOnErrorElement(XmlElementWithLocation xmlElement, ProjectTargetElement parent, ProjectRootElement project)
            : base(xmlElement, parent, project)
        {
            ErrorUtilities.VerifyThrowArgumentNull(parent, nameof(parent));
        }

        /// <summary>
        /// Initialize an unparented ProjectOnErrorElement
        /// </summary>
        private ProjectOnErrorElement(XmlElementWithLocation xmlElement, ProjectRootElement project)
            : base(xmlElement, null, project)
        {
        }

        /// <summary>
        /// Gets and sets the value of the ExecuteTargets attribute.
        /// </summary>
        /// <remarks>
        /// 'Attribute' suffix is for clarity.
        /// </remarks>
        public string ExecuteTargetsAttribute
        {
            [DebuggerStepThrough]
            get
            {
                return GetAttributeValue(XMakeAttributes.executeTargets);
            }

            set
            {
                ErrorUtilities.VerifyThrowArgumentLength(value, XMakeAttributes.executeTargets);
                SetOrRemoveAttribute(XMakeAttributes.executeTargets, value, "Set OnError ExecuteTargets {0}", value);
            }
        }

        /// <summary>
        /// Location of the "ExecuteTargets" attribute on this element, if any.
        /// If there is no such attribute, returns null;
        /// </summary>
        public ElementLocation ExecuteTargetsLocation => GetAttributeLocation(XMakeAttributes.executeTargets);

        /// <summary>
        /// Creates an unparented ProjectOnErrorElement, wrapping an unparented XmlElement.
        /// Caller should then ensure the element is added to a parent.
        /// </summary>
        internal static ProjectOnErrorElement CreateDisconnected(string executeTargets, ProjectRootElement containingProject)
        {
            XmlElementWithLocation element = containingProject.CreateElement(XMakeElements.onError);

            var onError = new ProjectOnErrorElement(element, containingProject) { ExecuteTargetsAttribute = executeTargets };
            
            return onError;
        }

        /// <summary>
        /// Overridden to verify that the potential parent and siblings
        /// are acceptable. Throws InvalidOperationException if they are not.
        /// </summary>
        internal override void VerifyThrowInvalidOperationAcceptableLocation(ProjectElementContainer parent, ProjectElement previousSibling, ProjectElement nextSibling)
        {
            ErrorUtilities.VerifyThrowInvalidOperation(parent is ProjectTargetElement, "OM_CannotAcceptParent");
        }

        /// <inheritdoc />
        protected override ProjectElement CreateNewInstance(ProjectRootElement owner)
        {
            return owner.CreateOnErrorElement(ExecuteTargetsAttribute);
        }
    }
}
