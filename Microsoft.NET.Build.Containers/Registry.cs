using Microsoft.VisualBasic;

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Extensions.Caching.Memory;

using Valleysoft.DockerCredsProvider;
using Microsoft.Extensions.Options;

namespace Microsoft.NET.Build.Containers;

#nullable enable

/// <summary>
/// A delegating handler that performs the Docker auth handshake as described <see href="https://docs.docker.com/registry/spec/auth/token/">in their docs</see> if a request isn't authenticated
/// </summary>
public class AuthHandshakeMessageHandler : DelegatingHandler {
    private record AuthInfo(Uri Realm, string Service, string Scope);
    private MemoryCache tokenCache = new MemoryCache(new OptionsWrapper<MemoryCacheOptions>(new MemoryCacheOptions()));

    /// <summary>
    /// the www-authenticate header must have realm, service, and scope information, so this method parses it into that shape if present
    /// </summary>
    /// <param name="msg"></param>
    /// <param name="authInfo"></param>
    /// <returns></returns>
    private static bool TryParseAuthenticationInfo(HttpResponseMessage msg, [NotNullWhen(true)]out AuthInfo? authInfo)
    {
        authInfo = null;

        var authenticateHeader = msg.Headers.WwwAuthenticate;
        if (!authenticateHeader.Any()) 
        {
            return false;
        }

        AuthenticationHeaderValue header = authenticateHeader.First();
        if (header is { Scheme: "Bearer", Parameter: string args })
        {
            Regex bearerParameterSplitter = new(@"(?<key>\w+)=""(?<value>[^""]*)""(?:,|$)");

            Dictionary<string, string> keyValues = new();

            foreach (Match match in bearerParameterSplitter.Matches(args))
            {
                keyValues.Add(match.Groups["key"].Value, match.Groups["value"].Value);
            }

            if (keyValues.TryGetValue("realm", out string? realm) && keyValues.TryGetValue("service", out string? service) && keyValues.TryGetValue("scope", out string? scope))
            {
                authInfo = new AuthInfo(new Uri(realm), service, scope);
                return true;
            }
        }

        return false;
    }

    public AuthHandshakeMessageHandler(HttpMessageHandler innerHandler) : base(innerHandler) { }

    /// <summary>
    /// Response to a request to get a token using some auth.
    /// </summary>
    /// <remarks>
    /// <see href="https://docs.docker.com/registry/spec/auth/token/#token-response-fields"/>
    /// </remarks>
    private record TokenResponse(string? token, string? access_token, int? expires_in, DateTimeOffset? issued_at)
    {
        public string ResolvedToken => token ?? access_token ?? throw new ArgumentException("Token response had neither token nor access_token.");
    }

    /// <summary>
    /// Uses the authentication information from a 401 response to perform the authentication dance for a given registry.
    /// Credentials for the request are retrieved from the credential provider, then used to acquire a token.
    /// That token is cached for some duration on a per-host basis.
    /// </summary>
    /// <param name="realm"></param>
    /// <param name="service"></param>
    /// <param name="scope"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private async Task<string> GetTokenAsync(Uri realm, string service, string scope, CancellationToken cancellationToken) {
        // fetch creds for the host
        DockerCredentials privateRepoCreds = await CredsProvider.GetCredentialsAsync(realm.Host);
        // use those creds when calling the token provider
        var header = privateRepoCreds.Username == "<token>" 
                        ? new AuthenticationHeaderValue("Bearer", privateRepoCreds.Password)
                        : new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{privateRepoCreds.Username}:{privateRepoCreds.Password}")));
        var builder = new UriBuilder(realm);
        var queryDict = System.Web.HttpUtility.ParseQueryString("");
        queryDict["service"] = service;
        queryDict["scope"] = scope;
        builder.Query = queryDict.ToString();
        var message = new HttpRequestMessage(HttpMethod.Get, builder.ToString());
        message.Headers.Authorization = header;

        var tokenResponse = await base.SendAsync(message, cancellationToken);
        tokenResponse.EnsureSuccessStatusCode();

        TokenResponse? token = JsonSerializer.Deserialize<TokenResponse>(tokenResponse.Content.ReadAsStream());
        if (token is null)
        {
            throw new ArgumentException("Could not deserialize token from JSON");
        }

