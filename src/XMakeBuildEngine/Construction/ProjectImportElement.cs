// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Definition of ProjectImportElement class.</summary>
//-----------------------------------------------------------------------

using System.Diagnostics;

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
        internal bool Implicit { get; } = false;
        private string _implicitProject = null;

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
        private ProjectImportElement(XmlElementWithLocation xmlElement, ProjectRootElement containingProject)
            : base(xmlElement, null, containingProject)
        {
        }

        /// <summary>
        /// Initialize an unparented implicit ProjectImportElement
        /// </summary>
        private ProjectImportElement(XmlElementWithLocation xmlElement, ProjectRootElement containingProject, bool isImplicit)
            : this(xmlElement, containingProject)
        {
            Implicit = isImplicit;
        }

        /// <summary>
        /// Gets or sets the Project value. 
        /// </summary>
        public string Project
        {
            get
            {
                return
                    FileUtilities.FixFilePath(Implicit
                        ? _implicitProject
                        : ProjectXmlUtilities.GetAttributeValue(XmlElement, XMakeAttributes.project));
            }

            set
            {
                ErrorUtilities.VerifyThrowArgumentLength(value, XMakeAttributes.project);

                if (Implicit)
                {
                    _implicitProject = value;
                }
                else
                {
                    ProjectXmlUtilities.SetOrRemoveAttribute(XmlElement, XMakeAttributes.project, value);
                    MarkDirty("Set Import Project {0}", value);
                }
            }
        }

        /// <summary>
        /// Location of the project attribute
        /// </summary>
        /// <remarks>
        /// For an implicit import, the location points to the Sdk attribute on the Project element.
        /// </remarks>
        public ElementLocation ProjectLocation => XmlElement.GetAttributeLocation(Implicit ? XMakeAttributes.sdk : XMakeAttributes.project);

        /// <summary>
        /// Creates an unparented ProjectImportElement, wrapping an unparented XmlElement.
        /// Validates the project value.
        /// Caller should then ensure the element is added to a parent
        /// </summary>
        internal static ProjectImportElement CreateDisconnected(string project, ProjectRootElement containingProject)
        {
            XmlElementWithLocation element = containingProject.CreateElement(XMakeElements.import);

            ProjectImportElement import = new ProjectImportElement(element, containingProject);

            import.Project = project;

            return import;
        }

        /// <summary>
        /// Creates an unparented ProjectImportElement, wrapping an unparented XmlElement.
        /// Validates the project value.
        /// Caller should then ensure the element is added to a parent
        /// </summary>
        internal static ProjectImportElement CreateDisconnected(string project, ProjectRootElement containingProject, bool isImplicit)
        {
            ProjectImportElement import = new ProjectImportElement(containingProject.XmlElement, containingProject, isImplicit);

            import.Project = project;

            return import;
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
