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
using BuildCommand = Microsoft.DotNet.Tools.Test.Utilities.BuildCommand;

namespace Microsoft.DotNet.Migration.Tests
{
    public class GivenThatIWantToMigrateTestApps : TestBase
    {
        [Theory]
        // TODO: Standalone apps [InlineData("TestAppSimple", false)]
        // https://github.com/dotnet/sdk/issues/73 [InlineData("TestAppWithLibrary/TestApp", false)]
        [InlineData("TestAppWithRuntimeOptions")]
        [InlineData("TestAppWithContents")]
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
        public void It_migrates_projects_with_multiple_TFMs(string projectName)
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

        [Theory]
        [InlineData("ProjectA", "ProjectA,ProjectB,ProjectC,ProjectD,ProjectE")]
        [InlineData("ProjectB", "ProjectB,ProjectC,ProjectD,ProjectE")]
        [InlineData("ProjectC", "ProjectC,ProjectD,ProjectE")]
        [InlineData("ProjectD", "ProjectD")]
        [InlineData("ProjectE", "ProjectE")]
        public void It_migrates_root_project_and_references(string projectName, string expectedProjects)
        {
            var projectDirectory =
                TestAssetsManager.CreateTestInstance("TestAppDependencyGraph", callingMethod: $"{projectName}.RefsTest").Path;

            FixUpProjectJsons(projectDirectory);

            MigrateProject(Path.Combine(projectDirectory, projectName));

            string[] migratedProjects = expectedProjects.Split(new char[] { ',' });
            VerifyMigration(migratedProjects, projectDirectory);
         }

        [Theory]
        [InlineData("ProjectA")]
        [InlineData("ProjectB")]
        [InlineData("ProjectC")]
        [InlineData("ProjectD")]
        [InlineData("ProjectE")]
        public void It_migrates_root_project_and_skips_references(string projectName)
        {
            var projectDirectory =
                TestAssetsManager.CreateTestInstance("TestAppDependencyGraph", callingMethod: $"{projectName}.SkipRefsTest").Path;

            FixUpProjectJsons(projectDirectory);

            MigrateCommand.Run(new [] { Path.Combine(projectDirectory, projectName), "--skip-project-references" }).Should().Be(0);

            VerifyMigration(Enumerable.Repeat(projectName, 1), projectDirectory);
         }

         [Theory]
         [InlineData(true)]
         [InlineData(false)]
         public void It_migrates_all_projects_in_given_directory(bool skipRefs)
         {
            var projectDirectory = TestAssetsManager.CreateTestInstance("TestAppDependencyGraph", callingMethod: $"MigrateDirectory.SkipRefs.{skipRefs}").Path;

            FixUpProjectJsons(projectDirectory);

            if (skipRefs)
            {
                MigrateCommand.Run(new [] { projectDirectory, "--skip-project-references" }).Should().Be(0);
            }
            else
            {
                MigrateCommand.Run(new [] { projectDirectory }).Should().Be(0);
            }

            string[] migratedProjects = new string[] { "ProjectA", "ProjectB", "ProjectC", "ProjectD", "ProjectE", "ProjectF", "ProjectG" };
            VerifyMigration(migratedProjects, projectDirectory);
         }

         [Fact]
         public void It_migrates_given_project_json()
         {
            var projectDirectory = TestAssetsManager.CreateTestInstance("TestAppDependencyGraph").Path;

            FixUpProjectJsons(projectDirectory);

            var project = Path.Combine(projectDirectory, "ProjectA", "project.json");
            MigrateCommand.Run(new [] { project }).Should().Be(0);

            string[] migratedProjects = new string[] { "ProjectA", "ProjectB", "ProjectC", "ProjectD", "ProjectE" };
            VerifyMigration(migratedProjects, projectDirectory);
         }

         private void FixUpProjectJsons(string projectDirectory)
         {
             var pjs = Directory.EnumerateFiles(projectDirectory, "project.json.1", SearchOption.AllDirectories);

             foreach(var pj in pjs)
             {
                 var newPj = pj.Replace("project.json.1", "project.json");
                 File.Move(pj, newPj);
             }
         }

         private void VerifyMigration(IEnumerable<string> expectedProjects, string rootDir)
         {
             var migratedProjects = Directory.EnumerateFiles(rootDir, "*.csproj", SearchOption.AllDirectories)
                                            .Select(s => Path.GetFileNameWithoutExtension(s));
             migratedProjects.Should().BeEquivalentTo(expectedProjects);
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

            // Remove lock file for migration
            File.Delete(Path.Combine(projectDirectory, "project.lock.json"));

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
                MigrateCommand.Run(new [] { projectDirectory });

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
            DeleteXproj(projectDirectory);

            var result = new Build3Command()
                .WithWorkingDirectory(projectDirectory)
                .ExecuteWithCapturedOutput($"/p:Configuration={configuration}");

            result
                .Should()
                .Pass();

            return result.StdOut;
        }

        private void DeleteXproj(string projectDirectory)
        {
            var xprojFiles = Directory.EnumerateFiles(projectDirectory, "*.xproj");
            foreach (var xprojFile in xprojFiles)
            {
                File.Delete(xprojFile);
            }
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
