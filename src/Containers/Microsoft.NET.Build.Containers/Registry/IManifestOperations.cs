// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// Represents registry v2 API, manifest operations.
/// </summary>
/// <remarks>
/// https://docs.docker.com/registry/spec/api/#manifest
/// </remarks>
internal interface IManifestOperations
{
    public Task<HttpResponseMessage> GetAsync(string repositoryName, string reference, CancellationToken cancellationToken);

    public Task PutAsync(string repositoryName, string reference, ManifestV2 manifest, CancellationToken cancellationToken);
}
