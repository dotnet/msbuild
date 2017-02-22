// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>A hack (leaking into public API) to prevent a certain case of Jitting in our NGen'd assemblies.</summary>
//-----------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Construction;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// Encapsulates an import relationship in an evaluated project
    /// between a ProjectImportElement and the ProjectRootElement of the
    /// imported project.
    /// </summary>
    /// <comment>
    /// This struct is functionally identical to KeyValuePair, but is necessary to avoid
    /// CA908 warnings (types that in ngen images that will JIT).
    /// It works because although this is a value type, it is not defined in mscorlib.
    /// Essentially we would use KeyValuePair except for this technical reason.
    /// </comment>
    [SuppressMessage("Microsoft.Performance", "CA1815:OverrideEqualsAndOperatorEqualsOnValueTypes", Justification = "Not possible as Equals cannot be implemented on the struct members")]
    public struct ResolvedImport
    {
        /// <summary>
        /// Element doing the import
        /// </summary>
        private ProjectImportElement _importingElement;

        /// <summary>
        /// One of the files it causes to import
        /// </summary>
        private ProjectRootElement _importedProject;

        /// <summary>
        /// Whether the importing element is itself imported.
        /// </summary>
        private bool _isImported;

        /// <summary>
        /// Initializes a new instance of the <see cref="ResolvedImport"/> struct.
        /// </summary>
        internal ResolvedImport(Project project, ProjectImportElement importingElement, ProjectRootElement importedProject)
        {
            ErrorUtilities.VerifyThrowInternalNull(importingElement, "parent");
            ErrorUtilities.VerifyThrowInternalNull(importedProject, "child");

            _importingElement = importingElement;
            _importedProject = importedProject;
            _isImported = !ReferenceEquals(project.Xml, importingElement.ContainingProject);
        }

        /// <summary>
        /// Gets the element doing the import.
        /// </summary>
        public ProjectImportElement ImportingElement
        {
            get { return _importingElement; }
        }

        /// <summary>
        /// Gets one of the imported projects.
        /// </summary>
        public ProjectRootElement ImportedProject
        {
            get { return _importedProject; }
        }

        /// <summary>
        /// Whether the importing element is itself imported.
        /// </summary>
        public bool IsImported
        {
            get { return _isImported; }
        }
    }
}
