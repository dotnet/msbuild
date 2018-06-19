// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Definition of ProjectImportGroupElement class.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Xml;
using System.Diagnostics;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Collections;

using ProjectXmlUtilities = Microsoft.Build.Internal.ProjectXmlUtilities;

namespace Microsoft.Build.Construction
{
    /// <summary>
    /// ProjectImportGroupElement represents the ImportGroup element in the MSBuild project.
    /// </summary>
    [DebuggerDisplay("#Imports={Count} Condition={Condition} Label={Label}")]
    public class ProjectImportGroupElement : ProjectElementContainer
    {
        #region Constructors

        /// <summary>
        /// Initialize a parented ProjectImportGroupElement
        /// </summary>
        internal ProjectImportGroupElement(XmlElementWithLocation xmlElement, ProjectElementContainer parent, ProjectRootElement containingProject)
            : base(xmlElement, parent, containingProject)
        {
            ErrorUtilities.VerifyThrowArgumentNull(parent, "parent");
        }

        /// <summary>
        /// Initialize an unparented ProjectImportGroupElement
        /// </summary>
        private ProjectImportGroupElement(XmlElementWithLocation xmlElement, ProjectRootElement containingProject)
            : base(xmlElement, null, containingProject)
        {
        }

        #endregion // Constructors

        #region Properties

        /// <summary>
        /// Get any contained properties.
        /// </summary>
        public ICollection<ProjectImportElement> Imports
        {
            get
            {
                return new ReadOnlyCollection<ProjectImportElement>(Children.OfType<ProjectImportElement>());
            }
        }
        #endregion // Properties

        #region Methods

        /// <summary>
        /// Convenience method that picks a location based on a heuristic:
        /// Adds a new import after the last import in this import group.
        /// </summary>
        public ProjectImportElement AddImport(string project)
        {
            ErrorUtilities.VerifyThrowArgumentLength(project, "project");

            ProjectImportElement newImport = ContainingProject.CreateImportElement(project);
            AppendChild(newImport);

            return newImport;
        }

        /// <summary>
        /// Creates an unparented ProjectImportGroupElement, wrapping an unparented XmlElement.
        /// Caller should then ensure the element is added to a parent
        /// </summary>
        internal static ProjectImportGroupElement CreateDisconnected(ProjectRootElement containingProject)
        {
            XmlElementWithLocation element = containingProject.CreateElement(XMakeElements.importGroup);

            return new ProjectImportGroupElement(element, containingProject);
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
            return owner.CreateImportGroupElement();
        }

        #endregion // Methods
    }
}
