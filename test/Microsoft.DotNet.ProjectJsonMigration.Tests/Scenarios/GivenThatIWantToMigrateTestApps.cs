using Microsoft.Build.Construction;
using Microsoft.DotNet.ProjectJsonMigration;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.Tools.Test.Utilities;
using NuGet.Frameworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using System.IO;
using Microsoft.DotNet.Tools.Common;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Build3Command = Microsoft.DotNet.Tools.Test.Utilities.Build3Command;
using TemporaryDotnetNewTemplateProject = Microsoft.DotNet.Cli.TemporaryDotnetNewTemplateProject;

namespace Microsoft.DotNet.Migration.Tests
{
    public class GivenThatIWantToMigrateTestApps : TestBase
    {
        private class Failure
        {
            public string Phase {get; set;}
            public string Message {get; set;}
            public string ProjectJson {get; set;}
        }

        [Theory]
        // TODO: Standalone apps [InlineData("TestAppSimple", false)]
        // https://github.com/dotnet/sdk/issues/73 [InlineData("TestAppWithLibrary/TestApp", false)]
        [InlineData("TestAppWithRuntimeOptions", false)]
        public void It_migrates_a_project(string projectName, bool isLibrary)
        {
            var projectDirectory = TestAssetsManager.CreateTestInstance(projectName, callingMethod: "i").WithLockFiles().Path;

            BuildProjectJson(projectDirectory);
            var projectJsonBuildOutputs = new HashSet<string>(CollectBuildOutputs(projectDirectory));
            CleanBinObj(projectDirectory);

            MigrateProject(projectDirectory);
            Restore(projectDirectory);
            BuildMSBuild(projectDirectory);

            var msbuildBuildOutputs = new HashSet<string>(CollectBuildOutputs(projectDirectory));

            var outputsIdentical = projectJsonBuildOutputs.SetEquals(msbuildBuildOutputs);

            // diagnostics
            if (!outputsIdentical)
            {
                Console.WriteLine("Project.json Outputs:");
                Console.WriteLine(string.Join("\n", projectJsonBuildOutputs));

                Console.WriteLine("");

                Console.WriteLine("MSBuild Outputs:");
                Console.WriteLine(string.Join("\n", msbuildBuildOutputs));
            }

            outputsIdentical.Should().BeTrue();

            if (!isLibrary)
            {
                VerifyMSBuildOutputRunnable(projectDirectory);                
            }
        }

        [Theory]
        [InlineData("TestAppWithLibrary/TestLibrary")]
        [InlineData("TestLibraryWithAnalyzer")]
        [InlineData("TestLibraryWithConfiguration")]
        public void It_migrates_a_library(string projectName)
        {
            var projectDirectory =
                TestAssetsManager.CreateTestInstance(projectName, callingMethod: "i").WithLockFiles().Path;

            BuildProjectJson(projectDirectory);
            var projectJsonBuildOutputs = new HashSet<string>(CollectBuildOutputs(projectDirectory));
            CleanBinObj(projectDirectory);

            MigrateProject(projectDirectory);
            Restore(projectDirectory);
            BuildMSBuild(projectDirectory);

            var msbuildBuildOutputs = new HashSet<string>(CollectBuildOutputs(projectDirectory));

            var msBuildHasAdditionalOutputsButIncludesProjectJsonOutputs = projectJsonBuildOutputs.IsProperSubsetOf(msbuildBuildOutputs);

            // diagnostics
            if (!msBuildHasAdditionalOutputsButIncludesProjectJsonOutputs)
            {
                Console.WriteLine("Project.json Outputs:");
                Console.WriteLine(string.Join("\n", projectJsonBuildOutputs));

                Console.WriteLine("");

                Console.WriteLine("MSBuild Outputs:");
                Console.WriteLine(string.Join("\n", msbuildBuildOutputs));
            }
            
            msBuildHasAdditionalOutputsButIncludesProjectJsonOutputs.Should().BeTrue();
        }

