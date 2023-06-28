// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.NET.Build.Containers.Resources;
using NuGet.RuntimeModel;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;

namespace Microsoft.NET.Build.Containers;

internal sealed class Registry
{
    private static readonly int s_defaultChunkSizeBytes = 1024 * 64;

    private readonly ILogger _logger;
    private readonly IRegistryAPI _registryAPI;
    private readonly RegistrySettings _settings;

    /// <summary>
    /// The name of the registry, which is the host name, optionally followed by a colon and the port number.
    /// This is used in user-facing error messages, and it should match what the user would manually enter as
    /// part of Docker commands like `docker login`.
    /// </summary>
    public string RegistryName { get; init; }

    internal Registry(Uri baseUri, ILogger logger) : this(baseUri, logger, new DefaultRegistryAPI(baseUri, logger), new RegistrySettings()) { }

    internal Registry(Uri baseUri, ILogger logger, IRegistryAPI registryAPI, RegistrySettings settings)
    {
        BaseUri = baseUri;
        _logger = logger;
        _registryAPI = registryAPI;
        _settings = settings;
        RegistryName = DeriveRegistryName(baseUri);
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
    public int MaxChunkSizeBytes => _settings.ChunkedUploadSizeBytes.HasValue ? _settings.ChunkedUploadSizeBytes.Value : (IsAmazonECRRegistry ? 5248080 : s_defaultChunkSizeBytes);

    public bool IsAmazonECRRegistry => BaseUri.IsAmazonECRRegistry();

    /// <summary>
    /// Check to see if the registry is GitHub Packages, which always uses ghcr.io.
    /// </summary>
    public bool IsGithubPackageRegistry => RegistryName.StartsWith("ghcr.io", StringComparison.Ordinal);

    /// <summary>
    /// Check to see if the registry is Docker Hub, which uses two well-known domains.
    /// </summary>
    public bool IsDockerHub => RegistryName.Equals("registry-1.docker.io", StringComparison.Ordinal) || RegistryName.Equals("registry.hub.docker.com", StringComparison.Ordinal);

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
    private bool SupportsParallelUploads => !IsAmazonECRRegistry && _settings.ParallelUploadEnabled;

    public async Task<ImageBuilder> GetImageManifestAsync(string repositoryName, string reference, string runtimeIdentifier, string runtimeIdentifierGraphPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        HttpResponseMessage initialManifestResponse = await _registryAPI.Manifest.GetAsync(repositoryName, reference, cancellationToken).ConfigureAwait(false);

        return initialManifestResponse.Content.Headers.ContentType?.MediaType switch
        {
            SchemaTypes.DockerManifestV2 or SchemaTypes.OciManifestV1 => await ReadSingleImageAsync(
                repositoryName,
                await initialManifestResponse.Content.ReadFromJsonAsync<ManifestV2>(cancellationToken: cancellationToken).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false),
            SchemaTypes.DockerManifestListV2 => await PickBestImageFromManifestListAsync(
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
        ManifestConfig config = manifest.Config;
        string configSha = config.digest;

        JsonNode configDoc = await _registryAPI.Blob.GetJsonAsync(repositoryName, configSha, cancellationToken).ConfigureAwait(false);

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
        PlatformSpecificManifest matchingManifest = ridDict[bestManifestRid];
        HttpResponseMessage manifestResponse = await _registryAPI.Manifest.GetAsync(repositoryName, matchingManifest.digest, cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        return await ReadSingleImageAsync(
            repositoryName,
            await manifestResponse.Content.ReadFromJsonAsync<ManifestV2>(cancellationToken: cancellationToken).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);
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
        using Stream responseStream = await _registryAPI.Blob.GetStreamAsync(repository, descriptor.Digest, cancellationToken).ConfigureAwait(false);

        string tempTarballPath = ContentStore.GetTempFile();
        using (FileStream fs = File.Create(tempTarballPath))
        {
            await responseStream.CopyToAsync(fs, cancellationToken).ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();

        File.Move(tempTarballPath, localPath, overwrite: true);

        return localPath;
    }

    internal async Task PushLayerAsync(Layer layer, string repository, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string digest = layer.Descriptor.Digest;

        using (Stream contents = layer.OpenBackingFile())
        {
            await UploadBlobAsync(repository, digest, contents, cancellationToken).ConfigureAwait(false);
        }
    }

    internal async Task<FinalizeUploadInformation> UploadBlobChunkedAsync(Stream contents, StartUploadInformation startUploadInformation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Uri patchUri = startUploadInformation.UploadUri;

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
            
            NextChunkUploadInformation nextChunk = await _registryAPI.Blob.Upload.UploadChunkAsync(patchUri, content, cancellationToken).ConfigureAwait(false);
            patchUri = nextChunk.UploadUri;

            chunkCount += 1;
            chunkStart += bytesRead;
        }
        return new(patchUri);
    }

    private Task<FinalizeUploadInformation> UploadBlobContentsAsync(Stream contents, StartUploadInformation startUploadInformation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_settings.ForceChunkedUpload)
        {
            //the chunked upload was forced in configuration
            _logger.LogTrace("Chunked upload is forced in configuration, attempting to upload blob in chunks. Content length: {0}.", contents.Length);
            return UploadBlobChunkedAsync(contents, startUploadInformation, cancellationToken);
        }

        try
        {
            _logger.LogTrace("Attempting to upload whole blob, content length: {0}.", contents.Length);
            return _registryAPI.Blob.Upload.UploadAtomicallyAsync(startUploadInformation.UploadUri, contents, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogTrace("Errored while uploading whole blob: {0}.\nRetrying with chunked upload. Content length: {1}.", ex, contents.Length);
            contents.Seek(0, SeekOrigin.Begin);
            return UploadBlobChunkedAsync(contents, startUploadInformation, cancellationToken);
        }
    }

    private async Task UploadBlobAsync(string repository, string digest, Stream contents, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (await _registryAPI.Blob.ExistsAsync(repository, digest, cancellationToken).ConfigureAwait(false))
        {
            // Already there!
            _logger.LogInformation(Strings.Registry_LayerExists, digest);
            return;
        }

        // Three steps to this process:
        // * start an upload session
        StartUploadInformation uploadUri = await _registryAPI.Blob.Upload.StartAsync(repository, cancellationToken).ConfigureAwait(false);
        _logger.LogTrace("Started upload session for {0}", digest);

        // * upload the blob
        cancellationToken.ThrowIfCancellationRequested();
        FinalizeUploadInformation finalChunkUri = await UploadBlobContentsAsync(contents, uploadUri, cancellationToken).ConfigureAwait(false);
        _logger.LogTrace("Uploaded content for {0}", digest);
        // * finish the upload session
        cancellationToken.ThrowIfCancellationRequested();
        await _registryAPI.Blob.Upload.CompleteAsync(finalChunkUri.UploadUri, digest, cancellationToken).ConfigureAwait(false);
        _logger.LogTrace("Finalized upload session for {0}", digest);

    }

    public async Task PushAsync(BuiltImage builtImage, ImageReference source, ImageReference destination, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Registry destinationRegistry = destination.Registry!;

        Func<Descriptor, Task> uploadLayerFunc = async (descriptor) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            string digest = descriptor.Digest;

            _logger.LogInformation(Strings.Registry_LayerUploadStarted, digest, destinationRegistry.RegistryName);
            if (await _registryAPI.Blob.ExistsAsync(destination.Repository, digest, cancellationToken).ConfigureAwait(false))
            {
                _logger.LogInformation(Strings.Registry_LayerExists, digest);
                return;
            }

            // Blob wasn't there; can we tell the server to get it from the base image?
            if (! await _registryAPI.Blob.Upload.TryMountAsync(destination.Repository, source.Repository, digest, cancellationToken).ConfigureAwait(false))
            {
                // The blob wasn't already available in another namespace, so fall back to explicitly uploading it

                if (source.Registry is { } sourceRegistry)
                {
                    // Ensure the blob is available locally
                    await sourceRegistry.DownloadBlobAsync(source.Repository, descriptor, cancellationToken).ConfigureAwait(false);
                    // Then push it to the destination registry
                    await destinationRegistry.PushLayerAsync(Layer.FromDescriptor(descriptor), destination.Repository, cancellationToken).ConfigureAwait(false);
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

        //manifest upload
        string manifestDigest = builtImage.Manifest.GetDigest();
        _logger.LogInformation(Strings.Registry_ManifestUploadStarted, RegistryName, manifestDigest);
        await _registryAPI.Manifest.PutAsync(destination.Repository, manifestDigest, builtImage.Manifest, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(Strings.Registry_ManifestUploaded, RegistryName);

        //tag upload
        _logger.LogInformation(Strings.Registry_TagUploadStarted, destination.Tag, RegistryName);
        await _registryAPI.Manifest.PutAsync(destination.Repository, destination.Tag, builtImage.Manifest, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(Strings.Registry_TagUploaded, destination.Tag, RegistryName);
    }
}
