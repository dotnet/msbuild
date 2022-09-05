using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Moq;

using Xunit;

namespace Microsoft.NET.Sdk.Publish.Tasks.ZipDeploy.Tests
{
    public class ZipDeploymentStatusTests
    {
        private const string UserAgentName = "websdk";
        private const string UserAgentVersion = "1.0";
        private const string userName = "deploymentUser";
        private const string password = "[PLACEHOLDER]";


        [Theory]
        [InlineData(HttpStatusCode.Forbidden, DeployStatus.Unknown)]
        [InlineData(HttpStatusCode.NotFound, DeployStatus.Unknown)]
        [InlineData(HttpStatusCode.RequestTimeout, DeployStatus.Unknown)]
        [InlineData(HttpStatusCode.InternalServerError, DeployStatus.Unknown)]
        public async Task PollDeploymentStatusTest_ForErrorResponses(HttpStatusCode responseStatusCode, DeployStatus expectedDeployStatus)
        {
            // Arrange
            string deployUrl = "https://sitename.scm.azurewebsites.net/DeploymentStatus?Id=knownId";
            Action<Mock<IHttpClient>, bool> verifyStep = (client, result) =>
            {
                client.Verify(c => c.GetAsync(
                It.Is<Uri>(uri => string.Equals(uri.AbsoluteUri, deployUrl, StringComparison.Ordinal)), It.IsAny<CancellationToken>()));
                Assert.Equal($"{UserAgentName}/{UserAgentVersion}", client.Object.DefaultRequestHeaders.GetValues("User-Agent").FirstOrDefault());
                Assert.True(result);
            };

            Mock<IHttpClient> client = new Mock<IHttpClient>();
            HttpRequestMessage requestMessage = new HttpRequestMessage();
            client.Setup(x => x.DefaultRequestHeaders).Returns(requestMessage.Headers);
            client.Setup(c => c.GetAsync(new Uri(deployUrl, UriKind.RelativeOrAbsolute), It.IsAny<CancellationToken>())).Returns(() =>
            {
                return Task.FromResult(new HttpResponseMessage(responseStatusCode));
            });
            ZipDeploymentStatus deploymentStatus = new ZipDeploymentStatus(client.Object, $"{UserAgentName}/{UserAgentVersion}", null, false);

            // Act
            var actualdeployStatus = await deploymentStatus.PollDeploymentStatusAsync(deployUrl, userName, password);

            // Assert
            verifyStep(client, expectedDeployStatus == actualdeployStatus.Status);
        }

        [Theory]
        [InlineData(HttpStatusCode.OK, DeployStatus.Success)]
        [InlineData(HttpStatusCode.Accepted, DeployStatus.Success)]
        [InlineData(HttpStatusCode.OK, DeployStatus.Failed)]
        [InlineData(HttpStatusCode.Accepted, DeployStatus.Failed)]
        [InlineData(HttpStatusCode.OK, DeployStatus.Unknown)]
        [InlineData(HttpStatusCode.Accepted, DeployStatus.Unknown)]
        public async Task PollDeploymentStatusTest_ForValidResponses(HttpStatusCode responseStatusCode, DeployStatus expectedDeployStatus)
        {
            // Arrange
            string deployUrl = "https://sitename.scm.azurewebsites.net/DeploymentStatus?Id=knownId";
            Action<Mock<IHttpClient>, bool> verifyStep = (client, result) =>
            {
                client.Verify(c => c.GetAsync(
                It.Is<Uri>(uri => string.Equals(uri.AbsoluteUri, deployUrl, StringComparison.Ordinal)), It.IsAny<CancellationToken>()));
                Assert.Equal($"{UserAgentName}/{UserAgentVersion}", client.Object.DefaultRequestHeaders.GetValues("User-Agent").FirstOrDefault());
                Assert.True(result);
            };

            var deploymentResponse = new DeploymentResponse()
            {
                Id = "20a106ca-3797-4dbb",
                Status = expectedDeployStatus,
                LogUrl = "https://mywebapp.scm.azurewebsites.net/api/deployments/latest/log",
            };

            Mock<IHttpClient> client = new Mock<IHttpClient>();
            HttpRequestMessage requestMessage = new HttpRequestMessage();
            client.Setup(x => x.DefaultRequestHeaders).Returns(requestMessage.Headers);
            client.Setup(c => c.GetAsync(new Uri(deployUrl, UriKind.RelativeOrAbsolute), It.IsAny<CancellationToken>())).Returns(() =>
            {
                string statusJson = JsonSerializer.Serialize(deploymentResponse);

                HttpContent httpContent = new StringContent(statusJson, Encoding.UTF8, "application/json");
                HttpResponseMessage responseMessage = new HttpResponseMessage(responseStatusCode)
                {
                    Content = httpContent
                };
                return Task.FromResult(responseMessage);
            });
            ZipDeploymentStatus deploymentStatus = new ZipDeploymentStatus(client.Object, $"{UserAgentName}/{UserAgentVersion}", null, false);

            // Act
            var actualdeployStatus = await deploymentStatus.PollDeploymentStatusAsync(deployUrl, userName, password);

            // Assert
            verifyStep(client, expectedDeployStatus == actualdeployStatus.Status);
        }

        [Theory]
        [InlineData("https://mywebapp.scm.azurewebsites.net/api/deployments/latest/log", "id_1", "https://mywebapp.scm.azurewebsites.net/api/deployments/id_1/log")]
        [InlineData("https://mywebapp.scm.azurewebsites.net/api/deployments/latest/log", "", "https://mywebapp.scm.azurewebsites.net/api/deployments/latest/log")]
        [InlineData("https://mywebapp.scm.azurewebsites.net/api/deployments/latest/log", null, "https://mywebapp.scm.azurewebsites.net/api/deployments/latest/log")]
        [InlineData("https://mywebapp.scm.azurewebsites.net/api/deployments/latest", "id_2", "https://mywebapp.scm.azurewebsites.net/api/deployments/id_2")]
        [InlineData("https://mywebapp.scm.azurewebsites.net/api/deployments/latest/diagnostics/log", "11223344", "https://mywebapp.scm.azurewebsites.net/api/deployments/11223344/diagnostics/log")]
        [InlineData("https://latest.scm.azurewebsites.net/api/deployments/latest/log", "11223344", "https://latest.scm.azurewebsites.net/api/deployments/11223344/log")]
        [InlineData("https://latest.scm.azurewebsites.net/api/deployments/log", "11223344", "https://latest.scm.azurewebsites.net/api/deployments/log")]
        [InlineData("https://latest.scm.azurewebsites.net/api/latest/deployments/latest/log", "11223344", "https://latest.scm.azurewebsites.net/api/11223344/deployments/11223344/log")]
        [InlineData("", "id_2", "")]
        [InlineData(null, "id_2", null)]
        [InlineData("MyWebSiteNotAsUrl", "id_2", "MyWebSiteNotAsUrl")]
        [InlineData("MyWebSiteNotAsUrl", null, "MyWebSiteNotAsUrl")]
        [InlineData(null, null, null)]
        public void TestLogUrlId(string url, string id, string expectedUrl)
        {
            DeploymentResponse deploymentResponse = null;

            if (!string.IsNullOrEmpty(url)
                || !string.IsNullOrEmpty(id)
                || !string.IsNullOrEmpty(expectedUrl))
            {
                deploymentResponse = new()
                {
                    Id = id,
                    Status = DeployStatus.Success,
                    LogUrl = url,
                };
            }

            Assert.Equal(expectedUrl, deploymentResponse?.GetLogUrlWithId());
        }
    }
}
