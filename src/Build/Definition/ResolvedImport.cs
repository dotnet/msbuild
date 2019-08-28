// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework;
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
        /// Initializes a new instance of the <see cref="ResolvedImport"/> struct.
        /// </summary>
        internal ResolvedImport(ProjectImportElement importingElement, ProjectRootElement importedProject, int versionEvaluated, SdkResult sdkResult, bool isImported)
        {
            ErrorUtilities.VerifyThrowInternalNull(importedProject, "child");

            ImportingElement = importingElement;
            ImportedProject = importedProject;
            SdkResult = sdkResult;
            VersionEvaluated = versionEvaluated;
            IsImported = isImported;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ResolvedImport"/> struct.
        /// </summary>
        internal ResolvedImport(Project project, ProjectImportElement importingElement, ProjectRootElement importedProject, int versionEvaluated, SdkResult sdkResult)
        {
            ErrorUtilities.VerifyThrowInternalNull(importedProject, "child");

            ImportingElement = importingElement;
            ImportedProject = importedProject;
            SdkResult = sdkResult;
            VersionEvaluated = versionEvaluated;
            IsImported = importingElement != null && !ReferenceEquals(project.Xml, importingElement.ContainingProject);
        }

        /// <summary>
        /// Gets the element doing the import.
        /// Null if this is the top project
        /// </summary>
        public ProjectImportElement ImportingElement { get; }

        /// <summary>
        /// Gets one of the imported projects.
        /// </summary>
        public ProjectRootElement ImportedProject { get; }

        /// <summary>
        /// Non null if this import was an sdk import.
        /// </summary>
        public SdkResult SdkResult { get; }

        internal int VersionEvaluated { get; }

        /// <summary>
        /// Whether the importing element is itself imported.
        /// </summary>
        public bool IsImported { get; }
    }
}
