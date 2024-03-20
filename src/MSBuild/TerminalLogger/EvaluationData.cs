// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

internal sealed class EvaluationData
{
    /// <summary>
    /// Captures data that comes from project evaluation, is assumed to not change through execution,
    /// and is referenced for rendering purposes throughout the execution of the build.
    /// </summary>
    /// <param name="targetFramework"></param>
    public EvaluationData(string? targetFramework)
    {
        TargetFramework = targetFramework;
    }

    /// <summary>
    /// The target framework of the project or null if not multi-targeting.
    /// </summary>
    public string? TargetFramework { get; }

    /// <summary>
    /// This property is true when the project would prefer to have full paths in the logs and/or for processing tasks.
    /// </summary>
    /// <remarks>
    /// There's an MSBuild property called GenerateFullPaths that would be a great knob to use for this, but the Common
    /// Targets set it to true if not set, and setting it to false completely destroys the terminal logger output.
    /// That's why this value is hardcoded to false for now, until we define a better mechanism.
    /// </remarks>
    public bool GenerateFullPaths { get; } = false;
}
