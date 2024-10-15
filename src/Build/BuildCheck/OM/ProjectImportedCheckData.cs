// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Experimental.BuildCheck;

namespace Microsoft.Build.Experimental.BuildCheck;

/// <summary>
/// Represents data for a check related to an imported project in MSBuild.
/// </summary>
/// <remarks>
/// This class extends the base <see cref="CheckData"/> class to include
/// information specific to imported projects.
/// </remarks>
public class ProjectImportedCheckData : CheckData
{
    internal ProjectImportedCheckData(string importedProjectFile, string projectFilePath, int? projectConfigurationId)
        : base(projectFilePath, projectConfigurationId) => ImportedProjectFileFullPath = importedProjectFile;

    /// <summary>
    /// Gets the file path of the imported project.
    /// </summary>
    public string ImportedProjectFileFullPath { get; }
}
