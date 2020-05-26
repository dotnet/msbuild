// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------

#nullable enable

using Microsoft.Build.ObjectModelRemoting;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Construction
{
    /// <summary>
    ///     ProjectSdkElement represents the Sdk element within the MSBuild project.
    /// </summary>
    public class ProjectSdkElement : ProjectElementContainer, ISdkReferenceMutableSource
    {
        private static readonly SdkReferenceAttribute NameAttributeFactory =
            new SdkReferenceAttribute(
                XMakeAttributes.sdkName, "Set SDK Name to {0}"
            );

        private static readonly SdkReferenceAttribute VersionAttributeFactory =
            new SdkReferenceAttribute(
                XMakeAttributes.sdkVersion, "Set SDK Version to {0}"
            );

        private static readonly SdkReferenceAttribute MinimumVersionAttributeFactory =
            new SdkReferenceAttribute(
                XMakeAttributes.sdkMinimumVersion, "Set SDK MinimumVersion to {0}"
            );

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
        {
        }

        /// <summary>
        /// Gets or sets the name of the SDK.
        /// </summary>
        public string Name
        {
            get => GetAttributeValue(XMakeAttributes.sdkName, true);
            set
            {
                ErrorUtilities.VerifyThrowArgumentLength(value, XMakeAttributes.sdkName);
                SetOrRemoveAttribute(XMakeAttributes.sdkName, value,
                                     NameAttributeFactory.ChangeReasonMessage, XMakeAttributes.sdkName);
            }
        }

        /// <summary>
        /// Gets or sets the version of the SDK.
        /// </summary>
        public string? Version
        {
            get => GetAttributeValue(XMakeAttributes.sdkVersion, true);
            set => SetOrRemoveAttribute(XMakeAttributes.sdkVersion, value,
                                        VersionAttributeFactory.ChangeReasonMessage, XMakeAttributes.sdkVersion);
        }

        /// <summary>
        /// Gets or sets the minimum version of the SDK required to build the project.
        /// </summary>
        public string? MinimumVersion
        {
            get => GetAttributeValue(XMakeAttributes.sdkMinimumVersion, true);
            set => SetOrRemoveAttribute(XMakeAttributes.sdkMinimumVersion, value,
                                        MinimumVersionAttributeFactory.ChangeReasonMessage,
                                        XMakeAttributes.sdkMinimumVersion);
        }

#nullable restore

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

#nullable enable

        /// <summary>
        ///     Creates a non-parented ProjectSdkElement, wrapping an non-parented XmlElement.
        ///     Caller should then ensure the element is added to a parent
        /// </summary>
        internal static ProjectSdkElement CreateDisconnected(string sdkName, string sdkVersion,
            ProjectRootElement containingProject)
        {
            var element = containingProject.CreateElement(XMakeElements.sdk);

            return new ProjectSdkElement(element, containingProject)
            {
                Name = sdkName,
                Version = sdkVersion
            };
        }

        SdkReferenceSourceQuery ISdkReferenceMutableSource.SdkReferenceNameQuery =>
            new SdkReferenceSourceQuery(this, NameAttributeFactory);

        SdkReferenceSourceQuery ISdkReferenceMutableSource.SdkReferenceVersionQuery =>
            new SdkReferenceSourceQuery(this, VersionAttributeFactory);

        SdkReferenceSourceQuery ISdkReferenceMutableSource.SdkReferenceMinimumVersionQuery =>
            new SdkReferenceSourceQuery(this, MinimumVersionAttributeFactory);

        SdkReferenceSourceFullQuery ISdkReferenceMutableSource.SdkReferenceFullQuery =>
            new SdkReferenceSourceFullQuery(
                this, NameAttributeFactory, VersionAttributeFactory, MinimumVersionAttributeFactory
            );
    }
}
