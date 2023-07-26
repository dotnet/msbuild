// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Diagnostics;
using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace Microsoft.NET.Build.Containers;

internal class DefaultBlobOperations : IBlobOperations
{
    private readonly Uri _baseUri;
    private readonly HttpClient _client;
    private readonly ILogger _logger;

    public DefaultBlobOperations(Uri baseUri, HttpClient client, ILogger logger)
    {
        _baseUri = baseUri;
        _client = client;
        _logger = logger;
        Upload = new DefaultBlobUploadOperations(_baseUri, _client, _logger);
    }

    public IBlobUploadOperations Upload { get; }

    public async Task<bool> ExistsAsync(string repositoryName, string digest, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        HttpResponseMessage response = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Head, new Uri(_baseUri, $"/v2/{repositoryName}/blobs/{digest}")), cancellationToken).ConfigureAwait(false);
        return response.StatusCode == HttpStatusCode.OK;
    }

    public async Task<JsonNode> GetJsonAsync(string repositoryName, string digest, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        HttpResponseMessage response = await GetAsync(repositoryName, digest, cancellationToken).ConfigureAwait(false);

        JsonNode? configDoc = JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        Debug.Assert(configDoc is not null);

        return configDoc;
    }

    public async Task<Stream> GetStreamAsync(string repositoryName, string digest, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        HttpResponseMessage response = await GetAsync(repositoryName, digest, cancellationToken).ConfigureAwait(false);

        return await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> GetAsync(string repositoryName, string digest, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, new Uri(_baseUri, $"/v2/{repositoryName}/blobs/{digest}")).AcceptManifestFormats();
        HttpResponseMessage response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return response;
    }
}
