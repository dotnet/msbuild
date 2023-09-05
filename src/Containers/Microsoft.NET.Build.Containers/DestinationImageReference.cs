// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// Represents a push destination reference to a Docker image.
/// A push destination reference is made of a registry, a repository (aka the image name) and multiple tags.
/// (unlike the <see cref="SourceImageReference"/> which has a single tag)
/// </summary>
internal readonly record struct DestinationImageReference(Registry? Registry, string Repository, string[] Tags)
{
    public override string ToString()
    {
        string tagList = string.Join(", ", Tags);
        if (Registry is { } reg)
        {
            return $"{reg.RegistryName}/{Repository}:{tagList}";
        }
        else
        {
            return $"{Repository}:{tagList}";
        }
    }
}
