// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.ObjectModelRemoting;
using Microsoft.Build.Shared;

using ProjectXmlUtilities = Microsoft.Build.Internal.ProjectXmlUtilities;

namespace Microsoft.Build.Construction
{
    /// <summary>
    /// Initializes a ProjectImportElement instance.
    /// </summary>
    [DebuggerDisplay("Project={Project} Condition={Condition}")]
    public class ProjectImportElement : ProjectElement
    {
        internal ProjectImportElementLink ImportLink => (ProjectImportElementLink)Link;

        private ImplicitImportLocation _implicitImportLocation;
        private ProjectElement _originalElement;

        /// <summary>
        /// External projects support
        /// </summary>
        internal ProjectImportElement(ProjectImportElementLink link)
            : base(link)
        {
        }

        /// <summary>
        /// Initialize a parented ProjectImportElement
        /// </summary>
        internal ProjectImportElement(XmlElementWithLocation xmlElement, ProjectElementContainer parent, ProjectRootElement containingProject, SdkReference sdkReference = null)
            : base(xmlElement, parent, containingProject)
        {
            ErrorUtilities.VerifyThrowArgumentNull(parent, nameof(parent));
            ParsedSdkReference = sdkReference;
        }

        /// <summary>
        /// Initialize an unparented ProjectImportElement
        /// </summary>
        internal ProjectImportElement(XmlElementWithLocation xmlElement, ProjectRootElement containingProject)
            : base(xmlElement, null, containingProject)
        {
        }

        /// <summary>
        /// Gets or sets the Project value. 
        /// </summary>
        public string Project
        {
            get => FileUtilities.FixFilePath(GetAttributeValue(XMakeAttributes.project));
            set
            {
                ErrorUtilities.VerifyThrowArgumentLength(value, XMakeAttributes.project);

                SetOrRemoveAttribute(XMakeAttributes.project, value, "Set Import Project {0}", value);
            }
        }

        /// <summary>
        /// Location of the project attribute
        /// </summary>
        public ElementLocation ProjectLocation => GetAttributeLocation(XMakeAttributes.project);

        /// <summary>
        /// Gets or sets the SDK that contains the import.
        /// </summary>
        public string Sdk
        {
            get => FileUtilities.FixFilePath(GetAttributeValue(XMakeAttributes.sdk));
            set
            {
                ErrorUtilities.VerifyThrowArgumentLength(value, XMakeAttributes.sdk);
                // TODO: here and other setters. Confirm that CheckUpdatedSdk() ussage is
                //       really the intended one. Currently it does check for equality  before updating the attribute
                //       so setter will only take effect if "ParsedSdkReference" is not updated. Currently it might work by accident
                //       if all "Set" calls are different and no ParsedSdkReference is triggered in between.
                if (!CheckUpdatedSdk()) return;
                SetOrRemoveAttribute(XMakeAttributes.sdk, value, "Set Import Sdk {0}", value);
            }
        }

        /// <summary>
        /// Gets or sets the version associated with this SDK import
        /// </summary>
        public string Version
        {
            get => GetAttributeValue(XMakeAttributes.sdkVersion);
            set
            {
                if (!CheckUpdatedSdk()) return;
                SetOrRemoveAttribute(XMakeAttributes.sdkVersion, value, "Set Import Version {0}", value);
            }
        }

        /// <summary>
        /// Gets or sets the minimum SDK version required by this import.
        /// </summary>
        public string MinimumVersion
        {
            get => GetAttributeValue(XMakeAttributes.sdkMinimumVersion);
            set
            {
                if (!CheckUpdatedSdk()) return;
                SetOrRemoveAttribute(XMakeAttributes.sdkMinimumVersion, value, "Set Import Minimum Version {0}", value);
            }
        }

        /// <summary>
        /// Location of the Sdk attribute
        /// </summary>
        public ElementLocation SdkLocation => GetAttributeLocation(XMakeAttributes.sdk);

        /// <summary>
        /// Gets the <see cref="ImplicitImportLocation"/> of the import.  This indicates if the import was implicitly
        /// added because of the <see cref="ProjectRootElement.Sdk"/> attribute and the location where the project was
        /// imported.
        /// </summary>
        public ImplicitImportLocation ImplicitImportLocation { get => Link != null ? ImportLink.ImplicitImportLocation : _implicitImportLocation; internal set => _implicitImportLocation = value; }

        /// <summary>
        /// If the import is an implicit one (<see cref="ImplicitImportLocation"/> != None) then this element points
        /// to the original element which generated this implicit import.
        /// </summary>
        public ProjectElement OriginalElement { get => Link != null ? ImportLink.OriginalElement : _originalElement; internal set => _originalElement = value; }


        /// <summary>
        /// <see cref="SdkReference"/> if applicable to this import element.
        /// </summary>
        internal SdkReference ParsedSdkReference { get; set; }

        /// <summary>
        /// Creates an unparented ProjectImportElement, wrapping an unparented XmlElement.
        /// Validates the project value.
        /// Caller should then ensure the element is added to a parent
        /// </summary>
        internal static ProjectImportElement CreateDisconnected(string project, ProjectRootElement containingProject)
        {
            XmlElementWithLocation element = containingProject.CreateElement(XMakeElements.import);
            return new ProjectImportElement(element, containingProject) {Project = project};
        }

        /// <summary>
        /// Creates an implicit ProjectImportElement as if it was in the project.
        /// </summary>
        /// <returns></returns>
        internal static ProjectImportElement CreateImplicit(
            string project,
            ProjectRootElement containingProject,
            ImplicitImportLocation implicitImportLocation,
            SdkReference sdkReference,
            ProjectElement originalElement)
        {
            XmlElementWithLocation element = containingProject.CreateElement(XMakeElements.import);
            return new ProjectImportElement(element, containingProject)
            {
                Project = project,
                Sdk = sdkReference.ToString(),
                ImplicitImportLocation = implicitImportLocation,
                ParsedSdkReference = sdkReference,
                OriginalElement = originalElement
            };
        }

        /// <summary>
        /// Overridden to verify that the potential parent and siblings
        /// are acceptable. Throws InvalidOperationException if they are not.
        /// </summary>
        internal override void VerifyThrowInvalidOperationAcceptableLocation(ProjectElementContainer parent, ProjectElement previousSibling, ProjectElement nextSibling)
        {
            ErrorUtilities.VerifyThrowInvalidOperation(parent is ProjectRootElement || parent is ProjectImportGroupElement, "OM_CannotAcceptParent");
        }

        /// <inheritdoc />
        protected override ProjectElement CreateNewInstance(ProjectRootElement owner)
        {
            return owner.CreateImportElement(Project);
        }

        /// <summary>
        /// Helper method to extract attribute values and update the ParsedSdkReference property if
        /// necessary (update only when changed).
        /// </summary>
        /// <returns>True if the ParsedSdkReference was updated, otherwise false (no update necessary).</returns>
        private bool CheckUpdatedSdk()
        {
            var sdk = new SdkReference(
                GetAttributeValue(XMakeAttributes.sdk, true),
                GetAttributeValue(XMakeAttributes.sdkVersion, true),
                GetAttributeValue(XMakeAttributes.sdkMinimumVersion, true));

            if (sdk.Equals(ParsedSdkReference))
            {
                return false;
            }

            ParsedSdkReference = sdk;
            return true;
        }
    }
}
