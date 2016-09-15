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
using System.Runtime.InteropServices;
using Microsoft.DotNet.Tools.Common;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Tools.Migrate;
using Build3Command = Microsoft.DotNet.Tools.Test.Utilities.Build3Command;

namespace Microsoft.DotNet.Migration.Tests
{
    public class GivenThatIWantToMigrateTestApps : TestBase
    {
        [Theory]
        // TODO: Standalone apps [InlineData("TestAppSimple", false)]
        // https://github.com/dotnet/sdk/issues/73 [InlineData("TestAppWithLibrary/TestApp", false)]
        [InlineData("TestAppWithRuntimeOptions")]
        public void It_migrates_apps(string projectName)
        {
            var projectDirectory = TestAssetsManager.CreateTestInstance(projectName, callingMethod: "i").WithLockFiles().Path;
            var outputComparisonData = BuildProjectJsonMigrateBuildMSBuild(projectDirectory);

            var outputsIdentical =
                outputComparisonData.ProjectJsonBuildOutputs.SetEquals(outputComparisonData.MSBuildBuildOutputs);
            if (!outputsIdentical)
            {
                OutputDiagnostics(outputComparisonData);
            }
            outputsIdentical.Should().BeTrue();
            VerifyAllMSBuildOutputsRunnable(projectDirectory);
        }

        [Fact]
        public void It_migrates_dotnet_new_console_with_identical_outputs()
        {
            var projectDirectory = Temp.CreateDirectory().Path;
            var outputComparisonData = GetDotnetNewComparisonData(projectDirectory, "console");

            var outputsIdentical =
                outputComparisonData.ProjectJsonBuildOutputs.SetEquals(outputComparisonData.MSBuildBuildOutputs);
            if (!outputsIdentical)
            {
                OutputDiagnostics(outputComparisonData);
            }
            outputsIdentical.Should().BeTrue();
            VerifyAllMSBuildOutputsRunnable(projectDirectory);
        }

        [Fact]
        public void It_migrates_dotnet_new_web_with_outputs_containing_project_json_outputs()
        {
            var projectDirectory = Temp.CreateDirectory().Path;
            var outputComparisonData = GetDotnetNewComparisonData(projectDirectory, "web");

            var outputsIdentical =
                outputComparisonData.ProjectJsonBuildOutputs.SetEquals(outputComparisonData.MSBuildBuildOutputs);
            if (!outputsIdentical)
            {
                OutputDiagnostics(outputComparisonData);
            }
            outputsIdentical.Should().BeTrue();
        }

        [Theory]
        // TODO: Enable this when X-Targeting is in
        // [InlineData("TestLibraryWithMultipleFrameworks")]
        public void It_migrates_projects_with_multiple_TFMs()
        {
            var projectDirectory =
                TestAssetsManager.CreateTestInstance(projectName, callingMethod: "i").WithLockFiles().Path;
            var outputComparisonData = BuildProjectJsonMigrateBuildMSBuild(projectDirectory);

            var outputsIdentical =
                outputComparisonData.ProjectJsonBuildOutputs.SetEquals(outputComparisonData.MSBuildBuildOutputs);

            if (!outputsIdentical)
            {
                OutputDiagnostics(outputComparisonData);
            }

            outputsIdentical.Should().BeTrue();
        }

        [Theory]
        [InlineData("TestAppWithLibrary/TestLibrary")]
        [InlineData("TestLibraryWithAnalyzer")]
        [InlineData("TestLibraryWithConfiguration")]
        public void It_migrates_a_library(string projectName)
        {
            var projectDirectory =
                TestAssetsManager.CreateTestInstance(projectName, callingMethod: "i").WithLockFiles().Path;
            var outputComparisonData = BuildProjectJsonMigrateBuildMSBuild(projectDirectory);

            var outputsIdentical =
                outputComparisonData.ProjectJsonBuildOutputs.SetEquals(outputComparisonData.MSBuildBuildOutputs);

            if (!outputsIdentical)
            {
                OutputDiagnostics(outputComparisonData);
            }

            outputsIdentical.Should().BeTrue();
        }

