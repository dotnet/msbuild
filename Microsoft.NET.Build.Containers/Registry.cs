// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using NuGet.RuntimeModel;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Microsoft.NET.Build.Containers;

internal sealed class Registry
{
    private const string DockerManifestV2 = "application/vnd.docker.distribution.manifest.v2+json";
    private const string DockerManifestListV2 = "application/vnd.docker.distribution.manifest.list.v2+json";
    private const string DockerContainerV1 = "application/vnd.docker.container.image.v1+json";


    private string RegistryName => BaseUri.GetComponents(UriComponents.HostAndPort, UriFormat.Unescaped);

    public Registry(Uri baseUri)
    {
        BaseUri = baseUri;
        _client = CreateClient();
    }

    public Uri BaseUri { get; }

    /// <summary>
    /// The max chunk size for patch blob uploads.
    /// </summary>
    /// <remarks>
    /// This varies by registry target, for example Amazon Elastic Container Registry requires 5MB chunks for all but the last chunk.
    /// </remarks>
    public int MaxChunkSizeBytes => IsAmazonECRRegistry ? 5248080 : 1024 * 64;

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
    /// Check to see if the registry is for Google Artifact Registry.
    /// </summary>
    /// <remarks>
    /// Google Artifact Registry locations (one for each availability zone) are of the form "ZONE-docker.pkg.dev".
    /// </remarks>
    public bool IsGoogleArtifactRegistry {
        get => RegistryName.EndsWith("-docker.pkg.dev", StringComparison.Ordinal);
    }

    /// <summary>
    /// Google Artifact Registry doesn't support chunked upload, but we want the capability check to be agnostic to the target.
    /// </summary>
    private bool SupportsChunkedUpload => !IsGoogleArtifactRegistry;

    /// <summary>
    /// Pushing to ECR uses a much larger chunk size. To avoid getting too many socket disconnects trying to do too many
    /// parallel uploads be more conservative and upload one layer at a time.
    /// </summary>
    private bool SupportsParallelUploads => !IsAmazonECRRegistry;

    public async Task<ImageBuilder> GetImageManifest(string repositoryName, string reference, string runtimeIdentifier, string runtimeIdentifierGraphPath)
    {
        var initialManifestResponse = await GetManifest(repositoryName, reference).ConfigureAwait(false);

        return initialManifestResponse.Content.Headers.ContentType?.MediaType switch {
            DockerManifestV2 => await ReadSingleImage(repositoryName, await initialManifestResponse.Content.ReadFromJsonAsync<ManifestV2>().ConfigureAwait(false)).ConfigureAwait(false),
            DockerManifestListV2 => await PickBestImageFromManifestList(repositoryName, reference, await initialManifestResponse.Content.ReadFromJsonAsync<ManifestListV2>().ConfigureAwait(false), runtimeIdentifier, runtimeIdentifierGraphPath).ConfigureAwait(false),
            var unknownMediaType => throw new NotImplementedException($"The manifest for {repositoryName}:{reference} from registry {BaseUri} was an unknown type: {unknownMediaType}. Please raise an issue at https://github.com/dotnet/sdk-container-builds/issues with this message.")
        };
    }

    private async Task<ImageBuilder> ReadSingleImage(string repositoryName, ManifestV2 manifest)
    {
        var config = manifest.Config;
        string configSha = config.digest;

        var blobResponse = await GetBlob(repositoryName, configSha).ConfigureAwait(false);

        JsonNode? configDoc = JsonNode.Parse(await blobResponse.Content.ReadAsStringAsync().ConfigureAwait(false));
        Debug.Assert(configDoc is not null);

        return new ImageBuilder(manifest, new ImageConfig(configDoc));
    }

