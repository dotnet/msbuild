// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.NET.Build.Containers;

internal class RegistrySettings
{
    private const int DefaultChunkSizeBytes = 1024 * 64;
    private const int FiveMegs = 5_242_880;

    /// <summary>
    /// When chunking is enabled, allows explicit control over the size of the chunks uploaded
    /// </summary>
    /// <remarks>
    /// Our default of 64KB is very conservative, so raising this to 1MB or more can speed up layer uploads reasonably well.
    /// </remarks>
    internal int? ChunkedUploadSizeBytes { get; init; } = Env.GetEnvironmentVariableAsNullableInt(EnvVariables.ChunkedUploadSizeBytes);

    /// <summary>
    /// Allows to force chunked upload for debugging purposes.
    /// </summary>
    internal bool ForceChunkedUpload { get; init; } = Env.GetEnvironmentVariableAsBool(EnvVariables.ForceChunkedUpload, defaultValue: false);

    /// <summary>
    /// Whether we should upload blobs in parallel (enabled by default, but disabled for certain registries in conjunction with the explicit support check below).
    /// </summary>
    /// <remarks>
    /// Enabling this can swamp some registries, so this is an escape hatch.
    /// </remarks>
    internal bool ParallelUploadEnabled { get; init; } = Env.GetEnvironmentVariableAsBool(EnvVariables.ParallelUploadEnabled, defaultValue: true);

    internal struct EnvVariables
    {
        internal const string ChunkedUploadSizeBytes = "SDK_CONTAINER_REGISTRY_CHUNKED_UPLOAD_SIZE_BYTES";

        internal const string ForceChunkedUpload = "SDK_CONTAINER_DEBUG_REGISTRY_FORCE_CHUNKED_UPLOAD";
        internal const string ParallelUploadEnabled = "SDK_CONTAINER_REGISTRY_PARALLEL_UPLOAD";
    }
}
