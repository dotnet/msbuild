// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Text.Json.Nodes;

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// Represents registry v2 API, blob operations.
/// </summary>
/// <remarks>
/// https://docs.docker.com/registry/spec/api/#blob
/// </remarks>
internal interface IBlobOperations
{
    public IBlobUploadOperations Upload { get; }

    public Task<bool> ExistsAsync(string repositoryName, string digest, CancellationToken cancellationToken);

    public Task<JsonNode> GetJsonAsync(string repositoryName, string digest, CancellationToken cancellationToken);

    public Task<Stream> GetStreamAsync(string repositoryName, string digest, CancellationToken cancellationToken);
}
