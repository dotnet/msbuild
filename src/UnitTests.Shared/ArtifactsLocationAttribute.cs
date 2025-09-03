// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.UnitTests.Shared;

[System.AttributeUsage(System.AttributeTargets.Assembly)]
public sealed class ArtifactsLocationAttribute(string artifactsLocation) : System.Attribute
{
    public string ArtifactsLocation { get; } = artifactsLocation;
}
