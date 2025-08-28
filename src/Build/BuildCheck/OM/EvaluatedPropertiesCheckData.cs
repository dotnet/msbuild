// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.Build.Experimental.BuildCheck;

/// <summary>
/// BuildCheck OM data representing the evaluated properties of a project.
/// </summary>
public class EvaluatedPropertiesCheckData : CheckData
{
    internal EvaluatedPropertiesCheckData(
        string projectFilePath,
        int? projectConfigurationId,
        IReadOnlyDictionary<string, string> evaluatedProperties,
        IReadOnlyDictionary<string, string> globalProperties)
        : base(projectFilePath, projectConfigurationId)
        => (EvaluatedProperties, GlobalProperties) = (evaluatedProperties, globalProperties);

    /// <summary>
    /// Gets the evaluated properties of the project.
    /// </summary>
    public IReadOnlyDictionary<string, string> EvaluatedProperties { get; }

    /// <summary>
    /// Gets the global properties passed to the project.
    /// </summary>
    public IReadOnlyDictionary<string, string> GlobalProperties { get; }
}
