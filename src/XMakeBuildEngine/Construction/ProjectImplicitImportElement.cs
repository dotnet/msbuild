// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Definition of ProjectImportElement class.</summary>
//-----------------------------------------------------------------------

using System.Diagnostics;
using System.Xml;
using Microsoft.Build.Shared;

using ProjectXmlUtilities = Microsoft.Build.Internal.ProjectXmlUtilities;

namespace Microsoft.Build.Construction
{
    /// <summary>
    /// Represents an import element that does not physically exist in the
    /// project XML, but is implied by the project's SDK definition.
    /// </summary>
    [DebuggerDisplay("Implicit Project={Project} Condition={Condition}")]
    internal class ProjectImplicitImportElement : ProjectElement
    {
        private string _project;

        private ProjectImplicitImportElement(ProjectRootElement containingProject)
        {
            this.ContainingProject = containingProject;
        }

        internal new string ElementName => "ImplicitImport";

        public string Project
        {
            get { return _project; }

            set
            {
                ErrorUtilities.VerifyThrowArgumentLength(value, XMakeAttributes.project);
                _project = value;
            }
        }

        /// <summary>
        /// Creates an unparented ProjectImportElement, wrapping an unparented XmlElement.
        /// Validates the project value.
        /// Caller should then ensure the element is added to a parent
        /// </summary>
        internal static ProjectImplicitImportElement CreateDisconnected(string project, ProjectRootElement containingProject)
        {
            ProjectImplicitImportElement import = new ProjectImplicitImportElement(containingProject)
            {
                Project = project
            };

            return import;
        }

        /// <summary>
        /// Overridden to verify that the potential parent and siblings
        /// are acceptable. Throws InvalidOperationException if they are not.
        /// </summary>
        internal override void VerifyThrowInvalidOperationAcceptableLocation(ProjectElementContainer parent, ProjectElement previousSibling, ProjectElement nextSibling)
        {
            ErrorUtilities.VerifyThrowInvalidOperation(parent is ProjectRootElement, "OM_CannotAcceptParent");
        }

        /// <inheritdoc />
        protected override ProjectElement CreateNewInstance(ProjectRootElement owner)
        {
            return owner.CreateImportElement(this.Project);
        }
    }
}
