// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.NET.Build.Containers.Resources;
using Microsoft.NET.TestFramework;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Containers.UnitTests;

public class RegistryTests : IDisposable
{
    private ITestOutputHelper _testOutput;
    private readonly TestLoggerFactory _loggerFactory;

    public RegistryTests(ITestOutputHelper testOutput)
    {
        _testOutput = testOutput;
        _loggerFactory = new TestLoggerFactory(testOutput);
    }

    public void Dispose()
    {
        _loggerFactory.Dispose();
    }

    [InlineData("public.ecr.aws", true)]
    [InlineData("123412341234.dkr.ecr.us-west-2.amazonaws.com", true)]
    [InlineData("123412341234.dkr.ecr-fips.us-west-2.amazonaws.com", true)]
    [InlineData("notvalid.dkr.ecr.us-west-2.amazonaws.com", false)]
    [InlineData("1111.dkr.ecr.us-west-2.amazonaws.com", false)]
    [InlineData("mcr.microsoft.com", false)]
    [InlineData("localhost", false)]
    [InlineData("hub", false)]
    [Theory]
    public void CheckIfAmazonECR(string registryName, bool isECR)
    {
        ILogger logger = _loggerFactory.CreateLogger(nameof(CheckIfAmazonECR));
        Registry registry = new Registry(ContainerHelpers.TryExpandRegistryToUri(registryName), logger);
        Assert.Equal(isECR, registry.IsAmazonECRRegistry);
    }

    [InlineData("us-south1-docker.pkg.dev", true)]
    [InlineData("us.gcr.io", false)]
    [Theory]
    public void CheckIfGoogleArtifactRegistry(string registryName, bool isECR)
    {
        ILogger logger = _loggerFactory.CreateLogger(nameof(CheckIfGoogleArtifactRegistry));
        Registry registry = new Registry(ContainerHelpers.TryExpandRegistryToUri(registryName), logger);
        Assert.Equal(isECR, registry.IsGoogleArtifactRegistry);
    }

