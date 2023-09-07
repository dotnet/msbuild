// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.NET.Build.Containers.Resources;

namespace Microsoft.NET.Build.Containers;

internal class DefaultManifestOperations : IManifestOperations
{
    private readonly Uri _baseUri;
    private readonly HttpClient _client;
    private readonly ILogger _logger;
    private readonly string _registryName;

    internal DefaultManifestOperations(Uri baseUri, string registryName, HttpClient client, ILogger logger)
    {
        _baseUri = baseUri;
        _client = client;
        _logger = logger;
        _registryName = registryName;
    }

    public async Task<HttpResponseMessage> GetAsync(string repositoryName, string reference, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, new Uri(_baseUri, $"/v2/{repositoryName}/manifests/{reference}")).AcceptManifestFormats();
        HttpResponseMessage response = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        return response.StatusCode switch
        {
            HttpStatusCode.OK => response,
            HttpStatusCode.NotFound => throw new RepositoryNotFoundException(_registryName, repositoryName, reference),
            HttpStatusCode.Unauthorized => throw new UnableToAccessRepositoryException(_registryName, repositoryName),
            _ => await LogAndThrowContainerHttpException<HttpResponseMessage>(response, cancellationToken).ConfigureAwait(false)
        };
    }

    public async Task PutAsync(string repositoryName, string reference, ManifestV2 manifest, CancellationToken cancellationToken)
    {
        string jsonString = JsonSerializer.SerializeToNode(manifest)?.ToJsonString() ?? "";
        HttpContent manifestUploadContent = new StringContent(jsonString);
        manifestUploadContent.Headers.ContentType = new MediaTypeHeaderValue(SchemaTypes.DockerManifestV2);

        HttpResponseMessage putResponse = await _client.PutAsync(new Uri(_baseUri, $"/v2/{repositoryName}/manifests/{reference}"), manifestUploadContent, cancellationToken).ConfigureAwait(false);

        if (!putResponse.IsSuccessStatusCode)
        {
            await putResponse.LogHttpResponseAsync(_logger, cancellationToken).ConfigureAwait(false);
            throw new ContainerHttpException(Resource.FormatString(nameof(Strings.RegistryPushFailed), putResponse.StatusCode), putResponse.RequestMessage?.RequestUri?.ToString());
        }
    }

    private async Task<T> LogAndThrowContainerHttpException<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await response.LogHttpResponseAsync(_logger, cancellationToken).ConfigureAwait(false);
        throw new ContainerHttpException(Resource.GetString(nameof(Strings.RegistryPullFailed)), response.RequestMessage?.RequestUri?.ToString());
    }
}
