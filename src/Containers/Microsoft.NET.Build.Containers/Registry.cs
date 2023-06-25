// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.NET.Build.Containers.Resources;
using NuGet.RuntimeModel;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Microsoft.NET.Build.Containers;

internal sealed class Registry
{
    private const string DockerManifestV2 = "application/vnd.docker.distribution.manifest.v2+json";
    private const string OciManifestV1 = "application/vnd.oci.image.manifest.v1+json"; // https://containers.gitbook.io/build-containers-the-hard-way/#registry-format-oci-image-manifest
    private const string DockerManifestListV2 = "application/vnd.docker.distribution.manifest.list.v2+json";
    private const string DockerContainerV1 = "application/vnd.docker.container.image.v1+json";
    private const string DockerHubRegistry1 = "registry-1.docker.io";
    private const string DockerHubRegistry2 = "registry.hub.docker.com";

    /// <summary>
    /// Whether we should upload blobs via chunked upload (enabled by default, but disabled for certain registries in conjunction with the explicit support check below).
    /// </summary>
    /// <remarks>
    /// Relates to https://github.com/dotnet/sdk-container-builds/pull/383#issuecomment-1466408853
    /// </remarks>
    private static readonly bool s_ForceChunkedUploadEnabled = Env.GetEnvironmentVariableAsBool(ContainerHelpers.ForceChunkedUploadEnabled, defaultValue: false);

    /// <summary>
    /// When chunking is enabled, allows explicit control over the size of the chunks uploaded
    /// </summary>
    /// <remarks>
    /// Our default of 64KB is very conservative, so raising this to 1MB or more can speed up layer uploads reasonably well.
    /// </remarks>
    private static readonly int? s_chunkedUploadSizeBytes = Env.GetEnvironmentVariableAsNullableInt(ContainerHelpers.ChunkedUploadSizeBytes);

    /// <summary>
    /// Whether we should upload blobs in parallel (enabled by default, but disabled for certain registries in conjunction with the explicit support check below).
    /// </summary>
    /// <remarks>
    /// Enabling this can swamp some registries, so this is an escape hatch.
    /// </remarks>
    private static readonly bool s_parallelUploadEnabled =  Env.GetEnvironmentVariableAsBool(ContainerHelpers.ParallelUploadEnabled, defaultValue: true);

    private static readonly int s_defaultChunkSizeBytes = 1024 * 64;

    private readonly HttpClient _client;
    private readonly ILogger _logger;

    /// <summary>
    /// The name of the registry, which is the host name, optionally followed by a colon and the port number.
    /// This is used in user-facing error messages, and it should match what the user would manually enter as
    /// part of Docker commands like `docker login`.
    /// </summary>
    public string RegistryName { get; init; }

    public Registry(Uri baseUri, ILogger logger)
    {
        _logger = logger;
        RegistryName = DeriveRegistryName(baseUri);
        BaseUri = baseUri;
        // "docker.io" is not a real registry. Replace the uri to refer to an actual registry.
        if (BaseUri.Host == ContainerHelpers.DockerRegistryAlias)
        {
            BaseUri = new UriBuilder(BaseUri.ToString()) { Host = DockerHubRegistry1 }.Uri;
        }
        _client = CreateClient();
    }

    private static string DeriveRegistryName(Uri baseUri)
    {
        var port = baseUri.Port == -1 ? string.Empty : $":{baseUri.Port}";
        if (baseUri.OriginalString.EndsWith(port, ignoreCase: true, culture: null))
        {
            // the port was part of the original assignment, so it's ok to consider it part of the 'name
            return baseUri.GetComponents(UriComponents.HostAndPort, UriFormat.Unescaped);
        }
        else
        {
            // the port was not part of the original assignment, so it's not part of the 'name'
            return baseUri.GetComponents(UriComponents.Host, UriFormat.Unescaped);
        }
    }

    public Uri BaseUri { get; }

