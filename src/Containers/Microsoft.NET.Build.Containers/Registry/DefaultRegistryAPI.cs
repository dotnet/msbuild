// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.Extensions.Logging;

namespace Microsoft.NET.Build.Containers;

internal class DefaultRegistryAPI : IRegistryAPI
{
    private readonly Uri _baseUri;
    private readonly HttpClient _client;
    private readonly ILogger _logger;

    internal DefaultRegistryAPI(Uri baseUri, ILogger logger)
    {
        bool isAmazonECRRegistry = baseUri.IsAmazonECRRegistry();
        _baseUri = baseUri;
        _logger = logger;
        _client = CreateClient(baseUri, isAmazonECRRegistry);
        Manifest = new DefaultManifestOperations(_baseUri, _client, _logger);
        Blob = new DefaultBlobOperations(_baseUri, _client, _logger);
    }

    public IBlobOperations Blob { get; }

    public IManifestOperations Manifest { get; }

    private static HttpClient CreateClient(Uri baseUri, bool isAmazonECRRegistry = false)
    {
        HttpMessageHandler clientHandler = new AuthHandshakeMessageHandler(new SocketsHttpHandler() { PooledConnectionLifetime = TimeSpan.FromMilliseconds(10 /* total guess */) });

        if (isAmazonECRRegistry)
        {
            clientHandler = new AmazonECRMessageHandler(clientHandler);
        }

        HttpClient client = new(clientHandler);

        client.DefaultRequestHeaders.Add("User-Agent", $".NET Container Library v{Constants.Version}");

        return client;
    }
}