    [Fact]
    public async Task RegistriesThatProvideNoUploadSizeAttemptFullUpload()
    {
        ILogger logger = _loggerFactory.CreateLogger(nameof(RegistriesThatProvideNoUploadSizeAttemptFullUpload));
        var repoName = "testRepo";
        var layerDigest = "sha256:fafafafafafafafafafafafafafafafa";
        var mockLayer = new Mock<Layer>(MockBehavior.Strict);
        mockLayer
            .Setup(l => l.OpenBackingFile()).Returns(new MemoryStream(new byte[1000]));
        mockLayer
            .Setup(l => l.Descriptor).Returns(new Descriptor("blah", layerDigest, 1234));

        var uploadPath = new Uri("/uploads/foo/12345", UriKind.Relative);
        var api = new Mock<IRegistryAPI>(MockBehavior.Loose);
        api.Setup(api => api.Blob.ExistsAsync(repoName, layerDigest, It.IsAny<CancellationToken>())).Returns(Task.FromResult(false));
        api.Setup(api => api.Blob.Upload.StartAsync(repoName, It.IsAny<CancellationToken>())).Returns(Task.FromResult(new StartUploadInformation(uploadPath)));
        api.Setup(api => api.Blob.Upload.UploadAtomicallyAsync(uploadPath, It.IsAny<Stream>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(new FinalizeUploadInformation(uploadPath)));

        Registry registry = new(ContainerHelpers.TryExpandRegistryToUri("public.ecr.aws"), logger, api.Object, new RegistrySettings());
        await registry.PushLayerAsync(mockLayer.Object, repoName, CancellationToken.None);

        api.Verify(api => api.Blob.Upload.UploadChunkAsync(uploadPath, It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()), Times.Never());
        api.Verify(api => api.Blob.Upload.UploadAtomicallyAsync(uploadPath, It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task RegistriesThatProvideUploadSizePrefersFullUploadWhenChunkSizeIsLowerThanContentLength()
    {
        ILogger logger = _loggerFactory.CreateLogger(nameof(RegistriesThatProvideUploadSizePrefersFullUploadWhenChunkSizeIsLowerThanContentLength));
        var repoName = "testRepo";
        var layerDigest = "sha256:fafafafafafafafafafafafafafafafa";
        var mockLayer = new Mock<Layer>(MockBehavior.Strict);
        var chunkSizeLessThanContentLength = 10000;
        var registryUri = ContainerHelpers.TryExpandRegistryToUri("public.ecr.aws");
        mockLayer
            .Setup(l => l.OpenBackingFile()).Returns(new MemoryStream(new byte[100000]));
        mockLayer
            .Setup(l => l.Descriptor).Returns(new Descriptor("blah", layerDigest, 1234));

        var uploadPath = new Uri("/uploads/foo/12345", UriKind.Relative);
        var absoluteUploadUri = new Uri(registryUri, uploadPath);
        var api = new Mock<IRegistryAPI>(MockBehavior.Loose);
        var uploadedCount = 0;
        api.Setup(api => api.Blob.ExistsAsync(repoName, layerDigest, It.IsAny<CancellationToken>())).Returns(Task.FromResult(false));
        api.Setup(api => api.Blob.Upload.StartAsync(repoName, It.IsAny<CancellationToken>())).Returns(Task.FromResult(new StartUploadInformation(uploadPath)));
        api.Setup(api => api.Blob.Upload.UploadAtomicallyAsync(uploadPath, It.IsAny<Stream>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(new FinalizeUploadInformation(uploadPath)));
        api.Setup(api => api.Blob.Upload.UploadChunkAsync(It.IsIn(absoluteUploadUri, uploadPath), It.IsAny<HttpContent>(), It.IsAny<CancellationToken>())).Returns(() => {
            uploadedCount += chunkSizeLessThanContentLength;
            return Task.FromResult(ChunkUploadSuccessful(absoluteUploadUri, uploadPath, uploadedCount));
        });

        Registry registry = new(registryUri, logger, api.Object, new RegistrySettings());
        await registry.PushLayerAsync(mockLayer.Object, repoName, CancellationToken.None);

        api.Verify(api => api.Blob.Upload.UploadAtomicallyAsync(It.IsIn(absoluteUploadUri, uploadPath), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Exactly(1));
        api.Verify(api => api.Blob.Upload.UploadChunkAsync(It.IsIn(absoluteUploadUri, uploadPath), It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegistriesThatFailAtomicUploadFallbackToChunked()
    {
        ILogger logger = _loggerFactory.CreateLogger(nameof(RegistriesThatFailAtomicUploadFallbackToChunked));
        var repoName = "testRepo";
        var layerDigest = "sha256:fafafafafafafafafafafafafafafafa";
        var mockLayer = new Mock<Layer>(MockBehavior.Strict);
        var contentLength = 100000;
        var chunkSizeLessThanContentLength = 100000;
        var registryUri = ContainerHelpers.TryExpandRegistryToUri("public.ecr.aws");
        mockLayer
            .Setup(l => l.OpenBackingFile()).Returns(new MemoryStream(new byte[contentLength]));
        mockLayer
            .Setup(l => l.Descriptor).Returns(new Descriptor("blah", layerDigest, 1234));

        var uploadPath = new Uri("/uploads/foo/12345", UriKind.Relative);
        var absoluteUploadUri = new Uri(registryUri, uploadPath);
        var api = new Mock<IRegistryAPI>(MockBehavior.Loose);
        var uploadedCount = 0;
        api.Setup(api => api.Blob.ExistsAsync(repoName, layerDigest, It.IsAny<CancellationToken>())).Returns(Task.FromResult(false));
        api.Setup(api => api.Blob.Upload.StartAsync(repoName, It.IsAny<CancellationToken>())).Returns(Task.FromResult(new StartUploadInformation(uploadPath)));
        api.Setup(api => api.Blob.Upload.UploadAtomicallyAsync(It.IsIn(absoluteUploadUri, uploadPath), It.IsAny<Stream>(), It.IsAny<CancellationToken>())).Throws(new Exception("Server-side shutdown the thing"));
        api.Setup(api => api.Blob.Upload.UploadChunkAsync(It.IsIn(absoluteUploadUri, uploadPath), It.IsAny<HttpContent>(), It.IsAny<CancellationToken>())).Returns(() => {
            uploadedCount += chunkSizeLessThanContentLength;
            return Task.FromResult(ChunkUploadSuccessful(absoluteUploadUri, uploadPath, uploadedCount));
        });

        Registry registry = new(registryUri, logger, api.Object, new RegistrySettings());
        await registry.PushLayerAsync(mockLayer.Object, repoName, CancellationToken.None);

        api.Verify(api => api.Blob.Upload.UploadAtomicallyAsync(It.IsIn(absoluteUploadUri, uploadPath), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Once());
        api.Verify(api => api.Blob.Upload.UploadChunkAsync(It.IsIn(absoluteUploadUri, uploadPath), It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()), Times.Exactly(contentLength / chunkSizeLessThanContentLength));
    }

    [Fact]
    public async Task ChunkedUploadCalculatesChunksCorrectly()
    {
        ILogger logger = _loggerFactory.CreateLogger(nameof(RegistriesThatFailAtomicUploadFallbackToChunked));
        var repoName = "testRepo";
        var layerDigest = "sha256:fafafafafafafafafafafafafafafafa";
        var mockLayer = new Mock<Layer>(MockBehavior.Strict);
        var contentLength = 1000000;
        var chunkSize = 100000;
        var registryUri = ContainerHelpers.TryExpandRegistryToUri("public.ecr.aws");
        mockLayer
            .Setup(l => l.OpenBackingFile()).Returns(new MemoryStream(new byte[contentLength]));
        mockLayer
            .Setup(l => l.Descriptor).Returns(new Descriptor("blah", layerDigest, 1234));

        var uploadPath = new Uri("/uploads/foo/12345", UriKind.Relative);
        var absoluteUploadUri = new Uri(registryUri, uploadPath);
        var api = new Mock<IRegistryAPI>(MockBehavior.Loose);
        var uploadedCount = 0;
        api.Setup(api => api.Blob.ExistsAsync(repoName, layerDigest, It.IsAny<CancellationToken>())).Returns(Task.FromResult(false));
        api.Setup(api => api.Blob.Upload.StartAsync(repoName, It.IsAny<CancellationToken>())).Returns(Task.FromResult(new StartUploadInformation(uploadPath)));
        api.Setup(api => api.Blob.Upload.UploadAtomicallyAsync(It.IsIn(absoluteUploadUri, uploadPath), It.IsAny<Stream>(), It.IsAny<CancellationToken>())).Throws(new Exception("Server-side shutdown the thing"));
        api.Setup(api => api.Blob.Upload.UploadChunkAsync(It.IsIn(absoluteUploadUri, uploadPath), It.IsAny<HttpContent>(), It.IsAny<CancellationToken>())).Returns(() => {
            uploadedCount += chunkSize;
            return Task.FromResult(ChunkUploadSuccessful(absoluteUploadUri, uploadPath, uploadedCount));
        });

        RegistrySettings settings = new()
        {
            ParallelUploadEnabled = false,
            ForceChunkedUpload = false,
            ChunkedUploadSizeBytes = chunkSize,
        };

        Registry registry = new(registryUri, logger, api.Object, settings);
        await registry.PushLayerAsync(mockLayer.Object, repoName, CancellationToken.None);

        api.Verify(api => api.Blob.Upload.UploadAtomicallyAsync(It.IsIn(absoluteUploadUri, uploadPath), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Once());
        api.Verify(api => api.Blob.Upload.UploadChunkAsync(It.IsIn(absoluteUploadUri, uploadPath), It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()), Times.Exactly(10));
    }

    [Fact]
    public async Task PushAsync_Logging()
    {
        using TestLoggerFactory loggerFactory = new(_testOutput);
        List<(LogLevel, string)> loggedMessages = new();
        loggerFactory.AddProvider(new InMemoryLoggerProvider(loggedMessages));
        ILogger logger = loggerFactory.CreateLogger(nameof(PushAsync_Logging));

        var repoName = "testRepo";
        var layerDigest = "sha256:fafafafafafafafafafafafafafafafa";
        var mockLayer = new Mock<Layer>(MockBehavior.Strict);
        mockLayer
            .Setup(l => l.OpenBackingFile()).Returns(new MemoryStream(new byte[1000]));
        mockLayer
            .Setup(l => l.Descriptor).Returns(new Descriptor("blah", layerDigest, 1234));

        var uploadPath = new Uri("/uploads/foo/12345", UriKind.Relative);
        var api = new Mock<IRegistryAPI>(MockBehavior.Loose);
        api.Setup(api => api.Blob.ExistsAsync(repoName, layerDigest, It.IsAny<CancellationToken>())).Returns(Task.FromResult(false));
        api.Setup(api => api.Blob.Upload.StartAsync(repoName, It.IsAny<CancellationToken>())).Returns(Task.FromResult(new StartUploadInformation(uploadPath)));
        api.Setup(api => api.Blob.Upload.UploadAtomicallyAsync(uploadPath, It.IsAny<Stream>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(new FinalizeUploadInformation(uploadPath)));

        Registry registry = new(ContainerHelpers.TryExpandRegistryToUri("public.ecr.aws"), logger, api.Object, new RegistrySettings());
        await registry.PushLayerAsync(mockLayer.Object, repoName, CancellationToken.None);

        Assert.NotEmpty(loggedMessages);
        Assert.True(loggedMessages.All(m => m.Item1 == LogLevel.Trace));
        var messages = loggedMessages.Select(m => m.Item2).ToList();
        Assert.Contains(messages, m => m == "Started upload session for sha256:fafafafafafafafafafafafafafafafa");
        Assert.Contains(messages, m => m == "Finalized upload session for sha256:fafafafafafafafafafafafafafafafa");
    }

    [Fact]
    public async Task PushAsync_ForceChunkedUpload()
    {
        ILogger logger = _loggerFactory.CreateLogger(nameof(PushAsync_ForceChunkedUpload));

        string repoName = "testRepo";
        string layerDigest = "sha256:fafafafafafafafafafafafafafafafa";
        Mock<Layer> mockLayer = new(MockBehavior.Strict);
        int contentLength = 1000000;
        int chunkSize = 100000;
        Uri registryUri = ContainerHelpers.TryExpandRegistryToUri("public.ecr.aws");
        mockLayer
            .Setup(l => l.OpenBackingFile()).Returns(new MemoryStream(new byte[contentLength]));
        mockLayer
            .Setup(l => l.Descriptor).Returns(new Descriptor("blah", layerDigest, 1234));

        Uri uploadPath = new("/uploads/foo/12345", UriKind.Relative);
        Uri absoluteUploadUri = new(registryUri, uploadPath);
        Mock<IRegistryAPI> api = new(MockBehavior.Loose);
        int uploadedCount = 0;
        api.Setup(api => api.Blob.ExistsAsync(repoName, layerDigest, It.IsAny<CancellationToken>())).Returns(Task.FromResult(false));
        api.Setup(api => api.Blob.Upload.StartAsync(repoName, It.IsAny<CancellationToken>())).Returns(Task.FromResult(new StartUploadInformation(uploadPath)));
        api.Setup(api => api.Blob.Upload.UploadChunkAsync(It.IsIn(absoluteUploadUri, uploadPath), It.IsAny<HttpContent>(), It.IsAny<CancellationToken>())).Returns(() => {
            uploadedCount += chunkSize;
            return Task.FromResult(ChunkUploadSuccessful(absoluteUploadUri, uploadPath, uploadedCount));
        });

        RegistrySettings settings = new()
        {
            ParallelUploadEnabled = false,
            ForceChunkedUpload = true,
            ChunkedUploadSizeBytes = chunkSize,
        };

        Registry registry = new(registryUri, logger, api.Object, settings);
        await registry.PushLayerAsync(mockLayer.Object, repoName, CancellationToken.None);

        api.Verify(api => api.Blob.Upload.UploadAtomicallyAsync(It.IsIn(absoluteUploadUri, uploadPath), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Never());
        api.Verify(api => api.Blob.Upload.UploadChunkAsync(It.IsIn(absoluteUploadUri, uploadPath), It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()), Times.Exactly(10));
    }

    [Fact]
    public async Task CanParseRegistryDeclaredChunkSize_FromRange()
    {
        ILogger logger = _loggerFactory.CreateLogger(nameof(CanParseRegistryDeclaredChunkSize_FromRange));
        string repoName = "testRepo";

        Mock<HttpClient> client = new(MockBehavior.Loose);
        HttpResponseMessage httpResponse = new()
        {
            StatusCode = HttpStatusCode.Accepted,
        };
        httpResponse.Headers.Add("Range", "0-100000");
        httpResponse.Headers.Location = new Uri("https://my-registy.com/v2/testRepo/blobs/uploads/");
        client.Setup(client => client.SendAsync(It.Is<HttpRequestMessage>(m => m.RequestUri == new Uri("https://my-registy.com/v2/testRepo/blobs/uploads/")), It.IsAny<CancellationToken>())).Returns(Task.FromResult(httpResponse));

        HttpClient finalClient = client.Object;
        DefaultBlobUploadOperations operations = new(new Uri("https://my-registy.com"), finalClient, logger);
        StartUploadInformation result = await operations.StartAsync(repoName, CancellationToken.None);

        Assert.Equal("https://my-registy.com/v2/testRepo/blobs/uploads/", result.UploadUri.AbsoluteUri);
    }

    [Fact]
    public async Task CanParseRegistryDeclaredChunkSize_FromOCIChunkMinLength()
    {
        ILogger logger = _loggerFactory.CreateLogger(nameof(CanParseRegistryDeclaredChunkSize_FromOCIChunkMinLength));
        string repoName = "testRepo";

        Mock<HttpClient> client = new(MockBehavior.Loose);
        HttpResponseMessage httpResponse = new()
        {
            StatusCode = HttpStatusCode.Accepted,
        };
        httpResponse.Headers.Add("OCI-Chunk-Min-Length", "100000");
        httpResponse.Headers.Location = new Uri("https://my-registy.com/v2/testRepo/blobs/uploads/");
        client.Setup(client => client.SendAsync(It.Is<HttpRequestMessage>(m => m.RequestUri == new Uri("https://my-registy.com/v2/testRepo/blobs/uploads/")), It.IsAny<CancellationToken>())).Returns(Task.FromResult(httpResponse));

        HttpClient finalClient = client.Object;
        DefaultBlobUploadOperations operations = new(new Uri("https://my-registy.com"), finalClient, logger);
        StartUploadInformation result = await operations.StartAsync(repoName, CancellationToken.None);

        Assert.Equal("https://my-registy.com/v2/testRepo/blobs/uploads/", result.UploadUri.AbsoluteUri);
    }

    [Fact]
    public async Task CanParseRegistryDeclaredChunkSize_None()
    {
        ILogger logger = _loggerFactory.CreateLogger(nameof(CanParseRegistryDeclaredChunkSize_None));
        string repoName = "testRepo";

        Mock<HttpClient> client = new(MockBehavior.Loose);
        HttpResponseMessage httpResponse = new()
        {
            StatusCode = HttpStatusCode.Accepted,
        };
        httpResponse.Headers.Location = new Uri("https://my-registy.com/v2/testRepo/blobs/uploads/");
        client.Setup(client => client.SendAsync(It.Is<HttpRequestMessage>(m => m.RequestUri == new Uri("https://my-registy.com/v2/testRepo/blobs/uploads/")), It.IsAny<CancellationToken>())).Returns(Task.FromResult(httpResponse));

        HttpClient finalClient = client.Object;
        DefaultBlobUploadOperations operations = new(new Uri("https://my-registy.com"), finalClient, logger);
        StartUploadInformation result = await operations.StartAsync(repoName, CancellationToken.None);

        Assert.Equal("https://my-registy.com/v2/testRepo/blobs/uploads/", result.UploadUri.AbsoluteUri);
    }

    [Fact]
    public async Task UploadBlobChunkedAsync_NormalFlow()
    {
        ILogger logger = _loggerFactory.CreateLogger(nameof(UploadBlobChunkedAsync_NormalFlow));
        Uri registryUri = ContainerHelpers.TryExpandRegistryToUri("public.ecr.aws");

        int contentLength = 50000000;
        int chunkSize = 10000000;

        Stream testStream = new MemoryStream(new byte[contentLength]);

        Uri uploadPath = new("/uploads/foo/12345", UriKind.Relative);
        Uri absoluteUploadUri = new(registryUri, uploadPath);
        Mock<IRegistryAPI> api = new(MockBehavior.Loose);
        int uploadedCount = 0;
        api.Setup(api => api.Blob.Upload.UploadChunkAsync(It.IsIn(absoluteUploadUri, uploadPath), It.IsAny<HttpContent>(), It.IsAny<CancellationToken>())).Returns(() => {
            uploadedCount += chunkSize;
            return Task.FromResult(ChunkUploadSuccessful(absoluteUploadUri, uploadPath, uploadedCount));
        });

        RegistrySettings settings = new()
        {
            ParallelUploadEnabled = false,
            ForceChunkedUpload = false,
            ChunkedUploadSizeBytes = chunkSize,
        };

        Registry registry = new(registryUri, logger, api.Object, settings);
        await registry.UploadBlobChunkedAsync(testStream, new StartUploadInformation(absoluteUploadUri), CancellationToken.None);

        api.Verify(api => api.Blob.Upload.UploadChunkAsync(It.IsIn(absoluteUploadUri, uploadPath), It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()), Times.Exactly(5));
    }

    [Fact]
    public async Task UploadBlobChunkedAsync_Failure()
    {
        ILogger logger = _loggerFactory.CreateLogger(nameof(UploadBlobChunkedAsync_NormalFlow));
        Uri registryUri = ContainerHelpers.TryExpandRegistryToUri("public.ecr.aws");

        int contentLength = 50000000;
        int chunkSize = 10000000;

        Stream testStream = new MemoryStream(new byte[contentLength]);

        Uri uploadPath = new("/uploads/foo/12345", UriKind.Relative);
        Uri absoluteUploadUri = new(registryUri, uploadPath);
        Mock<IRegistryAPI> api = new(MockBehavior.Loose);

        Exception preparedException = new ApplicationException(Resource.FormatString(nameof(Strings.BlobUploadFailed), $"PATCH <uri>", HttpStatusCode.InternalServerError));

        api.Setup(api => api.Blob.Upload.UploadChunkAsync(It.IsIn(absoluteUploadUri, uploadPath), It.IsAny<HttpContent>(), It.IsAny<CancellationToken>())).Returns(() => {
            throw preparedException;
        });

        RegistrySettings settings = new()
        {
            ParallelUploadEnabled = false,
            ForceChunkedUpload = false,
            ChunkedUploadSizeBytes = chunkSize,
        };

        Registry registry = new(registryUri, logger, api.Object, settings);
        ApplicationException receivedException = await Assert.ThrowsAsync<ApplicationException>(() => registry.UploadBlobChunkedAsync(testStream, new StartUploadInformation(absoluteUploadUri), CancellationToken.None));

        Assert.Equal(preparedException, receivedException);

        api.Verify(api => api.Blob.Upload.UploadChunkAsync(It.IsIn(absoluteUploadUri, uploadPath), It.IsAny<HttpContent>(), It.IsAny<CancellationToken>()), Times.Exactly(1));
    }



    private static NextChunkUploadInformation ChunkUploadSuccessful(Uri requestUri, Uri uploadUrl, int? contentLength, HttpStatusCode code = HttpStatusCode.Accepted)
    {
        return new(uploadUrl);
    }

}
