// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// Abstracts over the concept of a local registry storeof some kind.
/// </summary>
internal interface ILocalRegistry {

    /// <summary>
    /// Loads an image (presumably from a tarball) into the local registry.
    /// </summary>
    public Task LoadAsync(BuiltImage image, SourceImageReference sourceReference, DestinationImageReference destinationReference, CancellationToken cancellationToken);

    /// <summary>
    /// Checks to see if the local registry is available. This is used to give nice errors to the user.
    /// </summary>
    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Checks to see if the local registry is available. This is used to give nice errors to the user.
    /// See <see cref="IsAvailableAsync(CancellationToken)"/> for async version.
    /// </summary>
    public bool IsAvailable();
}
