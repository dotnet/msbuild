using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

namespace Microsoft.DotNet.Tools.Publish.Tests
{
    public class PublishDesktopTests : TestBase
    {
        [WindowsOnlyTheory]
        [InlineData("KestrelDesktopWithRuntimes", "http://localhost:20201", null)]
        [InlineData("KestrelDesktopWithRuntimes", "http://localhost:20201", "win7-x64")]
        [InlineData("KestrelDesktop", "http://localhost:20203", null)]
        [InlineData("KestrelDesktop", "http://localhost:20204", "win7-x64")]
        public async Task DesktopApp_WithKestrel_WorksWhenPublishedWithRID(string project, string url, string runtime)
        {
            var testInstance = GetTestInstance(project)
                .WithLockFiles()
                .WithBuildArtifacts();

            var publishCommand = new PublishCommand(testInstance.TestRoot, runtime);
            var result = await publishCommand.ExecuteAsync();

            result.Should().Pass();

            // Test the output
            var outputDir = publishCommand.GetOutputDirectory(portable: false);
            outputDir.Should().HaveFile("libuv.dll");
            outputDir.Should().HaveFile(publishCommand.GetOutputExecutable());

            var command = new TestCommand(Path.Combine(outputDir.FullName, publishCommand.GetOutputExecutable()));
            try
            {
                command.Execute(url);
                NetworkHelper.IsServerUp(url).Should().BeTrue($"Unable to connect to kestrel server - {project} @ {url}");
                NetworkHelper.TestGetRequest(url, url);
            }
            finally
            {
                command.KillTree();
            }
        }

        private static TestInstance GetTestInstance(string name)
        {
            return TestAssetsManager.CreateTestInstance(Path.Combine("..", "DesktopTestProjects", "DesktopKestrelSample", name));
        }
    }
}
