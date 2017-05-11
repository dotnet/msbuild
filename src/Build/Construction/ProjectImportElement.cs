// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Definition of ProjectImportElement class.</summary>
//-----------------------------------------------------------------------

using System.Diagnostics;
using Microsoft.Build.Framework;
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
        /// <summary>
        /// Initialize a parented ProjectImportElement
        /// </summary>
        internal ProjectImportElement(XmlElementWithLocation xmlElement, ProjectElementContainer parent, ProjectRootElement containingProject)
            : base(xmlElement, parent, containingProject)
        {
            ErrorUtilities.VerifyThrowArgumentNull(parent, "parent");
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
            get
            {
                return
                    FileUtilities.FixFilePath(ProjectXmlUtilities.GetAttributeValue(XmlElement, XMakeAttributes.project));
            }

            set
            {
                ErrorUtilities.VerifyThrowArgumentLength(value, XMakeAttributes.project);

                ProjectXmlUtilities.SetOrRemoveAttribute(XmlElement, XMakeAttributes.project, value);
                MarkDirty("Set Import Project {0}", value);
            }
        }

        /// <summary>
        /// Location of the project attribute
        /// </summary>
        public ElementLocation ProjectLocation => XmlElement.GetAttributeLocation(XMakeAttributes.project);

        /// <summary>
        /// Gets or sets the SDK that contains the import.
        /// </summary>
        public string Sdk
        {
            get
            {
                return
                    FileUtilities.FixFilePath(ProjectXmlUtilities.GetAttributeValue(XmlElement, XMakeAttributes.sdk));
            }

            set
            {
                ErrorUtilities.VerifyThrowArgumentLength(value, XMakeAttributes.sdk);

                ProjectXmlUtilities.SetOrRemoveAttribute(XmlElement, XMakeAttributes.sdk, value);
                MarkDirty("Set Import Sdk {0}", value);
            }
        }

        /// <summary>
        /// Location of the Sdk attribute
        /// </summary>
        public ElementLocation SdkLocation => XmlElement.GetAttributeLocation(XMakeAttributes.sdk);

        /// <summary>
        /// Gets the <see cref="ImplicitImportLocation"/> of the import.  This indicates if the import was implicitly
        /// added because of the <see cref="ProjectRootElement.Sdk"/> attribute and the location where the project was
        /// imported.
        /// </summary>
        public ImplicitImportLocation ImplicitImportLocation { get; internal set; }

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
        internal static ProjectImportElement CreateImplicit(string project, ProjectRootElement containingProject, ImplicitImportLocation implicitImportLocation, SdkReference sdkReference)
        {
            XmlElementWithLocation element = containingProject.CreateElement(XMakeElements.import);
            return new ProjectImportElement(element, containingProject)
            {
                Project = project,
                Sdk = sdkReference.ToString(),
                ImplicitImportLocation = implicitImportLocation,
                ParsedSdkReference = sdkReference
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
            return owner.CreateImportElement(this.Project);
        }
    }
}
