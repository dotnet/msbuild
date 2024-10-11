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
        string envVarName,
        string envVarValue,
        IMSBuildElementLocation Location)
        : base(projectFilePath, projectConfigurationId)
    {
        EnvironmentVariableName = envVarName;
        EnvironmentVariableValue = envVarValue;
        EnvironmentVariableLocation = Location;
    }

    /// <summary>
    /// Gets the evaluated environment variable location.
    /// </summary>
    public IMSBuildElementLocation EnvironmentVariableLocation { get; }

    /// <summary>
    /// Gets the evaluated environment variable name.
    /// </summary>
    public string EnvironmentVariableName { get; }

    /// <summary>
    /// Gets the evaluated environment variable value.
    /// </summary>
    public string EnvironmentVariableValue { get; }
}
