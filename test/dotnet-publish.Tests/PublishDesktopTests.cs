using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
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
        [InlineData("KestrelDesktopWithRuntimes", "http://localhost:20201", null, "libuv.dll", true)]
        [InlineData("KestrelDesktopWithRuntimes", "http://localhost:20202", "win7-x64", "libuv.dll", true)]
        [InlineData("KestrelDesktop", "http://localhost:20204", null, "libuv.dll", true)]
        [InlineData("KestrelDesktop", "http://localhost:20205", "win7-x64", "libuv.dll", true)]
        public async Task DesktopApp_WithKestrel_WorksWhenPublished(string project, string url, string runtime, string libuvName, bool runnable)
        {
            var testInstance = GetTestInstance()
                .WithLockFiles();

            var publishCommand = new PublishCommand(Path.Combine(testInstance.TestRoot, project), runtime: runtime);
            var result = await publishCommand.ExecuteAsync();

            result.Should().Pass();

            // Test the output
            var outputDir = publishCommand.GetOutputDirectory(portable: false);
            outputDir.Should().HaveFile(libuvName);
            outputDir.Should().HaveFile(publishCommand.GetOutputExecutable());

            Task exec = null;
            if (runnable)
            {
                var command = new TestCommand(Path.Combine(outputDir.FullName, publishCommand.GetOutputExecutable()));
                try
                {
                    exec = command.ExecuteAsync(url);
                    NetworkHelper.IsServerUp(url).Should().BeTrue($"Unable to connect to kestrel server - {project} @ {url}");
                    NetworkHelper.TestGetRequest(url, url);
                }
                finally
                {
                    command.KillTree();
                }
                if (exec != null)
                {
                    await exec;
                }
            }
        }

        [WindowsOnlyTheory]
        [InlineData("KestrelDesktop", "http://localhost:20207")]
        [InlineData("KestrelDesktopWithRuntimes", "http://localhost:20208")]
        public async Task DesktopApp_WithKestrel_WorksWhenRun(string project, string url)
        {
            var testInstance = GetTestInstance()
                .WithLockFiles()
                .WithBuildArtifacts();

            Task exec = null;
            var command = new RunCommand(Path.Combine(testInstance.TestRoot, project));
            try
            {
                exec = command.ExecuteAsync(url);
                NetworkHelper.IsServerUp(url).Should().BeTrue($"Unable to connect to kestrel server - {project} @ {url}");
                NetworkHelper.TestGetRequest(url, url);
            }
            finally
            {
                command.KillTree();
            }
            if (exec != null)
            {
                await exec;
            }
        }

        private static TestInstance GetTestInstance([CallerMemberName] string callingMethod = "")
        {
            return TestAssetsManager.CreateTestInstance(Path.Combine("..", "DesktopTestProjects", "DesktopKestrelSample"), callingMethod);
        }
    }
}
