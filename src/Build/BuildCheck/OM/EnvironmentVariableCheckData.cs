// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
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
        IReadOnlyDictionary<string, (string EnvVarValue, IMSBuildElementLocation Location)> evaluatedEnvironmentVariables)
        : base(projectFilePath, projectConfigurationId) => EvaluatedEnvironmentVariables = evaluatedEnvironmentVariables;

    /// <summary>
    /// Gets the evaluated environment variables and their metadata.
    /// </summary>
    public IReadOnlyDictionary<string, (string EnvVarValue, IMSBuildElementLocation Location)> EvaluatedEnvironmentVariables { get; }
}
