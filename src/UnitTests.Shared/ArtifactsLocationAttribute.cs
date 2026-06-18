// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.UnitTests.Shared;

[System.AttributeUsage(System.AttributeTargets.Assembly)]
public sealed class ArtifactsLocationAttribute(string artifactsLocation, string? artifactsLogLocation = null) : System.Attribute
{
    public string ArtifactsLocation { get; } = artifactsLocation;

    /// <summary>
    /// The build's log output directory (artifacts/log/&lt;Configuration&gt;). This directory is published
    /// as a build artifact by CI, so binary logs captured here for failing out-of-process MSBuild
    /// invocations are automatically collected without any pipeline changes.
    /// </summary>
    public string? ArtifactsLogLocation { get; } = artifactsLogLocation;
}
