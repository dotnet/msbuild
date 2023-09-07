// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;
using Microsoft.NET.Build.Containers.LocalDaemons;

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// Lists the different kinds of <see cref="DestinationImageReference"/> to which
/// a container image might be pushed to.
/// </summary>
internal enum DestinationImageReferenceKind
{
    LocalRegistry,
    RemoteRegistry
}

/// <summary>
/// Represents a push destination reference to a Docker image.
/// A push destination reference is made of a registry, a repository (aka the image name) and multiple tags.
/// (unlike the <see cref="SourceImageReference"/> which has a single tag)
/// </summary>
internal readonly record struct DestinationImageReference
{
    public DestinationImageReferenceKind Kind { get; }
    public Registry? RemoteRegistry { get; }
    public ILocalRegistry? LocalRegistry { get; }
    public string Repository { get; }
    public string[] Tags { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DestinationImageReference"/> class representing
    /// a destination on a remote registry.
    /// </summary>
    /// <param name="remoteRegistry">The remote registry to push to.</param>
    /// <param name="repository">The repository (aka. image name) at the destination.</param>
    /// <param name="tags">The tags at the destination.</param>
    public DestinationImageReference(Registry remoteRegistry, string repository, string[] tags)
    {
        Kind = DestinationImageReferenceKind.RemoteRegistry;
        RemoteRegistry = remoteRegistry;
        Repository = repository;
        Tags = tags;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DestinationImageReference"/> class representing
    /// a destination at the local registry (e.g. docker daemon).
    /// </summary>
    /// <param name="localRegistry">The local registry to push to.</param>
    /// <param name="repository">The repository (aka. image name) at the destination.</param>
    /// <param name="tags">The tags at the destination.</param>
    public DestinationImageReference(ILocalRegistry localRegistry, string repository, string[] tags)
    {
        Kind = DestinationImageReferenceKind.LocalRegistry;
        LocalRegistry = localRegistry;
        Repository = repository;
        Tags = tags;
    }


    public static DestinationImageReference CreateFromSettings(
        string repository,
        string[] imageTags,
        ILoggerFactory loggerFactory,
        string? archiveOutputPath,
        string? outputRegistry,
        string? localRegistryCommand)
    {
        DestinationImageReference destinationImageReference;
        if (!string.IsNullOrEmpty(archiveOutputPath))
        {
            destinationImageReference = new DestinationImageReference(new ArchiveFileRegistry(archiveOutputPath), repository, imageTags);
        }
        else if (!string.IsNullOrEmpty(outputRegistry))
        {
            destinationImageReference = new DestinationImageReference(new Registry(outputRegistry, loggerFactory.CreateLogger<Registry>()), repository, imageTags);
        }
        else
        {
            ILocalRegistry localRegistry = KnownLocalRegistryTypes.CreateLocalRegistry(localRegistryCommand, loggerFactory);
            destinationImageReference = new DestinationImageReference(localRegistry, repository, imageTags);
        }

        return destinationImageReference;
    }

    public override string ToString()
    {
        string tagList = string.Join(", ", Tags);
        return $"{Repository}:{tagList}";
    }
}