    /// <summary>
    /// The max chunk size for patch blob uploads.
    /// </summary>
    /// <remarks>
    /// This varies by registry target, for example Amazon Elastic Container Registry requires 5MB chunks for all but the last chunk.
    /// </remarks>
    public int MaxChunkSizeBytes => s_chunkedUploadSizeBytes.HasValue ? s_chunkedUploadSizeBytes.Value : (IsAmazonECRRegistry ? 5248080 : s_defaultChunkSizeBytes);

    /// <summary>
    /// Check to see if the registry is for Amazon Elastic Container Registry (ECR).
    /// </summary>
    public bool IsAmazonECRRegistry
    {
        get
        {
            // If this the registry is to public ECR the name will contain "public.ecr.aws".
            if (RegistryName.Contains("public.ecr.aws"))
            {
                return true;
            }

            // If the registry is to a private ECR the registry will start with an account id which is a 12 digit number and will container either
            // ".ecr." or ".ecr-" if pushed to a FIPS endpoint.
            var accountId = RegistryName.Split('.')[0];
            if ((RegistryName.Contains(".ecr.") || RegistryName.Contains(".ecr-")) && accountId.Length == 12 && long.TryParse(accountId, out _))
            {
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Check to see if the registry is GitHub Packages, which always uses ghcr.io.
    /// </summary>
    public bool IsGithubPackageRegistry => RegistryName.StartsWith("ghcr.io", StringComparison.Ordinal);

    /// <summary>
    /// Check to see if the registry is Docker Hub, which uses two well-known domains.
    /// </summary>
    public bool IsDockerHub => RegistryName.Equals(ContainerHelpers.DockerRegistryAlias, StringComparison.Ordinal)
                            || RegistryName.Equals(DockerHubRegistry1, StringComparison.Ordinal)
                            || RegistryName.Equals(DockerHubRegistry2, StringComparison.Ordinal);

    /// <summary>
    /// Check to see if the registry is for Google Artifact Registry.
    /// </summary>
    /// <remarks>
    /// Google Artifact Registry locations (one for each availability zone) are of the form "ZONE-docker.pkg.dev".
    /// </remarks>
    public bool IsGoogleArtifactRegistry {
        get => RegistryName.EndsWith("-docker.pkg.dev", StringComparison.Ordinal);
    }

    /// <summary>
    /// Pushing to ECR uses a much larger chunk size. To avoid getting too many socket disconnects trying to do too many
    /// parallel uploads be more conservative and upload one layer at a time.
    /// </summary>
    private bool SupportsParallelUploads => !IsAmazonECRRegistry && s_parallelUploadEnabled;

    public async Task<ImageBuilder> GetImageManifestAsync(string repositoryName, string reference, string runtimeIdentifier, string runtimeIdentifierGraphPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var initialManifestResponse = await GetManifestAsync(repositoryName, reference, cancellationToken).ConfigureAwait(false);

        return initialManifestResponse.Content.Headers.ContentType?.MediaType switch
        {
            DockerManifestV2 or OciManifestV1 => await ReadSingleImageAsync(
                repositoryName,
                await initialManifestResponse.Content.ReadFromJsonAsync<ManifestV2>(cancellationToken: cancellationToken).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false),
            DockerManifestListV2 => await PickBestImageFromManifestListAsync(
                repositoryName,
                reference,
                await initialManifestResponse.Content.ReadFromJsonAsync<ManifestListV2>(cancellationToken: cancellationToken).ConfigureAwait(false),
                runtimeIdentifier,
                runtimeIdentifierGraphPath,
                cancellationToken).ConfigureAwait(false),
            var unknownMediaType => throw new NotImplementedException(Resource.FormatString(
                nameof(Strings.UnknownMediaType),
                repositoryName,
                reference,
                BaseUri,
                unknownMediaType))
        };
    }

    private async Task<ImageBuilder> ReadSingleImageAsync(string repositoryName, ManifestV2 manifest, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var config = manifest.Config;
        string configSha = config.digest;

        var blobResponse = await GetBlobAsync(repositoryName, configSha, cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();
        JsonNode? configDoc = JsonNode.Parse(await blobResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        Debug.Assert(configDoc is not null);

        cancellationToken.ThrowIfCancellationRequested();
        return new ImageBuilder(manifest, new ImageConfig(configDoc));
    }

    private async Task<ImageBuilder> PickBestImageFromManifestListAsync(
        string repositoryName,
        string reference,
        ManifestListV2 manifestList,
        string runtimeIdentifier,
        string runtimeIdentifierGraphPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var runtimeGraph = GetRuntimeGraphForDotNet(runtimeIdentifierGraphPath);
        var (ridDict, graphForManifestList) = ConstructRuntimeGraphForManifestList(manifestList, runtimeGraph);
        var bestManifestRid = CheckIfRidExistsInGraph(graphForManifestList, ridDict.Keys, runtimeIdentifier);
        if (bestManifestRid is null) {
            throw new BaseImageNotFoundException(runtimeIdentifier, repositoryName, reference, graphForManifestList.Runtimes.Keys);
        }
        var matchingManifest = ridDict[bestManifestRid];
        var manifestResponse = await GetManifestAsync(repositoryName, matchingManifest.digest, cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        return await ReadSingleImageAsync(
            repositoryName,
            await manifestResponse.Content.ReadFromJsonAsync<ManifestV2>(cancellationToken: cancellationToken).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> GetManifestAsync(string repositoryName, string reference, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var client = GetClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(BaseUri, $"/v2/{repositoryName}/manifests/{reference}"));
        AddDockerFormatsAcceptHeader(request);
        var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return response;
    }

    private async Task<HttpResponseMessage> GetBlobAsync(string repositoryName, string digest, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var client = GetClient();
        using var request =
            new HttpRequestMessage(HttpMethod.Get, new Uri(BaseUri, $"/v2/{repositoryName}/blobs/{digest}"));
        AddDockerFormatsAcceptHeader(request);
        var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return response;
    }

    private static string? CheckIfRidExistsInGraph(RuntimeGraph graphForManifestList, IEnumerable<string> leafRids, string userRid) => leafRids.FirstOrDefault(leaf => graphForManifestList.AreCompatible(leaf, userRid));

    private (IReadOnlyDictionary<string, PlatformSpecificManifest>, RuntimeGraph) ConstructRuntimeGraphForManifestList(ManifestListV2 manifestList, RuntimeGraph dotnetRuntimeGraph)
    {
        var ridDict = new Dictionary<string, PlatformSpecificManifest>();
        var runtimeDescriptionSet = new HashSet<RuntimeDescription>();
        foreach (var manifest in manifestList.manifests) {
            if (CreateRidForPlatform(manifest.platform) is { } rid)
            {
                if (ridDict.TryAdd(rid, manifest)) {
                    AddRidAndDescendantsToSet(runtimeDescriptionSet, rid, dotnetRuntimeGraph);
                }
            }
        }

        var graph = new RuntimeGraph(runtimeDescriptionSet);
        return (ridDict, graph);
    }

    private static string? CreateRidForPlatform(PlatformInformation platform)
    {
        // we only support linux and windows containers explicitly, so anything else we should skip past.
        var osPart = platform.os switch
        {
            "linux" => "linux",
            "windows" => "win",
            _ => null
        };
        // TODO: this part needs a lot of work, the RID graph isn't super precise here and version numbers (especially on windows) are _whack_
        // TODO: we _may_ need OS-specific version parsing. Need to do more research on what the field looks like across more manifest lists.
        var versionPart = platform.version?.Split('.') switch
        {
            [var major, .. ] => major,
            _ => null
        };
        var platformPart = platform.architecture switch
        {
            "amd64" => "x64",
            "x386" => "x86",
            "arm" => $"arm{(platform.variant != "v7" ? platform.variant : "")}",
            "arm64" => "arm64",
            "ppc64le" => "ppc64le",
            "s390x" => "s390x",
            _ => null
        };

        if (osPart is null || platformPart is null) return null;
        return $"{osPart}{versionPart ?? ""}-{platformPart}";
    }

    private static RuntimeGraph GetRuntimeGraphForDotNet(string ridGraphPath) => JsonRuntimeFormat.ReadRuntimeGraph(ridGraphPath);

    private void AddRidAndDescendantsToSet(HashSet<RuntimeDescription> runtimeDescriptionSet, string rid, RuntimeGraph dotnetRuntimeGraph)
    {
        var R = dotnetRuntimeGraph.Runtimes[rid];
        runtimeDescriptionSet.Add(R);
        foreach (var r in R.InheritedRuntimes) AddRidAndDescendantsToSet(runtimeDescriptionSet, r, dotnetRuntimeGraph);
    }

    /// <summary>
    /// Ensure a blob associated with <paramref name="repository"/> from the registry is available locally.
    /// </summary>
    /// <param name="repository">Name of the associated image repository.</param>
    /// <param name="descriptor"><see cref="Descriptor"/> that describes the blob.</param>
    /// <returns>Local path to the (decompressed) blob content.</returns>
    public async Task<string> DownloadBlobAsync(string repository, Descriptor descriptor, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string localPath = ContentStore.PathForDescriptor(descriptor);

        if (File.Exists(localPath))
        {
            // Assume file is up to date and just return it
            return localPath;
        }

        // No local copy, so download one

        HttpClient client = GetClient();

        using var request = new HttpRequestMessage(HttpMethod.Get,
            new Uri(BaseUri, $"/v2/{repository}/blobs/{descriptor.Digest}"));
        AddDockerFormatsAcceptHeader(request);
        var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();
        response.EnsureSuccessStatusCode();

        string tempTarballPath = ContentStore.GetTempFile();
        using (FileStream fs = File.Create(tempTarballPath))
        {
            using Stream responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

            await responseStream.CopyToAsync(fs, cancellationToken).ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();

        File.Move(tempTarballPath, localPath, overwrite: true);

        return localPath;
    }

    public async Task PushAsync(Layer layer, string repository, Action<string> logProgressMessage, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string digest = layer.Descriptor.Digest;

        using (FileStream contents = File.OpenRead(layer.BackingFile))
        {
            await UploadBlobAsync(repository, digest, contents, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<UriBuilder> UploadBlobChunkedAsync(string repository, string digest, Stream contents, HttpClient client, UriBuilder uploadUri, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Uri patchUri = uploadUri.Uri;
        var localUploadUri = new UriBuilder(uploadUri.Uri);
        localUploadUri.Query += $"&digest={Uri.EscapeDataString(digest)}";

        // TODO: this chunking is super tiny and probably not necessary; what does the docker client do
        //       and can we be smarter?

        byte[] chunkBackingStore = new byte[MaxChunkSizeBytes];

        int chunkCount = 0;
        int chunkStart = 0;

        _logger.LogTrace("Uploading {0} bytes of content in chunks of {1} bytes.", contents.Length, chunkBackingStore.Length);

        while (contents.Position < contents.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogTrace("Processing next chunk because current position {0} < content size {1}, chunk size: {2}.", contents.Position, contents.Length, chunkBackingStore.Length);

            int bytesRead = await contents.ReadAsync(chunkBackingStore, cancellationToken).ConfigureAwait(false);

            ByteArrayContent content = new (chunkBackingStore, offset: 0, count: bytesRead);
            content.Headers.ContentLength = bytesRead;

            // manual because ACR throws an error with the .NET type {"Range":"bytes 0-84521/*","Reason":"the Content-Range header format is invalid"}
            //    content.Headers.Add("Content-Range", $"0-{contents.Length - 1}");
            Debug.Assert(content.Headers.TryAddWithoutValidation("Content-Range", $"{chunkStart}-{chunkStart + bytesRead - 1}"));
            
            _logger.LogTrace("Uploading {0} bytes of content at {1}", bytesRead, patchUri);

            HttpRequestMessage patchMessage = new(HttpMethod.Patch, patchUri)
            {
                Content = content
            };
            patchMessage.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
            HttpResponseMessage patchResponse = await client.SendAsync(patchMessage, cancellationToken).ConfigureAwait(false);

            _logger.LogTrace("Received status code '{0}' from upload.", patchResponse.StatusCode);

            // Fail the upload if the response code is not Accepted (202) or if uploading to Amazon ECR which returns back Created (201).
            if (!(patchResponse.StatusCode == HttpStatusCode.Accepted || (IsAmazonECRRegistry && patchResponse.StatusCode == HttpStatusCode.Created)))
            {
                await patchResponse.LogHttpResponseAsync(_logger, cancellationToken).ConfigureAwait(false);
                string errorMessage = Resource.FormatString(nameof(Strings.BlobUploadFailed), $"PATCH {patchUri}", patchResponse.StatusCode);
                throw new ApplicationException(errorMessage);
            }

           localUploadUri = GetNextLocation(patchResponse);

            patchUri = localUploadUri.Uri;

            chunkCount += 1;
            chunkStart += bytesRead;
        }
        return new UriBuilder(patchUri);
    }

    private UriBuilder GetNextLocation(HttpResponseMessage response) {
        if (response.Headers.Location is {IsAbsoluteUri: true })
        {
            return new UriBuilder(response.Headers.Location);
        }
        else
        {
            // if we don't trim the BaseUri and relative Uri of slashes, you can get invalid urls.
            // Uri constructor does this on our behalf.
            return new UriBuilder(new Uri(BaseUri, response.Headers.Location?.OriginalString ?? ""));
        }
    }

    private async Task<UriBuilder> UploadBlobWholeAsync(string repository, string digest, Stream contents, HttpClient client, UriBuilder uploadUri, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        StreamContent content = new StreamContent(contents);
        content.Headers.ContentLength = contents.Length;
        HttpRequestMessage patchMessage = new(HttpMethod.Patch, uploadUri.Uri)
        {
            Content = content
        };
        patchMessage.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
        HttpResponseMessage patchResponse = await client.SendAsync(patchMessage, cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();
        // Fail the upload if the response code is not Accepted (202) or if uploading to Amazon ECR which returns back Created (201).
        if (!(patchResponse.StatusCode == HttpStatusCode.Accepted || (IsAmazonECRRegistry && patchResponse.StatusCode == HttpStatusCode.Created)))
        {
            await patchResponse.LogHttpResponseAsync(_logger, cancellationToken).ConfigureAwait(false);
            string errorMessage = Resource.FormatString(nameof(Strings.BlobUploadFailed), $"PATCH {uploadUri}", patchResponse.StatusCode);
            throw new ApplicationException(errorMessage);
        }
        return GetNextLocation(patchResponse);
    }

    private async Task<UriBuilder> StartUploadSessionAsync(string repository, string digest, HttpClient client, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Uri startUploadUri = new Uri(BaseUri, $"/v2/{repository}/blobs/uploads/");

        HttpResponseMessage pushResponse = await client.PostAsync(startUploadUri, content: null, cancellationToken).ConfigureAwait(false);

        if (pushResponse.StatusCode != HttpStatusCode.Accepted)
        {
            await pushResponse.LogHttpResponseAsync(_logger, cancellationToken).ConfigureAwait(false);
            string errorMessage = Resource.FormatString(nameof(Strings.BlobUploadFailed), $"POST {startUploadUri}", pushResponse.StatusCode);
            throw new ApplicationException(errorMessage);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return GetNextLocation(pushResponse);
    }

    private Task<UriBuilder> UploadBlobContentsAsync(string repository, string digest, Stream contents, HttpClient client, UriBuilder uploadUri, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (s_ForceChunkedUploadEnabled)
        {
            //the chunked upload was forced in configuration
            _logger.LogTrace("Chunked upload is forced in configuration, attempting to upload blob in chunks. Content length: {0}.", contents.Length);
            return UploadBlobChunkedAsync(repository, digest, contents, client, uploadUri, cancellationToken);
        }

        try
        {
            _logger.LogTrace("Attempting to upload whole blob, content length: {0}.", contents.Length);
            return UploadBlobWholeAsync(repository, digest, contents, client, uploadUri, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogTrace("Errored while uploading whole blob: {0}.\nRetrying with chunked upload. Content length: {1}.", ex, contents.Length);
            contents.Seek(0, SeekOrigin.Begin);
            return UploadBlobChunkedAsync(repository, digest, contents, client, uploadUri, cancellationToken);
        }
    }

    private async Task FinishUploadSessionAsync(string digest, HttpClient client, UriBuilder uploadUri, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // PUT with digest to finalize
        uploadUri.Query += $"&digest={Uri.EscapeDataString(digest)}";

        var putUri = uploadUri.Uri;

        HttpResponseMessage finalizeResponse = await client.PutAsync(putUri, content: null, cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        if (finalizeResponse.StatusCode != HttpStatusCode.Created)
        {
            await finalizeResponse.LogHttpResponseAsync(_logger, cancellationToken).ConfigureAwait(false);
            string errorMessage = Resource.FormatString(nameof(Strings.BlobUploadFailed), $"PUT {putUri}", finalizeResponse.StatusCode);
            throw new ApplicationException(errorMessage);
        }
    }

    private async Task UploadBlobAsync(string repository, string digest, Stream contents, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        HttpClient client = GetClient();

        if (await BlobAlreadyUploadedAsync(repository, digest, client, cancellationToken).ConfigureAwait(false))
        {
            // Already there!
            return;
        }

        // Three steps to this process:
        // * start an upload session
        cancellationToken.ThrowIfCancellationRequested();
        var uploadUri = await StartUploadSessionAsync(repository, digest, client, cancellationToken).ConfigureAwait(false);
        _logger.LogTrace("Started upload session for {0}", digest);

        // * upload the blob
        cancellationToken.ThrowIfCancellationRequested();
        var finalChunkUri = await UploadBlobContentsAsync(repository, digest, contents, client, uploadUri, cancellationToken).ConfigureAwait(false);
        _logger.LogTrace("Uploaded content for {0}", digest);
        // * finish the upload session
        cancellationToken.ThrowIfCancellationRequested();
        await FinishUploadSessionAsync(digest, client, finalChunkUri, cancellationToken).ConfigureAwait(false);
        _logger.LogTrace("Finalized upload session for {0}", digest);

    }

    private async Task<bool> BlobAlreadyUploadedAsync(string repository, string digest, HttpClient client, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        HttpResponseMessage response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, new Uri(BaseUri, $"/v2/{repository}/blobs/{digest}")), cancellationToken).ConfigureAwait(false);

        return response.StatusCode == HttpStatusCode.OK;
    }

    private HttpClient GetClient()
    {
        return _client;
    }

    private HttpClient CreateClient()
    {
        HttpMessageHandler clientHandler = new AuthHandshakeMessageHandler(new SocketsHttpHandler() { PooledConnectionLifetime = TimeSpan.FromMilliseconds(10 /* total guess */) });

        if(IsAmazonECRRegistry)
        {
            clientHandler = new AmazonECRMessageHandler(clientHandler);
        }

        HttpClient client = new(clientHandler);

        client.DefaultRequestHeaders.Add("User-Agent", $".NET Container Library v{Constants.Version}");

        return client;
    }

    private static void AddDockerFormatsAcceptHeader(HttpRequestMessage request)
    {
        request.Headers.Accept.Clear();
        request.Headers.Accept.Add(new("application/json"));
        request.Headers.Accept.Add(new(DockerManifestListV2));
        request.Headers.Accept.Add(new(DockerManifestV2));
        request.Headers.Accept.Add(new(OciManifestV1));
        request.Headers.Accept.Add(new(DockerContainerV1));
    }

    public async Task PushAsync(BuiltImage builtImage, ImageReference source, ImageReference destination, Action<string> logProgressMessage, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        HttpClient client = GetClient();
        Registry destinationRegistry = destination.Registry!;

        Func<Descriptor, Task> uploadLayerFunc = async (descriptor) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            string digest = descriptor.Digest;

            _logger.LogInformation(Strings.Registry_LayerUploadStarted, digest, destinationRegistry.RegistryName);
            if (await destinationRegistry.BlobAlreadyUploadedAsync(destination.Repository, digest, client, cancellationToken).ConfigureAwait(false))
            {
                _logger.LogInformation(Strings.Registry_LayerExists, digest);
                return;
            }

            // Blob wasn't there; can we tell the server to get it from the base image?
            HttpResponseMessage pushResponse = await client.PostAsync(new Uri(destinationRegistry.BaseUri, $"/v2/{destination.Repository}/blobs/uploads/?mount={digest}&from={source.Repository}"), content: null).ConfigureAwait(false);

            if (pushResponse.StatusCode != HttpStatusCode.Created)
            {
                // The blob wasn't already available in another namespace, so fall back to explicitly uploading it

                if (source.Registry is { } sourceRegistry)
                {
                    // Ensure the blob is available locally
                    await sourceRegistry.DownloadBlobAsync(source.Repository, descriptor, cancellationToken).ConfigureAwait(false);
                    // Then push it to the destination registry
                    await destinationRegistry.PushAsync(Layer.FromDescriptor(descriptor), destination.Repository, logProgressMessage, cancellationToken).ConfigureAwait(false);
                    _logger.LogInformation(Strings.Registry_LayerUploaded, digest, destinationRegistry.RegistryName);
                }
                else {
                    throw new NotImplementedException(Resource.GetString(nameof(Strings.MissingLinkToRegistry)));
                }
            }
        };

        if (SupportsParallelUploads)
        {
            await Task.WhenAll(builtImage.LayerDescriptors.Select(descriptor => uploadLayerFunc(descriptor))).ConfigureAwait(false);
        }
        else
        {
            foreach(var descriptor in builtImage.LayerDescriptors)
            {
                await uploadLayerFunc(descriptor).ConfigureAwait(false);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        using (MemoryStream stringStream = new MemoryStream(Encoding.UTF8.GetBytes(builtImage.Config)))
        {
            var configDigest = builtImage.ImageDigest;
            _logger.LogInformation(Strings.Registry_ConfigUploadStarted, configDigest);
            await UploadBlobAsync(destination.Repository, configDigest, stringStream, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation(Strings.Registry_ConfigUploaded);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var manifestDigest = builtImage.Manifest.GetDigest();
        _logger.LogInformation(Strings.Registry_ManifestUploadStarted, RegistryName, manifestDigest);
        string jsonString = JsonSerializer.SerializeToNode(builtImage.Manifest)?.ToJsonString() ?? "";
        HttpContent manifestUploadContent = new StringContent(jsonString);
        manifestUploadContent.Headers.ContentType = new MediaTypeHeaderValue(DockerManifestV2);
        var putResponse = await client.PutAsync(new Uri(BaseUri, $"/v2/{destination.Repository}/manifests/{manifestDigest}"), manifestUploadContent, cancellationToken).ConfigureAwait(false);

        if (!putResponse.IsSuccessStatusCode)
        {
            throw new ContainerHttpException(Resource.GetString(nameof(Strings.RegistryPushFailed)), putResponse.RequestMessage?.RequestUri?.ToString(), jsonString);
        }
        _logger.LogInformation(Strings.Registry_ManifestUploaded, RegistryName);

        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogInformation(Strings.Registry_TagUploadStarted, destination.Tag, RegistryName);
        var putResponse2 = await client.PutAsync(new Uri(BaseUri, $"/v2/{destination.Repository}/manifests/{destination.Tag}"), manifestUploadContent, cancellationToken).ConfigureAwait(false);

        if (!putResponse2.IsSuccessStatusCode)
        {
            throw new ContainerHttpException(Resource.GetString(nameof(Strings.RegistryPushFailed)), putResponse2.RequestMessage?.RequestUri?.ToString(), jsonString);
        }

        _logger.LogInformation(Strings.Registry_TagUploaded, destination.Tag, RegistryName);
    }
}