        [Fact]
        public void It_migrates_an_app_with_scripts_and_the_scripts_run()
        {
            var projectDirectory =
                TestAssetsManager.CreateTestInstance("TestAppWithMigrateAbleScripts", callingMethod: "i").WithLockFiles().Path;

            BuildProjectJson(projectDirectory);
            var projectJsonBuildOutputs = new HashSet<string>(CollectBuildOutputs(projectDirectory));
            CleanBinObj(projectDirectory);

            MigrateProject(projectDirectory);
            Restore(projectDirectory);
            var msBuildStdOut = BuildMSBuild(projectDirectory);

            var msbuildBuildOutputs = new HashSet<string>(CollectBuildOutputs(projectDirectory));

            var outputsIdentical = projectJsonBuildOutputs.SetEquals(msbuildBuildOutputs);
            outputsIdentical.Should().BeTrue();
                VerifyMSBuildOutputRunnable(projectDirectory);

            var outputDir =
                PathUtility.EnsureTrailingSlash(Path.Combine(projectDirectory, "bin", "Debug", "netcoreapp1.0"));

            msBuildStdOut.Should().Contain($"precompile_output ?Debug? ?{outputDir}? ?.NETCoreApp=v1.0?");
            msBuildStdOut.Should().Contain($"postcompile_output ?Debug? ?{outputDir}? ?.NETCoreApp=v1.0?");
        }

        private string RunNetcoreappMSBuildOutput(string projectDirectory)
        {
            var dllFileName = Path.GetFileName(projectDirectory) + ".dll";

            var runnableDll = Path.Combine(projectDirectory, "bin","Debug", "netcoreapp1.0", dllFileName);
            var result = new TestCommand("dotnet").ExecuteWithCapturedOutput(runnableDll);
            result.Should().Pass();
            return result.StdOut;
        }

        private void VerifyMSBuildOutputRunnable(string projectDirectory)
        {
            var dllFileName = Path.GetFileName(projectDirectory) + ".dll";

            var runnableDlls = Directory.EnumerateFiles(Path.Combine(projectDirectory, "bin"), dllFileName,
                SearchOption.AllDirectories);

            foreach (var dll in runnableDlls)
            {
                new TestCommand("dotnet").ExecuteWithCapturedOutput(dll).Should().Pass();
            }
        }

        private IEnumerable<string> CollectBuildOutputs(string projectDirectory)
        {
            var fullBinPath = Path.GetFullPath(Path.Combine(projectDirectory, "bin"));

            return Directory.EnumerateFiles(fullBinPath, "*", SearchOption.AllDirectories)
                            .Select(p => Path.GetFullPath(p).Substring(fullBinPath.Length));
        }

        private void CleanBinObj(string projectDirectory)
        {
            var dirs = new string[] { Path.Combine(projectDirectory, "bin"), Path.Combine(projectDirectory, "obj") };

            foreach (var dir in dirs)
            {
                Directory.Delete(dir, true);
            }
        }

        private void BuildProjectJson(string projectDirectory)
        {
            var projectFile = Path.Combine(projectDirectory, "project.json");
            var result = new BuildCommand(projectPath: projectFile)
                .ExecuteWithCapturedOutput();

            result.Should().Pass();
        }

        private void MigrateProject(string projectDirectory)
        {
            var dotnetNew = new TemporaryDotnetNewTemplateProject();
            var sdkVersion = new ProjectJsonParser(dotnetNew.ProjectJson).SdkPackageVersion;
            var migrationSettings = new MigrationSettings(projectDirectory, projectDirectory, sdkVersion, dotnetNew.MSBuildProject);
            new ProjectMigrator().Migrate(migrationSettings);
        }

        private void Restore(string projectDirectory)
        {
            new TestCommand("dotnet")
                .WithWorkingDirectory(projectDirectory)
                .Execute("restore")
                .Should()
                .Pass();
        }

        private string BuildMSBuild(string projectDirectory)
        {
            var result = new Build3Command()
                .WithWorkingDirectory(projectDirectory)
                .ExecuteWithCapturedOutput();
            result
                .Should()
                .Pass();

            return result.StdOut;
        }
    }
}
