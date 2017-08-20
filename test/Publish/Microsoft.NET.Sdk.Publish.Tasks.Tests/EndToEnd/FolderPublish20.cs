using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.NET.Sdk.Publish.Tasks.Tests.EndToEnd
{
    public class FolderPublish20
    {
        public string BaseTestDirectory
        {
            get
            {
                return Path.Combine(AppContext.BaseDirectory, nameof(FolderPublish20));
            }
        }

        public const string DotNetExeName = "dotnet";
        public const string DotNetInstallArgs = "new -i Microsoft.dotnet.web.projecttemplates.1.x::1.0.0-*";
        public const string DotNetNewAdditionalArgs = "";

        [Theory]
        [InlineData("netcoreapp2.0", "Release", "core")]
        [InlineData("netcoreapp2.0", "Debug", "core")]
        public async Task EmptyWebCore(string templateFramework, string configuration, string msBuildType)
        {
            string projectName = $"{nameof(EmptyWebCore)}_{Path.GetRandomFileName()}";

            // Arrange
            string dotNetNewArguments = $"new web --framework {templateFramework} {DotNetNewAdditionalArgs}";
            string testFolder = Path.Combine(BaseTestDirectory, projectName);

            // dotnet new
            int? exitCode = new ProcessWrapper().RunProcess(DotNetExeName, dotNetNewArguments, testFolder, out int? processId1, createDirectoryIfNotExists: true);
            Assert.True(exitCode.HasValue && exitCode.Value == 0);

            string resultText = await RestoreBuildPublishAndRun(testFolder, projectName, configuration, msBuildType);

            Assert.Equal($"Hello World!", resultText);
        }


        [Theory]
        [InlineData("netcoreapp2.0", "Release", "core")]
        [InlineData("netcoreapp2.0", "Debug", "core")]
        public async Task WebAPICore(string templateFramework, string configuration, string msBuildType)
        {
            string projectName = $"{nameof(WebAPICore)}_{Path.GetRandomFileName()}";

            // Arrange
            string dotNetNewArguments = $"new webapi --framework {templateFramework} {DotNetNewAdditionalArgs}";
            string testFolder = Path.Combine(BaseTestDirectory, projectName);
            // dotnet new
            int? exitCode = new ProcessWrapper().RunProcess(DotNetExeName, dotNetNewArguments, testFolder, out int? processId1, createDirectoryIfNotExists: true);
            Assert.True(exitCode.HasValue && exitCode.Value == 0);

            string resultText = await RestoreBuildPublishAndRun(testFolder, projectName, configuration, msBuildType, isStandAlone:false, resultUrl:"http://localhost:5000/api/Values");

            Assert.Equal(resultText, "[\"value1\",\"value2\"]");
        }

        [Theory]
        [InlineData("netcoreapp2.0", "Release", "core", "none", "false")]
        [InlineData("netcoreapp2.0", "Debug", "core", "none", "false")]
        [InlineData("netcoreapp2.0", "Release", "core", "Individual", "false")]
        [InlineData("netcoreapp2.0", "Debug", "core", "Individual", "false")]
        [InlineData("netcoreapp2.0", "Release", "core", "Individual", "true")]
        [InlineData("netcoreapp2.0", "Debug", "core", "Individual", "true")]
        public async Task MvcCore(string templateFramework, string configuration, string msBuildType, string auth, string useLocalDB)
        {
            string projectName = $"{nameof(MvcCore)}_{Path.GetRandomFileName()}";

            string additionalOptions = string.Empty;
            // Arrange
            if (bool.TryParse(useLocalDB, out bool localDBBool) && localDBBool)
            {
                additionalOptions = $"--use-local-db";
            }
            string dotNetNewArguments = $"new mvc --framework {templateFramework} --auth {auth} {DotNetNewAdditionalArgs} {additionalOptions}";
            string testFolder = Path.Combine(BaseTestDirectory, projectName);

            // dotnet new
            int? exitCode = new ProcessWrapper().RunProcess(DotNetExeName, dotNetNewArguments, testFolder, out int? processId1, createDirectoryIfNotExists: true);
            Assert.True(exitCode.HasValue && exitCode.Value == 0);

            string resultText = await RestoreBuildPublishAndRun(testFolder, projectName, configuration, msBuildType);

            Assert.Contains($"<title>Home Page -", resultText);
        }

        [Theory]
        [InlineData("netcoreapp2.0", "Release", "core", "none", "false")]
        [InlineData("netcoreapp2.0", "Debug", "core", "none", "false")]
        [InlineData("netcoreapp2.0", "Release", "core", "Individual", "false")]
        [InlineData("netcoreapp2.0", "Debug", "core", "Individual", "false")]
        [InlineData("netcoreapp2.0", "Release", "core", "Individual", "true")]
        [InlineData("netcoreapp2.0", "Debug", "core", "Individual", "true")]
        public async Task RazorCore(string templateFramework, string configuration, string msBuildType, string auth, string useLocalDB)
        {
            string projectName = $"{nameof(RazorCore)}_{Path.GetRandomFileName()}";

            string additionalOptions = string.Empty;
            // Arrange
            if (bool.TryParse(useLocalDB, out bool localDBBool) && localDBBool)
            {
                additionalOptions = $"--use-local-db";
            }
            string dotNetNewArguments = $"new razor --framework {templateFramework} --auth {auth} {DotNetNewAdditionalArgs} {additionalOptions}";
            string testFolder = Path.Combine(BaseTestDirectory, projectName);

            // dotnet new
            int? exitCode = new ProcessWrapper().RunProcess(DotNetExeName, dotNetNewArguments, testFolder, out int? processId1, createDirectoryIfNotExists: true);
            Assert.True(exitCode.HasValue && exitCode.Value == 0);

            string resultText = await RestoreBuildPublishAndRun(testFolder, projectName, configuration, msBuildType);

            Assert.Contains($"<title>Home page -", resultText);
        }

        [Theory]
        [InlineData("netcoreapp2.0", "Release", "core", "net461")]
        [InlineData("netcoreapp2.0", "Debug", "core", "net461")]

        //[InlineData("netcoreapp2.0", "Release", "core", "net462")]
        //[InlineData("netcoreapp2.0", "Debug", "core", "net462")]
        public async Task EmptyWebNET(string templateFramework, string configuration, string msBuildType, string targetFramework)
        {
            string projectName = $"{nameof(EmptyWebNET)}_{Path.GetRandomFileName()}";

            // Arrange
            string dotNetNewArguments = $"new web --framework {templateFramework} --target-framework-override {targetFramework} {DotNetNewAdditionalArgs}";
            string testFolder = Path.Combine(BaseTestDirectory, projectName);

            // dotnet new
            int? exitCode = new ProcessWrapper().RunProcess(DotNetExeName, dotNetNewArguments, testFolder, out int? processId1, createDirectoryIfNotExists: true);
            Assert.True(exitCode.HasValue && exitCode.Value == 0);

            string resultText = await RestoreBuildPublishAndRun(testFolder, projectName, configuration, msBuildType, isStandAlone:true);

            Assert.Equal($"Hello World!", resultText);
        }

        [Theory]
        [InlineData("netcoreapp2.0", "Release", "core", "net461")]
        [InlineData("netcoreapp2.0", "Debug", "core", "net461")]

        //[InlineData("netcoreapp2.0", "Release", "core", "net462")]
        //[InlineData("netcoreapp2.0", "Debug", "core", "net462")]
        public async Task WebAPINET(string templateFramework, string configuration, string msBuildType, string targetFramework)
        {
            string projectName = $"{nameof(WebAPINET)}_{Path.GetRandomFileName()}";

            // Arrange
            string dotNetNewArguments = $"new webapi --framework {templateFramework} --target-framework-override {targetFramework} {DotNetNewAdditionalArgs}";
            string testFolder = Path.Combine(BaseTestDirectory, projectName);
            // dotnet new
            int? exitCode = new ProcessWrapper().RunProcess(DotNetExeName, dotNetNewArguments, testFolder, out int? processId1, createDirectoryIfNotExists: true);
            Assert.True(exitCode.HasValue && exitCode.Value == 0);

            string resultText = await RestoreBuildPublishAndRun(testFolder, projectName, configuration, msBuildType, isStandAlone:true, resultUrl: "http://localhost:5000/api/Values");

            Assert.Equal(resultText, "[\"value1\",\"value2\"]");
        }

        [Theory]
        [InlineData("netcoreapp2.0", "Release", "core", "none", "false", "net461")]
        [InlineData("netcoreapp2.0", "Debug", "core", "none", "false", "net461")]
        [InlineData("netcoreapp2.0", "Release", "core", "Individual", "false", "net461")]
        [InlineData("netcoreapp2.0", "Debug", "core", "Individual", "false", "net461")]
        [InlineData("netcoreapp2.0", "Release", "core", "Individual", "true", "net461")]
        [InlineData("netcoreapp2.0", "Debug", "core", "Individual", "true", "net461")]

        //[InlineData("netcoreapp2.0", "Release", "core", "none", "false", "net462")]
        //[InlineData("netcoreapp2.0", "Debug", "core", "none", "false", "net462")]
        //[InlineData("netcoreapp2.0", "Release", "core", "Individual", "false", "net462")]
        //[InlineData("netcoreapp2.0", "Debug", "core", "Individual", "false", "net462")]
        //[InlineData("netcoreapp2.0", "Release", "core", "Individual", "true", "net462")]
        //[InlineData("netcoreapp2.0", "Debug", "core", "Individual", "true", "net462")]
        public async Task MvcNET(string templateFramework, string configuration, string msBuildType, string auth, string useLocalDB, string targetFramework)
        {
            string projectName = $"{nameof(MvcNET)}_{Path.GetRandomFileName()}";

            string additionalOptions = string.Empty;
            // Arrange
            if (bool.TryParse(useLocalDB, out bool localDBBool) && localDBBool)
            {
                additionalOptions = $"--use-local-db";
            }
            string dotNetNewArguments = $"new mvc --framework {templateFramework} --target-framework-override {targetFramework} --auth {auth} {DotNetNewAdditionalArgs} {additionalOptions}";
            string testFolder = Path.Combine(BaseTestDirectory, projectName);

            // dotnet new
            int? exitCode = new ProcessWrapper().RunProcess(DotNetExeName, dotNetNewArguments, testFolder, out int? processId1, createDirectoryIfNotExists: true);
            Assert.True(exitCode.HasValue && exitCode.Value == 0);

            string resultText = await RestoreBuildPublishAndRun(testFolder, projectName, configuration, msBuildType, isStandAlone:true);

            Assert.Contains($"<title>Home Page -", resultText);
        }

        [Theory]
        [InlineData("netcoreapp2.0", "Release", "core", "none", "false", "net461")]
        [InlineData("netcoreapp2.0", "Debug", "core", "none", "false", "net461")]
        [InlineData("netcoreapp2.0", "Release", "core", "Individual", "false", "net461")]
        [InlineData("netcoreapp2.0", "Debug", "core", "Individual", "false", "net461")]
        [InlineData("netcoreapp2.0", "Release", "core", "Individual", "true", "net461")]
        [InlineData("netcoreapp2.0", "Debug", "core", "Individual", "true", "net461")]

        //[InlineData("netcoreapp2.0", "Release", "core", "none", "false", "net462")]
        //[InlineData("netcoreapp2.0", "Debug", "core", "none", "false", "net462")]
        //[InlineData("netcoreapp2.0", "Release", "core", "Individual", "false", "net462")]
        //[InlineData("netcoreapp2.0", "Debug", "core", "Individual", "false", "net462")]
        //[InlineData("netcoreapp2.0", "Release", "core", "Individual", "true", "net462")]
        //[InlineData("netcoreapp2.0", "Debug", "core", "Individual", "true", "net462")]
        public async Task RazorNET(string templateFramework, string configuration, string msBuildType, string auth, string useLocalDB, string targetFramework)
        {
            string projectName = $"{nameof(RazorNET)}_{Path.GetRandomFileName()}";

            string additionalOptions = string.Empty;
            // Arrange
            if (bool.TryParse(useLocalDB, out bool localDBBool) && localDBBool)
            {
                additionalOptions = $"--use-local-db";
            }
            string dotNetNewArguments = $"new razor --framework {templateFramework} --target-framework-override {targetFramework} --auth {auth} {DotNetNewAdditionalArgs} {additionalOptions}";
            string testFolder = Path.Combine(BaseTestDirectory, projectName);

            // dotnet new
            int? exitCode = new ProcessWrapper().RunProcess(DotNetExeName, dotNetNewArguments, testFolder, out int? processId1, createDirectoryIfNotExists: true);
            Assert.True(exitCode.HasValue && exitCode.Value == 0);

            string resultText = await RestoreBuildPublishAndRun(testFolder, projectName, configuration, msBuildType, isStandAlone: true);

            Assert.Contains($"<title>Home page -", resultText);
        }

        private async Task<string> RestoreBuildPublishAndRun(string testFolder, string projectName, string configuration, string msBuildType, bool isStandAlone = false, string resultUrl = "http://localhost:5000")
        {
            int? runningProcess = null;
            HttpResponseMessage result = null;
            try
            {
                int? exitCode = 0;

                // dotnet restore
                string dotnetRestoreArguments = "restore";
                exitCode = new ProcessWrapper().RunProcess(DotNetExeName, dotnetRestoreArguments, testFolder, out int? processId2);
                Assert.True(exitCode.HasValue && exitCode.Value == 0);

                // dotnet build
                string dotnetBuildArguments = "build";
                exitCode = new ProcessWrapper().RunProcess(DotNetExeName, dotnetBuildArguments, testFolder, out int? processId3);
                Assert.True(exitCode.HasValue && exitCode.Value == 0);

                // msbuild publish
                string fileName = "msbuild";
                string publishOutputFolder = $"bin\\{configuration}\\PublishOutput";
                string dotnetPublishArguments = $"{projectName}.csproj /p:DeployOnBuild=true /p:Configuration={configuration} /p:PublishUrl={publishOutputFolder}";
                if (string.Equals(msBuildType, "core"))
                {
                    dotnetPublishArguments = $"{fileName} {dotnetPublishArguments}";
                    fileName = DotNetExeName;
                }
                exitCode = new ProcessWrapper().RunProcess(fileName, dotnetPublishArguments, testFolder, out int? processId4);
                Assert.True(exitCode.HasValue && exitCode.Value == 0);

                string publishOutputFolderFullPath = Path.Combine(testFolder, publishOutputFolder);
                string dotNetRunArguments = $"{projectName}.dll";
                fileName = DotNetExeName;
                if (isStandAlone)
                {
                    dotNetRunArguments = null;
                    fileName = Path.Combine(publishOutputFolderFullPath, $"{projectName}.exe");
                }
                exitCode = new ProcessWrapper().RunProcess(fileName, dotNetRunArguments, publishOutputFolderFullPath, out runningProcess, waitForExit: false);

                // Wait for 2 seconds for the application to start
                await Task.Delay(TimeSpan.FromSeconds(2));

                CancellationTokenSource tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
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
            }
            finally
            {
                if (runningProcess != null)
                {
                    ProcessWrapper.KillProcessTree(runningProcess.Value);
                }
            }


            try
            {
                Directory.Delete(testFolder, true);
            }
            catch { }

            Assert.True(result != null);
            string resultText = await result.Content.ReadAsStringAsync();
            return resultText;
        }
    }
}