        [Fact]
        public void It_migrates_an_app_with_scripts_and_the_scripts_run()
        {
            var projectDirectory =
                TestAssetsManager.CreateTestInstance("TestAppWithMigrateableScripts", callingMethod: "i").WithLockFiles().Path;

            BuildProjectJson(projectDirectory);
            var projectJsonBuildOutputs = new HashSet<string>(CollectBuildOutputs(projectDirectory));
            CleanBinObj(projectDirectory);

            MigrateProject(projectDirectory);
            Restore(projectDirectory);
            var msBuildStdOut = BuildMSBuild(projectDirectory);

            var msbuildBuildOutputs = new HashSet<string>(CollectBuildOutputs(projectDirectory));

            var outputsIdentical = projectJsonBuildOutputs.SetEquals(msbuildBuildOutputs);
            outputsIdentical.Should().BeTrue();
                VerifyAllMSBuildOutputsRunnable(projectDirectory);

            var outputDir =
                PathUtility.EnsureTrailingSlash(Path.Combine(projectDirectory, "bin", "Debug", "netcoreapp1.0"));

            msBuildStdOut.Should().Contain($"precompile_output ?Debug? ?{outputDir}? ?.NETCoreApp,Version=v1.0?");
            msBuildStdOut.Should().Contain($"postcompile_output ?Debug? ?{outputDir}? ?.NETCoreApp,Version=v1.0?");
        }

        private MigratedBuildComparisonData GetDotnetNewComparisonData(string projectDirectory, string dotnetNewType)
        {
            DotnetNew(projectDirectory, dotnetNewType);
            File.Copy("NuGet.tempaspnetpatch.config", Path.Combine(projectDirectory, "NuGet.Config"));
            Restore(projectDirectory);

            var outputComparisonData = BuildProjectJsonMigrateBuildMSBuild(projectDirectory);
            return outputComparisonData;
        }

        private void VerifyAllMSBuildOutputsRunnable(string projectDirectory)
        {
            var dllFileName = Path.GetFileName(projectDirectory) + ".dll";

            var runnableDlls = Directory.EnumerateFiles(Path.Combine(projectDirectory, "bin"), dllFileName,
                SearchOption.AllDirectories);

            foreach (var dll in runnableDlls)
            {
                new TestCommand("dotnet").ExecuteWithCapturedOutput(dll).Should().Pass();
            }
        }

        private MigratedBuildComparisonData BuildProjectJsonMigrateBuildMSBuild(string projectDirectory)
        {
            BuildProjectJson(projectDirectory);
            var projectJsonBuildOutputs = new HashSet<string>(CollectBuildOutputs(projectDirectory));
            CleanBinObj(projectDirectory);

            MigrateProject(projectDirectory);
            Restore(projectDirectory);
            BuildMSBuild(projectDirectory);

            var msbuildBuildOutputs = new HashSet<string>(CollectBuildOutputs(projectDirectory));

            return new MigratedBuildComparisonData(projectJsonBuildOutputs, msbuildBuildOutputs);
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
            var result =
                MigrateCommand.Run(new [] { "-p", projectDirectory });

            result.Should().Be(0);
        }

        private void DotnetNew(string projectDirectory, string dotnetNewType)
        {
            new NewCommand().WithWorkingDirectory(projectDirectory)
                .ExecuteWithCapturedOutput($"-t {dotnetNewType}")
                .Should()
                .Pass();
        }

        private void Restore(string projectDirectory)
        {
            new TestCommand("dotnet")
                .WithWorkingDirectory(projectDirectory)
                .Execute("restore")
                .Should()
                .Pass();
        }

        private string BuildMSBuild(string projectDirectory, string configuration="Debug")
        {
            var result = new Build3Command()
                .WithWorkingDirectory(projectDirectory)
                .ExecuteWithCapturedOutput($"/p:Configuration={configuration}");

            result
                .Should()
                .Pass();

            return result.StdOut;
        }

        private void OutputDiagnostics(MigratedBuildComparisonData comparisonData)
        {
            OutputDiagnostics(comparisonData.MSBuildBuildOutputs, comparisonData.ProjectJsonBuildOutputs);
        }

        private void OutputDiagnostics(HashSet<string> msbuildBuildOutputs, HashSet<string> projectJsonBuildOutputs)
        {
            Console.WriteLine("Project.json Outputs:");
            Console.WriteLine(string.Join("\n", projectJsonBuildOutputs));

            Console.WriteLine("");

            Console.WriteLine("MSBuild Outputs:");
            Console.WriteLine(string.Join("\n", msbuildBuildOutputs));
        }

        private class MigratedBuildComparisonData
        {
            public HashSet<string> ProjectJsonBuildOutputs { get; }
            public HashSet<string> MSBuildBuildOutputs { get; }

            public MigratedBuildComparisonData(HashSet<string> projectJsonBuildOutputs,
                HashSet<string> msBuildBuildOutputs)
            {
                ProjectJsonBuildOutputs = projectJsonBuildOutputs;
                MSBuildBuildOutputs = msBuildBuildOutputs;
            }
        }
    }
}