    async Task<ImageBuilder> PickBestImageFromManifestList(string repositoryName, string reference, ManifestListV2 manifestList, string runtimeIdentifier, string runtimeIdentifierGraphPath) {
        var runtimeGraph = GetRuntimeGraphForDotNet(runtimeIdentifierGraphPath);
        var (ridDict, graphForManifestList) = ConstructRuntimeGraphForManifestList(manifestList, runtimeGraph);
        var bestManifestRid = CheckIfRidExistsInGraph(graphForManifestList, ridDict.Keys, runtimeIdentifier);
        if (bestManifestRid is null) {
            throw new BaseImageNotFoundException(runtimeIdentifier, repositoryName, reference, graphForManifestList.Runtimes.Keys);
        }
        var matchingManifest = ridDict[bestManifestRid];
        var manifestResponse = await GetManifest(repositoryName, matchingManifest.digest).ConfigureAwait(false);
        return await ReadSingleImage(repositoryName, await manifestResponse.Content.ReadFromJsonAsync<ManifestV2>().ConfigureAwait(false)).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> GetManifest(string repositoryName, string reference)
    {
        var client = GetClient();
        var response = await client.GetAsync(new Uri(BaseUri, $"/v2/{repositoryName}/manifests/{reference}")).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return response;
    }

    async Task<HttpResponseMessage> GetBlob(string repositoryName, string digest)
    {
        var client = GetClient();
        var response = await client.GetAsync(new Uri(BaseUri, $"/v2/{repositoryName}/blobs/{digest}")).ConfigureAwait(false);
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
        // there are theoretically other platforms/architectures that Docker supports (s390x?), but we are
        // deliberately ignoring them without clear user signal.
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
    public async Task<string> DownloadBlob(string repository, Descriptor descriptor)
    {
        string localPath = ContentStore.PathForDescriptor(descriptor);

        if (File.Exists(localPath))
        {
            // Assume file is up to date and just return it
            return localPath;
        }

        // No local copy, so download one

        HttpClient client = GetClient();

        var response = await client.GetAsync(new Uri(BaseUri, $"/v2/{repository}/blobs/{descriptor.Digest}"), HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        string tempTarballPath = ContentStore.GetTempFile();
        using (FileStream fs = File.Create(tempTarballPath))
        {
            using Stream responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);

            await responseStream.CopyToAsync(fs).ConfigureAwait(false);
        }

        File.Move(tempTarballPath, localPath, overwrite: true);

        return localPath;
    }

    public async Task Push(Layer layer, string repository, Action<string> logProgressMessage)
    {
        string digest = layer.Descriptor.Digest;

        using (FileStream contents = File.OpenRead(layer.BackingFile))
        {
            await UploadBlob(repository, digest, contents).ConfigureAwait(false);
        }
    }

    private async Task<UriBuilder> UploadBlobChunked(string repository, string digest, Stream contents, HttpClient client, UriBuilder uploadUri) {
        Uri patchUri = uploadUri.Uri;
        var localUploadUri = new UriBuilder(uploadUri.Uri);
        localUploadUri.Query += $"&digest={Uri.EscapeDataString(digest)}";

        // TODO: this chunking is super tiny and probably not necessary; what does the docker client do
        //       and can we be smarter?

        byte[] chunkBackingStore = new byte[MaxChunkSizeBytes];

        int chunkCount = 0;
        int chunkStart = 0;

        while (contents.Position < contents.Length)
        {
            int bytesRead = await contents.ReadAsync(chunkBackingStore).ConfigureAwait(false);

            ByteArrayContent content = new (chunkBackingStore, offset: 0, count: bytesRead);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content.Headers.ContentLength = bytesRead;

            // manual because ACR throws an error with the .NET type {"Range":"bytes 0-84521/*","Reason":"the Content-Range header format is invalid"}
            //    content.Headers.Add("Content-Range", $"0-{contents.Length - 1}");
            Debug.Assert(content.Headers.TryAddWithoutValidation("Content-Range", $"{chunkStart}-{chunkStart + bytesRead - 1}"));

            HttpResponseMessage patchResponse = await client.PatchAsync(patchUri, content).ConfigureAwait(false);

            // Fail the upload if the response code is not Accepted (202) or if uploading to Amazon ECR which returns back Created (201).
            if (!(patchResponse.StatusCode == HttpStatusCode.Accepted || (IsAmazonECRRegistry && patchResponse.StatusCode == HttpStatusCode.Created)))
            {
                string errorMessage = $"Failed to upload blob to {patchUri}; received {patchResponse.StatusCode} with detail {await patchResponse.Content.ReadAsStringAsync().ConfigureAwait(false)}";
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

    private async Task<UriBuilder> UploadBlobWhole(string repository, string digest, Stream contents, HttpClient client, UriBuilder uploadUri) {
        StreamContent content = new StreamContent(contents);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Headers.ContentLength = contents.Length;
        HttpResponseMessage patchResponse = await client.PatchAsync(uploadUri.Uri, content).ConfigureAwait(false);
        if (patchResponse.StatusCode != HttpStatusCode.Accepted)
        {
            string errorMessage = $"Failed to upload to {uploadUri}; received {patchResponse.StatusCode} with detail {await patchResponse.Content.ReadAsStringAsync().ConfigureAwait(false)}";
            throw new ApplicationException(errorMessage);
        }
        return GetNextLocation(patchResponse);
    }

    private async Task<UriBuilder> StartUploadSession(string repository, string digest, HttpClient client) {
        Uri startUploadUri = new Uri(BaseUri, $"/v2/{repository}/blobs/uploads/");

        HttpResponseMessage pushResponse = await client.PostAsync(startUploadUri, content: null).ConfigureAwait(false);

        if (pushResponse.StatusCode != HttpStatusCode.Accepted)
        {
            string errorMessage = $"Failed to upload blob to {startUploadUri}; received {pushResponse.StatusCode} with detail {await pushResponse.Content.ReadAsStringAsync().ConfigureAwait(false)}";
            throw new ApplicationException(errorMessage);
        }

        return GetNextLocation(pushResponse);
    }

    private async Task<UriBuilder> UploadBlobContents(string repository, string digest, Stream contents, HttpClient client, UriBuilder uploadUri) {
        if (SupportsChunkedUpload) return await UploadBlobChunked(repository, digest, contents, client, uploadUri).ConfigureAwait(false);
        else return await UploadBlobWhole(repository, digest, contents, client, uploadUri).ConfigureAwait(false);
    }

    private static async Task FinishUploadSession(string digest, HttpClient client, UriBuilder uploadUri) {
        // PUT with digest to finalize
        uploadUri.Query += $"&digest={Uri.EscapeDataString(digest)}";

        var putUri = uploadUri.Uri;

        HttpResponseMessage finalizeResponse = await client.PutAsync(putUri, content: null).ConfigureAwait(false);

        if (finalizeResponse.StatusCode != HttpStatusCode.Created)
        {
            string errorMessage = $"Failed to finalize upload to {putUri}; received {finalizeResponse.StatusCode} with detail {await finalizeResponse.Content.ReadAsStringAsync().ConfigureAwait(false)}";
            throw new ApplicationException(errorMessage);
        }
    }

    private async Task UploadBlob(string repository, string digest, Stream contents)
    {
        HttpClient client = GetClient();

        if (await BlobAlreadyUploaded(repository, digest, client).ConfigureAwait(false))
        {
            // Already there!
            return;
        }

        // Three steps to this process:
        // * start an upload session
        var uploadUri = await StartUploadSession(repository, digest, client).ConfigureAwait(false);
        // * upload the blob
        var finalChunkUri = await UploadBlobContents(repository, digest, contents, client, uploadUri).ConfigureAwait(false);
        // * finish the upload session
        await FinishUploadSession(digest, client, finalChunkUri).ConfigureAwait(false);

    }

    private async Task<bool> BlobAlreadyUploaded(string repository, string digest, HttpClient client)
    {
        HttpResponseMessage response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, new Uri(BaseUri, $"/v2/{repository}/blobs/{digest}"))).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            return true;
        }

        return false;
    }

    private readonly HttpClient _client;

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

        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new("application/json"));
        client.DefaultRequestHeaders.Accept.Add(new(DockerManifestListV2));
        client.DefaultRequestHeaders.Accept.Add(new(DockerManifestV2));
        client.DefaultRequestHeaders.Accept.Add(new(DockerContainerV1));

        client.DefaultRequestHeaders.Add("User-Agent", ".NET Container Library");

        return client;
    }

