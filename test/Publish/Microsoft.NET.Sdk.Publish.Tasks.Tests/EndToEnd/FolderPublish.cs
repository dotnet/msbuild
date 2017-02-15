using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.NET.Sdk.Publish.Tasks.Tests.EndToEnd
{
    public class FolderPublish
    {
        public string BaseTestDirectory
        {
            get
            {
                return Path.Combine(AppContext.BaseDirectory, nameof(FolderPublish));
            }
        }

        public const string DotNetExeName = "dotnet";
        public const string DotNetNewAdditionalArgs = "--debug:ephemeral-hive";
        [Theory]
        // For the full desktop scenarios, the tests run against the msbuild versions installed on the machines.
        //[InlineData("netcoreapp1.0", "Release", "full")]
        //[InlineData("netcoreapp1.1", "Release", "full")]
        //[InlineData("netcoreapp1.0", "Debug", "full")]
        //[InlineData("netcoreapp1.1", "Debug", "full")]
        // CLI Sdks are updated as part of setup.
        [InlineData("netcoreapp1.0", "Release", "core")]
        [InlineData("netcoreapp1.1", "Release", "core")]
        [InlineData("netcoreapp1.0", "Debug", "core")]
        [InlineData("netcoreapp1.1", "Debug", "core")]
        public async Task WebTemplate(string framework, string configuration, string msBuildType)
        {
            string projectName = $"{nameof(WebTemplate)}_{Path.GetRandomFileName()}";

            // Arrange
            string dotNetNewArguments = $"new web --framework {framework} {DotNetNewAdditionalArgs}";
            string testFolder = Path.Combine(BaseTestDirectory, projectName);

            // dotnet new
            int? exitCode = ProcessWrapper.RunProcess(DotNetExeName, dotNetNewArguments, testFolder, out int? processId1, createDirectoryIfNotExists: true);
            Assert.True(exitCode.HasValue && exitCode.Value == 0);

            string resultText = await RestoreBuildPublishAndRun(testFolder, projectName, configuration, msBuildType);

            Assert.Equal(resultText, "Hello World!");
        }

        [Theory]
        // For the full desktop scenarios, the tests run against the msbuild versions installed on the machines.
        //[InlineData("netcoreapp1.0", "Release", "full")]
        //[InlineData("netcoreapp1.1", "Release", "full")]
        //[InlineData("netcoreapp1.0", "Debug", "full")]
        //[InlineData("netcoreapp1.1", "Debug", "full")]
        // CLI Sdks are updated as part of setup.
        [InlineData("netcoreapp1.0", "Release", "core")]
        [InlineData("netcoreapp1.1", "Release", "core")]
        [InlineData("netcoreapp1.0", "Debug", "core")]
        [InlineData("netcoreapp1.1", "Debug", "core")]
        public async Task WebAPITemplate(string framework, string configuration, string msBuildType)
        {
            string projectName = $"{nameof(WebAPITemplate)}_{Path.GetRandomFileName()}";

            // Arrange
            string dotNetNewArguments = $"new webapi --framework {framework} {DotNetNewAdditionalArgs}";
            string testFolder = Path.Combine(BaseTestDirectory, projectName);

            // dotnet new
            int? exitCode = ProcessWrapper.RunProcess(DotNetExeName, dotNetNewArguments, testFolder, out int? processId1, createDirectoryIfNotExists: true);
            Assert.True(exitCode.HasValue && exitCode.Value == 0);

            string resultText = await RestoreBuildPublishAndRun(testFolder, projectName, configuration, msBuildType, "http://localhost:5000/api/Values");

            Assert.Equal(resultText, "[\"value1\",\"value2\"]");
        }

        [Theory]
        // CLI Sdks are updated as part of setup.
        [InlineData("netcoreapp1.0", "Release", "core", "none", "false")]
        [InlineData("netcoreapp1.1", "Release", "core", "none", "false")]
        [InlineData("netcoreapp1.0", "Debug", "core", "none", "false")]
        [InlineData("netcoreapp1.1", "Debug", "core", "none", "false")]
        [InlineData("netcoreapp1.0", "Release", "core", "Individual", "false")]
        [InlineData("netcoreapp1.1", "Release", "core", "Individual", "false")]
        [InlineData("netcoreapp1.0", "Debug", "core", "Individual", "false")]
        [InlineData("netcoreapp1.1", "Debug", "core", "Individual", "false")]
        [InlineData("netcoreapp1.0", "Release", "core", "Individual", "true")]
        [InlineData("netcoreapp1.1", "Release", "core", "Individual", "true")]
        [InlineData("netcoreapp1.0", "Debug", "core", "Individual", "true")]
        [InlineData("netcoreapp1.1", "Debug", "core", "Individual", "true")]
        public async Task MvcTemplate(string framework, string configuration, string msBuildType, string auth, string useLocalDB)
        {
            string projectName = $"{nameof(MvcTemplate)}_{Path.GetRandomFileName()}";

            string additionalOptions = string.Empty;
            // Arrange
            if (bool.TryParse(useLocalDB, out bool localDBBool) && localDBBool)
            {
                additionalOptions = $"--use-local-db";
            }
            string dotNetNewArguments = $"new mvc --framework {framework} --auth {auth} {DotNetNewAdditionalArgs} {additionalOptions}";
            string testFolder = Path.Combine(BaseTestDirectory, projectName);

            // dotnet new
            int? exitCode = ProcessWrapper.RunProcess(DotNetExeName, dotNetNewArguments, testFolder, out int? processId1, createDirectoryIfNotExists: true);
            Assert.True(exitCode.HasValue && exitCode.Value == 0);

            string resultText = await RestoreBuildPublishAndRun(testFolder, projectName, configuration, msBuildType);

            Assert.Contains($"<title>Home Page -", resultText);
        }

        private async Task<string> RestoreBuildPublishAndRun(string testFolder, string projectName, string configuration, string msBuildType, string resultUrl = "http://localhost:5000")
        {
            // dotnet restore
            string dotnetRestoreArguments = "restore";
            int? exitCode = ProcessWrapper.RunProcess(DotNetExeName, dotnetRestoreArguments, testFolder, out int? processId2);
            Assert.True(exitCode.HasValue && exitCode.Value == 0);

            // dotnet build
            string dotnetBuildArguments = "build";
            exitCode = ProcessWrapper.RunProcess(DotNetExeName, dotnetBuildArguments, testFolder, out int? processId3);
            Assert.True(exitCode.HasValue && exitCode.Value == 0);

            // msbuild publish
            string fileName = "msbuild";
            string dotnetPublishArguments = $"{projectName}.csproj /p:DeployOnBuild=true /p:Configuration={configuration} /p:PublishUrl=bin\\{configuration}\\PublishOutput";
            if (string.Equals(msBuildType, "core"))
            {
                dotnetPublishArguments = $"{fileName} {dotnetPublishArguments}";
                fileName = DotNetExeName;
            }
            exitCode = ProcessWrapper.RunProcess(fileName, dotnetPublishArguments, testFolder, out int? processId4);
            Assert.True(exitCode.HasValue && exitCode.Value == 0);

            int? runningProcess = null;
            string dotNetRunArguments = $"run {projectName}.dll";
            exitCode = ProcessWrapper.RunProcess(DotNetExeName, dotNetRunArguments, testFolder, out runningProcess, waitForExit: false);

            // Wait for 2 seconds for the application to start
            await Task.Delay(TimeSpan.FromSeconds(2));

            CancellationTokenSource tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            HttpResponseMessage result = null;
            while (!tokenSource.IsCancellationRequested)
            {
                try
                {
                    using (HttpClient client = new HttpClient())
                    {
                        result = await client.GetAsync(resultUrl);
                        if (result.StatusCode == System.Net.HttpStatusCode.OK)
                        {
                            break;
                        }
                        await Task.Delay(100);
                    }
                }
                catch
                {
                }
            }

            if (runningProcess != null)
            {
                ProcessWrapper.KillProcessTree(runningProcess.Value);
            }

            Assert.True(result != null);
            string resultText = await result.Content.ReadAsStringAsync();
            return resultText;
        }
    }
}
