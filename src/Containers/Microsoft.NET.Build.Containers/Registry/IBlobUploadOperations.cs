// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// Represents registry v2 API, blob upload operations.
/// </summary>
/// <remarks>
/// https://docs.docker.com/registry/spec/api/#blob-upload
/// </remarks>
internal interface IBlobUploadOperations
{
    public Task CompleteAsync(Uri uploadUri, string digest, CancellationToken cancellationToken);

    /// <summary>
    /// Check on the status of an upload operation.
    /// </summary>
    /// <remarks>
    /// Note that unlike other operations, this method uses a full URI. This is because we are data-driven entirely by the registry, no path-patterns to follow.
    /// </remarks>
    public Task<HttpResponseMessage> GetStatusAsync(Uri uploadUri, CancellationToken cancellationToken);

    public Task<StartUploadInformation> StartAsync(string repositoryName, CancellationToken cancellationToken);

    public Task<bool> TryMountAsync(string destinationRepository, string sourceRepository, string digest, CancellationToken cancellationToken);

    /// <summary>
    /// Uploads a stream of data to the registry atomically.
    /// </summary>
    /// <remarks>
    /// Note that unlike other operations, this method uses a full URI. This is because we are data-driven entirely by the registry, no path-patterns to follow.
    /// This method is also implemented the same as UploadChunkAsync, and is here for semantic reasons only.
    /// </remarks>
    public Task<FinalizeUploadInformation> UploadAtomicallyAsync(Uri uploadUri, Stream content, CancellationToken cancellationToken);

    /// <summary>
    /// Uploads a chunk of data to the registry. The chunk size is determined by the registry.
    /// </summary>
    /// <remarks>
    /// Note that unlike other operations, this method uses a full URI. This is because we are data-driven entirely by the registry, no path-patterns to follow.
    /// </remarks>
    public Task<NextChunkUploadInformation> UploadChunkAsync(Uri uploadUri, HttpContent content, CancellationToken cancellationToken);
}
