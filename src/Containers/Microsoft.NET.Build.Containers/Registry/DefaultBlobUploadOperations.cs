// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Net.Http.Headers;
using System.Net;
using Microsoft.NET.Build.Containers.Resources;
using Microsoft.Extensions.Logging;

namespace Microsoft.NET.Build.Containers;

internal class DefaultBlobUploadOperations : IBlobUploadOperations
{
    private readonly Uri _baseUri;
    private readonly HttpClient _client;
    private readonly ILogger _logger;

    internal DefaultBlobUploadOperations(Uri baseUri, HttpClient client, ILogger logger)
    {
        _baseUri = baseUri;
        _client = client;
        _logger = logger;
    }

    public async Task CompleteAsync(Uri uploadUri, string digest, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // PUT with digest to finalize
        UriBuilder builder = new(uploadUri.IsAbsoluteUri ? uploadUri : new Uri(_baseUri, uploadUri));
        builder.Query += $"&digest={Uri.EscapeDataString(digest)}";
        Uri putUri = builder.Uri;
        HttpResponseMessage finalizeResponse = await _client.PutAsync(putUri, null, cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        if (finalizeResponse.StatusCode != HttpStatusCode.Created)
        {
            await finalizeResponse.LogHttpResponseAsync(_logger, cancellationToken).ConfigureAwait(false);
            string errorMessage = Resource.FormatString(nameof(Strings.BlobUploadFailed), $"PUT {putUri}", finalizeResponse.StatusCode);
            throw new ApplicationException(errorMessage);
        }
    }

    public async Task<HttpResponseMessage> GetStatusAsync(Uri uploadUri, CancellationToken cancellationToken)
    {
        return await _client.GetAsync(uploadUri.IsAbsoluteUri ? uploadUri : new Uri(_baseUri, uploadUri), cancellationToken).ConfigureAwait(false);
    }

    public async Task<StartUploadInformation> StartAsync(string repositoryName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Uri startUploadUri = new(_baseUri, $"/v2/{repositoryName}/blobs/uploads/");

        HttpResponseMessage pushResponse = await _client.PostAsync(startUploadUri, content: null, cancellationToken).ConfigureAwait(false);

        if (pushResponse.StatusCode != HttpStatusCode.Accepted)
        {
            await pushResponse.LogHttpResponseAsync(_logger, cancellationToken).ConfigureAwait(false);
            string errorMessage = Resource.FormatString(nameof(Strings.BlobUploadFailed), $"POST {startUploadUri}", pushResponse.StatusCode);
            throw new ApplicationException(errorMessage);
        }
        cancellationToken.ThrowIfCancellationRequested();
        Uri location = pushResponse.GetNextLocation();
        return new(location);
    }

    public async Task<bool> TryMountAsync(string destinationRepository, string sourceRepository, string digest, CancellationToken cancellationToken)
    {
        // Blob wasn't there; can we tell the server to get it from the base image?
        HttpResponseMessage pushResponse = await _client.PostAsync(new Uri(_baseUri, $"/v2/{destinationRepository}/blobs/uploads/?mount={digest}&from={sourceRepository}"), content: null, cancellationToken).ConfigureAwait(false);
        return pushResponse.StatusCode == HttpStatusCode.Created;
    }

    public async Task<FinalizeUploadInformation> UploadAtomicallyAsync(Uri uploadUri, Stream content, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        StreamContent httpContent = new(content);
        httpContent.Headers.ContentLength = content.Length;

        Uri nextUploadUri = await PatchAsync(uploadUri, httpContent, cancellationToken).ConfigureAwait(false);

        return new(nextUploadUri);
    }

    public async Task<NextChunkUploadInformation> UploadChunkAsync(Uri uploadUri, HttpContent content, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Uri nextUploadUri = await PatchAsync(uploadUri, content, cancellationToken).ConfigureAwait(false);

        return new(nextUploadUri);
    }

    private HttpRequestMessage GetPatchHttpRequest(Uri uploadUri, HttpContent httpContent)
    {
        Uri finalUri = uploadUri.IsAbsoluteUri ? uploadUri : new Uri(_baseUri, uploadUri);
        HttpRequestMessage patchMessage = new(HttpMethod.Patch, finalUri)
        {
            Content = httpContent
        };
        patchMessage.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
        return patchMessage;
    }

    private async Task<Uri> PatchAsync(Uri uploadUri, HttpContent content, CancellationToken cancellationToken)
    {
        _logger.LogTrace("Uploading {0} bytes of content at {1}", content.Headers.ContentLength, uploadUri);

        HttpRequestMessage patchMessage = GetPatchHttpRequest(uploadUri, content);
        HttpResponseMessage patchResponse = await _client.SendAsync(patchMessage, cancellationToken).ConfigureAwait(false);

        _logger.LogTrace("Received status code '{0}' from upload.", patchResponse.StatusCode);

        // Fail the upload if the response code is not Accepted (202) or if uploading to Amazon ECR which returns back Created (201).
        if (!(patchResponse.StatusCode == HttpStatusCode.Accepted || (uploadUri.IsAmazonECRRegistry() && patchResponse.StatusCode == HttpStatusCode.Created)))
        {
            await patchResponse.LogHttpResponseAsync(_logger, cancellationToken).ConfigureAwait(false);
            string errorMessage = Resource.FormatString(nameof(Strings.BlobUploadFailed), $"PATCH {uploadUri}", patchResponse.StatusCode);
            throw new ApplicationException(errorMessage);
        }
        return patchResponse.GetNextLocation();
    }
}
