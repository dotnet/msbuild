// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Shared;

namespace Microsoft.Build.Experimental.BuildCheck;

/// <summary>
/// BuildCheck OM data representing the environment variables used in a project file and its imports.
/// </summary>
public class EnvironmentVariableCheckData : CheckData
{
    internal EnvironmentVariableCheckData(
        string projectFilePath,
        int? projectConfigurationId,
        (string EnvVarKey, string EnvVarValue, IMSBuildElementLocation Location) evaluatedEnvironmentVariable)
        : base(projectFilePath, projectConfigurationId) => EvaluatedEnvironmentVariable = evaluatedEnvironmentVariable;

    /// <summary>
    /// Gets the evaluated environment variable.
    /// </summary>
    public (string EnvVarKey, string EnvVarValue, IMSBuildElementLocation Location) EvaluatedEnvironmentVariable { get; }
}