    public async Task Push(BuiltImage builtImage, ImageReference source, ImageReference destination, Action<string> logProgressMessage)
    {

        HttpClient client = GetClient();
        Registry destinationRegistry = destination.Registry!;

        Func<Descriptor, Task> uploadLayerFunc = async (descriptor) =>
        {
            string digest = descriptor.Digest;

            logProgressMessage($"Uploading layer {digest} to {destinationRegistry.RegistryName}");
            if (await destinationRegistry.BlobAlreadyUploaded(destination.Repository, digest, client).ConfigureAwait(false))
            {
                logProgressMessage($"Layer {digest} already existed");
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
                    await sourceRegistry.DownloadBlob(source.Repository, descriptor).ConfigureAwait(false);
                    // Then push it to the destination registry
                    await destinationRegistry.Push(Layer.FromDescriptor(descriptor), destination.Repository, logProgressMessage).ConfigureAwait(false);
                    logProgressMessage($"Finished uploading layer {digest} to {destinationRegistry.RegistryName}");
                }
                else {
                    throw new NotImplementedException("Need a good error for 'couldn't download a thing because no link to registry'");
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

        using (MemoryStream stringStream = new MemoryStream(Encoding.UTF8.GetBytes(builtImage.Config)))
        {
            var configDigest = builtImage.ImageDigest;
            logProgressMessage($"Uploading config to registry at blob {configDigest}");
            await UploadBlob(destination.Repository, configDigest, stringStream).ConfigureAwait(false);
            logProgressMessage($"Uploaded config to registry");
        }

        var manifestDigest = builtImage.Manifest.GetDigest();
        logProgressMessage($"Uploading manifest to registry {RegistryName} as blob {manifestDigest}");
        string jsonString = JsonSerializer.SerializeToNode(builtImage.Manifest)?.ToJsonString() ?? "";
        HttpContent manifestUploadContent = new StringContent(jsonString);
        manifestUploadContent.Headers.ContentType = new MediaTypeHeaderValue(DockerManifestV2);
        var putResponse = await client.PutAsync(new Uri(BaseUri, $"/v2/{destination.Repository}/manifests/{manifestDigest}"), manifestUploadContent).ConfigureAwait(false);

        if (!putResponse.IsSuccessStatusCode)
        {
            throw new ContainerHttpException("Registry push failed.", putResponse.RequestMessage?.RequestUri?.ToString(), jsonString);
        }
        logProgressMessage($"Uploaded manifest to {RegistryName}");

        logProgressMessage($"Uploading tag {destination.Tag} to {RegistryName}");
        var putResponse2 = await client.PutAsync(new Uri(BaseUri, $"/v2/{destination.Repository}/manifests/{destination.Tag}"), manifestUploadContent).ConfigureAwait(false);

        if (!putResponse2.IsSuccessStatusCode)
        {
            throw new ContainerHttpException("Registry push failed.", putResponse2.RequestMessage?.RequestUri?.ToString(), jsonString);
        }

        logProgressMessage($"Uploaded tag {destination.Tag} to {RegistryName}");
    }
}
