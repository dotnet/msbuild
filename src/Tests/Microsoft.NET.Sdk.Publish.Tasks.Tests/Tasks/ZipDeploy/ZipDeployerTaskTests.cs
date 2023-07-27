// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Net.Http;
using Moq;

namespace Microsoft.NET.Sdk.Publish.Tasks.ZipDeploy.Tests
{
    public class ZipDeployerTaskTests
    {
        private static string _testZippedPublishContentsPath;
        private static string TestAssemblyToTestZipPath = Path.Combine("Resources", "TestPublishContents.zip");
        private static string UserAgentName = "websdk";
        private static string UserAgentVersion = "1.0";

        public static string TestZippedPublishContentsPath
        {
            get
            {
                if (_testZippedPublishContentsPath == null)
                {
                    string codebase = typeof(ZipDeployerTaskTests).Assembly.Location;
                    string assemblyPath = new Uri(codebase, UriKind.Absolute).LocalPath;
                    string baseDirectory = Path.GetDirectoryName(assemblyPath);
                    _testZippedPublishContentsPath = Path.Combine(baseDirectory, TestAssemblyToTestZipPath);
                }

                return _testZippedPublishContentsPath;
            }
        }

        [Fact]
        public async Task ExecuteZipDeploy_InvalidZipFilePath()
        {
            Mock<IHttpClient> client = new Mock<IHttpClient>();
            ZipDeploy zipDeployer = new ZipDeploy();

            bool result = await zipDeployer.ZipDeployAsync(string.Empty, "username", "password", "publishUrl", null, "Foo", client.Object, false);

            client.Verify(c => c.PostAsync(It.IsAny<Uri>(), It.IsAny<StreamContent>()), Times.Never);
            Assert.False(result);
        }

        /// <summary>
        /// ZipDeploy should use PublishUrl if not null or empty, else use SiteName.
        /// </summary>
        [Theory]
        [InlineData("https://sitename.scm.azurewebsites.net", null, "https://sitename.scm.azurewebsites.net/api/zipdeploy?isAsync=true")]
        [InlineData("https://sitename.scm.azurewebsites.net", "", "https://sitename.scm.azurewebsites.net/api/zipdeploy?isAsync=true")]
        [InlineData("https://sitename.scm.azurewebsites.net", "shouldNotBeUsed", "https://sitename.scm.azurewebsites.net/api/zipdeploy?isAsync=true")]
        [InlineData(null, "sitename", "https://sitename.scm.azurewebsites.net/api/zipdeploy?isAsync=true")]
        [InlineData("", "sitename", "https://sitename.scm.azurewebsites.net/api/zipdeploy?isAsync=true")]
        public async Task ExecuteZipDeploy_PublishUrlOrSiteNameGiven(string publishUrl, string siteName, string expectedZipDeployEndpoint)
        {
            Action<Mock<IHttpClient>, bool> verifyStep = (client, result) =>
            {
                client.Verify(c => c.PostAsync(
                It.Is<Uri>(uri => string.Equals(uri.AbsoluteUri, expectedZipDeployEndpoint, StringComparison.Ordinal)),
                It.Is<StreamContent>(streamContent => IsStreamContentEqualToFileContent(streamContent, TestZippedPublishContentsPath))),
                Times.Once);
                Assert.Equal($"{UserAgentName}/{UserAgentVersion}", client.Object.DefaultRequestHeaders.GetValues("User-Agent").FirstOrDefault());
                Assert.True(result);
            };

            await RunZipDeployAsyncTest(publishUrl, siteName, UserAgentVersion, HttpStatusCode.OK, verifyStep);
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData("", "")]
        [InlineData("", null)]
        [InlineData(null, "")]
        public async Task ExecuteZipDeploy_NeitherPublishUrlNorSiteNameGiven(string publishUrl, string siteName)
        {
            Action<Mock<IHttpClient>, bool> verifyStep = (client, result) =>
            {
                client.Verify(c => c.PostAsync(
                It.IsAny<Uri>(),
                It.IsAny<StreamContent>()),
                Times.Never);
                Assert.False(client.Object.DefaultRequestHeaders.TryGetValues("User-Agent", out _));
                Assert.False(result);
            };

            await RunZipDeployAsyncTest(publishUrl, siteName, UserAgentVersion, HttpStatusCode.OK, verifyStep);
        }