        // save the retrieved token in the cache
        var entry = tokenCache.CreateEntry(realm.Host);
        entry.SetValue(token.ResolvedToken);
        entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(token.expires_in ?? 3600);
        return token.ResolvedToken;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        // attempt to use cached token for the request if available
        if(tokenCache.Get<string>(request.RequestUri.Host) is {} cachedToken){
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", cachedToken);
        }
        var response = await base.SendAsync(request, cancellationToken);
        if (response is { StatusCode: HttpStatusCode.OK}) {
            return response;
        } else if (response is { StatusCode: HttpStatusCode.Unauthorized} && TryParseAuthenticationInfo(response, out var authInfo)) {
            if (await GetTokenAsync(authInfo.Realm, authInfo.Service, authInfo.Scope, cancellationToken) is {} fetchedToken) {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", fetchedToken);
                return await base.SendAsync(request, cancellationToken);
            }
            return response;
        } else {
            return response;
        }
    }
}

public record struct Registry(Uri BaseUri)
{
    private const string DockerManifestV2 = "application/vnd.docker.distribution.manifest.v2+json";
    private const string DockerContainerV1 = "application/vnd.docker.container.image.v1+json";

    public async Task<Image> GetImageManifest(string name, string reference)
    {
        using HttpClient client = await GetClient();

        var response = await client.GetAsync(new Uri(BaseUri, $"/v2/{name}/manifests/{reference}"));

        response.EnsureSuccessStatusCode();

        var s = await response.Content.ReadAsStringAsync();

        var manifest = JsonNode.Parse(s);

        if (manifest is null) throw new NotImplementedException("Got a manifest but it was null");

        if ((string?)manifest["mediaType"] != DockerManifestV2)
        {
            throw new NotImplementedException($"Do not understand the mediaType {manifest["mediaType"]}");
        }

        JsonNode? config = manifest["config"];
        Debug.Assert(config is not null);
        Debug.Assert(((string?)config["mediaType"]) == DockerContainerV1);

        string? configSha = (string?)config["digest"];
        Debug.Assert(configSha is not null);

        response = await client.GetAsync(new Uri(BaseUri, $"/v2/{name}/blobs/{configSha}"));

        JsonNode? configDoc = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        Debug.Assert(configDoc is not null);
        //Debug.Assert(((string?)configDoc["mediaType"]) == DockerContainerV1);

        return new Image(manifest, configDoc, name, this);
    }

    /// <summary>
    /// Ensure a blob associated with <paramref name="name"/> from the registry is available locally.
    /// </summary>
    /// <param name="name">Name of the associated image.</param>
    /// <param name="descriptor"><see cref="Descriptor"/> that describes the blob.</param>
    /// <returns>Local path to the (decompressed) blob content.</returns>
    public async Task<string> DownloadBlob(string name, Descriptor descriptor)
    {
        string localPath = ContentStore.PathForDescriptor(descriptor);

        if (File.Exists(localPath))
        {
            // Assume file is up to date and just return it
            return localPath;
        }

        // No local copy, so download one

        using HttpClient client = await GetClient();

        var response = await client.GetAsync(new Uri(BaseUri, $"/v2/{name}/blobs/{descriptor.Digest}"));

        response.EnsureSuccessStatusCode();

        string tempTarballPath = ContentStore.GetTempFile();
        using (FileStream fs = File.Create(tempTarballPath))
        {
            Stream? gzs = null;

            Stream responseStream = await response.Content.ReadAsStreamAsync();
            if (descriptor.MediaType.EndsWith("gzip"))
            {
                gzs = new GZipStream(responseStream, CompressionMode.Decompress);
            }

            using Stream? gzipStreamToDispose = gzs;

            await (gzs ?? responseStream).CopyToAsync(fs);
        }

        File.Move(tempTarballPath, localPath, overwrite: true);

        return localPath;
    }

    public async Task Push(Layer layer, string name)
    {
        string digest = layer.Descriptor.Digest;

        using (FileStream contents = File.OpenRead(layer.BackingFile))
        {
            await UploadBlob(name, digest, contents);
        }
    }

