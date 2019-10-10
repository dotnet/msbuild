// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------

using Microsoft.Build.ObjectModelRemoting;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Construction
{
    /// <summary>
    ///     ProjectSdkElement represents the Sdk element within the MSBuild project.
    /// </summary>
    public class ProjectSdkElement : ProjectElementContainer
    {
        /// <summary>
        /// External projects support
        /// </summary>
        internal ProjectSdkElement(ProjectElementContainerLink link)
            : base(link)
        {
        }

        /// <summary>
        ///     Initialize a parented ProjectSdkElement
        /// </summary>
        internal ProjectSdkElement(XmlElementWithLocation xmlElement, ProjectRootElement parent,
            ProjectRootElement containingProject)
            : base(xmlElement, parent, containingProject)
        {
            ErrorUtilities.VerifyThrowArgumentNull(parent, nameof(parent));
        }

        /// <summary>
        ///     Initialize an non-parented ProjectSdkElement
        /// </summary>
        private ProjectSdkElement(XmlElementWithLocation xmlElement, ProjectRootElement containingProject)
            : base(xmlElement, null, containingProject)
        { }

        /// <summary>
        /// Gets or sets the name of the SDK.
        /// </summary>
        public string Name
        {
            get => GetAttributeValue(XMakeAttributes.sdkName);
            set
            {
                ErrorUtilities.VerifyThrowArgumentLength(value, XMakeAttributes.sdkName);
                SetOrRemoveAttribute(XMakeAttributes.sdkName, value, $"Set SDK Name to {value}", XMakeAttributes.sdkName);
            }
        }

        /// <summary>
        /// Gets or sets the version of the SDK.
        /// </summary>
        public string Version
        {
            get => GetAttributeValue(XMakeAttributes.sdkVersion);
            set
            {
                SetOrRemoveAttribute(XMakeAttributes.sdkVersion, value, $"Set SDK Version to {value}", XMakeAttributes.sdkVersion);
            }
        }

        /// <summary>
        /// Gets or sets the minimum version of the SDK required to build the project.
        /// </summary>
        public string MinimumVersion
        {
            get => GetAttributeValue(XMakeAttributes.sdkMinimumVersion);
            set
            {
                SetOrRemoveAttribute(XMakeAttributes.sdkMinimumVersion, value, $"Set SDK MinimumVersion to {value}", XMakeAttributes.sdkMinimumVersion);
            }
        }

        /// <inheritdoc />
        internal override void VerifyThrowInvalidOperationAcceptableLocation(ProjectElementContainer parent,
            ProjectElement previousSibling, ProjectElement nextSibling)
        {
            ErrorUtilities.VerifyThrowInvalidOperation(parent is ProjectRootElement, "OM_CannotAcceptParent");
        }

        /// <inheritdoc />
        protected override ProjectElement CreateNewInstance(ProjectRootElement owner)
        {
            return owner.CreateProjectSdkElement(Name, Version);
        }

        /// <summary>
        ///     Creates a non-parented ProjectSdkElement, wrapping an non-parented XmlElement.
        ///     Caller should then ensure the element is added to a parent
        /// </summary>
        internal static ProjectSdkElement CreateDisconnected(string sdkName, string sdkVersion,
            ProjectRootElement containingProject)
        {
            var element = containingProject.CreateElement(XMakeElements.sdk);

            var sdkElement = new ProjectSdkElement(element, containingProject)
            {
                Name = sdkName,
                Version = sdkVersion
            };

            return sdkElement;
        }
    }
}