        [Theory]
        [InlineData(HttpStatusCode.OK, true)]
        [InlineData(HttpStatusCode.Accepted, true)]
        [InlineData(HttpStatusCode.Forbidden, false)]
        [InlineData(HttpStatusCode.NotFound, false)]
        [InlineData(HttpStatusCode.RequestTimeout, false)]
        [InlineData(HttpStatusCode.InternalServerError, false)]
        public async Task ExecuteZipDeploy_VaryingHttpResponseStatuses(HttpStatusCode responseStatusCode, bool expectedResult)
        {
            Action<Mock<IHttpClient>, bool> verifyStep = (client, result) =>
            {
                client.Verify(c => c.PostAsync(
                It.Is<Uri>(uri => string.Equals(uri.AbsoluteUri, "https://sitename.scm.azurewebsites.net/api/zipdeploy?isAsync=true", StringComparison.Ordinal)),
                It.Is<StreamContent>(streamContent => IsStreamContentEqualToFileContent(streamContent, TestZippedPublishContentsPath))),
                Times.Once);
                Assert.Equal($"{UserAgentName}/{UserAgentVersion}", client.Object.DefaultRequestHeaders.GetValues("User-Agent").FirstOrDefault());
                Assert.Equal(expectedResult, result);
            };

            await RunZipDeployAsyncTest("https://sitename.scm.azurewebsites.net", null, UserAgentVersion, responseStatusCode, verifyStep);
        }

        private async Task RunZipDeployAsyncTest(string publishUrl, string siteName, string userAgentVersion, HttpStatusCode responseStatusCode, Action<Mock<IHttpClient>, bool> verifyStep)
        {
            Mock<IHttpClient> client = new Mock<IHttpClient>();

            //constructing HttpRequestMessage to get HttpRequestHeaders as HttpRequestHeaders contains no public constructors
            HttpRequestMessage requestMessage = new HttpRequestMessage();
            client.Setup(x => x.DefaultRequestHeaders).Returns(requestMessage.Headers);
            client.Setup(c => c.PostAsync(It.IsAny<Uri>(), It.IsAny<StreamContent>())).Returns((Uri uri, StreamContent streamContent) =>
            {
                byte[] plainAuthBytes = Encoding.ASCII.GetBytes("username:password");
                string base64AuthParam = Convert.ToBase64String(plainAuthBytes);

                Assert.Equal(base64AuthParam, client.Object.DefaultRequestHeaders.Authorization.Parameter);
                Assert.Equal("Basic", client.Object.DefaultRequestHeaders.Authorization.Scheme);

                return Task.FromResult(new HttpResponseMessage(responseStatusCode));
            });

            Func<Uri, StreamContent, Task<HttpResponseMessage>> runPostAsync = (uri, streamContent) =>
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            };

            ZipDeploy zipDeployer = new ZipDeploy();

            bool result = await zipDeployer.ZipDeployAsync(TestZippedPublishContentsPath, "username", "password", publishUrl, siteName, userAgentVersion, client.Object, false);

            verifyStep(client, result);
        }

        private bool IsStreamContentEqualToFileContent(StreamContent streamContent, string filePath)
        {
            byte[] expectedZipByteArr = File.ReadAllBytes(filePath);
            Task<byte[]> t = streamContent.ReadAsByteArrayAsync();
            t.Wait();
            return expectedZipByteArr.SequenceEqual(t.Result);
        }
    }
}