    private readonly async Task UploadBlob(string name, string digest, Stream contents)
    {
        using HttpClient client = await GetClient();

        if (await BlobAlreadyUploaded(name, digest, client))
        {
            // Already there!
            return;
        }

        HttpResponseMessage pushResponse = await client.PostAsync(new Uri(BaseUri, $"/v2/{name}/blobs/uploads/"), content: null);

        Debug.Assert(pushResponse.StatusCode == HttpStatusCode.Accepted);

        //Uri uploadUri = new(BaseUri, pushResponse.Headers.GetValues("location").Single() + $"?digest={layer.Descriptor.Digest}");
        Debug.Assert(pushResponse.Headers.Location is not null);

        UriBuilder x;
        if (pushResponse.Headers.Location.IsAbsoluteUri)
        {
            x = new UriBuilder(pushResponse.Headers.Location);
        }
        else
        {
            x = new UriBuilder(BaseUri + pushResponse.Headers.Location.OriginalString);
        }

        x.Query += $"&digest={Uri.EscapeDataString(digest)}";

        // TODO: consider chunking
        StreamContent content = new StreamContent(contents);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Headers.ContentLength = contents.Length;
        HttpResponseMessage putResponse = await client.PutAsync(x.Uri, content);

        string resp = await putResponse.Content.ReadAsStringAsync();

        putResponse.EnsureSuccessStatusCode();
    }

    private readonly async Task<bool> BlobAlreadyUploaded(string name, string digest, HttpClient client)
    {
        HttpResponseMessage response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, new Uri(BaseUri, $"/v2/{name}/blobs/{digest}")));

        if (response.StatusCode == HttpStatusCode.OK)
        {
            return true;
        }

        return false;
    }

    private readonly async Task<HttpClient> GetClient()
    {   
        var clientHandler = new AuthHandshakeMessageHandler(new HttpClientHandler() { UseDefaultCredentials = true });
        HttpClient client = new(clientHandler);

        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue(DockerManifestV2));
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue(DockerContainerV1));

        //try
        //{
        //    DockerCredentials privateRepoCreds = await CredsProvider.GetCredentialsAsync(BaseUri.Host);
        //    if (privateRepoCreds.Username == "<token>") {
        //        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", privateRepoCreds.Password);
        //    } else {
        //        byte[] byteArray = Encoding.ASCII.GetBytes($"{privateRepoCreds.Username}:{privateRepoCreds.Password}");
        //        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
        //    }
        //}
        //catch (CredsNotFoundException)
        //{
        //    // TODO: log?
        //}

        client.DefaultRequestHeaders.Add("User-Agent", ".NET Container Library");

        return client;
    }

    public async Task Push(Image x, string name, string? tag, string baseName)
    {
        tag ??= "latest";

        using HttpClient client = await GetClient();

        foreach (var descriptor in x.LayerDescriptors)
        {
            string digest = descriptor.Digest;

            if (await BlobAlreadyUploaded(name, digest, client))
            {
                continue;
            }

            // Blob wasn't there; can we tell the server to get it from the base image?
            HttpResponseMessage pushResponse = await client.PostAsync(new Uri(BaseUri, $"/v2/{name}/blobs/uploads/?mount={digest}&from={baseName}"), content: null);

            if (pushResponse.StatusCode != HttpStatusCode.Created)
            {
                // The blob wasn't already available in another namespace, so fall back to explicitly uploading it

                // TODO: don't do this search, which is ridiculous
                foreach (Layer layer in x.newLayers)
                {
                    if (layer.Descriptor.Digest == digest)
                    {
                        await Push(layer, name);
                        break;
                    }

                    throw new NotImplementedException("Need to push a layer but it's not a new one--need to download it from the base registry and upload it");
                }
            }
        }

        using (MemoryStream stringStream = new MemoryStream(Encoding.UTF8.GetBytes(x.config.ToJsonString())))
        {
            await UploadBlob(name, x.GetDigest(x.config), stringStream);
        }

        HttpContent manifestUploadContent = new StringContent(x.manifest.ToJsonString());
        manifestUploadContent.Headers.ContentType = new MediaTypeHeaderValue(DockerManifestV2);

        var putResponse = await client.PutAsync(new Uri(BaseUri, $"/v2/{name}/manifests/{x.GetDigest(x.manifest)}"), manifestUploadContent);

        string putresponsestr = await putResponse.Content.ReadAsStringAsync();

        putResponse.EnsureSuccessStatusCode();

        var putResponse2 = await client.PutAsync(new Uri(BaseUri, $"/v2/{name}/manifests/{tag}"), manifestUploadContent);

        putResponse2.EnsureSuccessStatusCode();
    }
}