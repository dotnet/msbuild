using Microsoft.Build.Construction;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Test.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using FluentAssertions;
using System.IO;
using Microsoft.DotNet.Tools.Migrate;
using Build3Command = Microsoft.DotNet.Tools.Test.Utilities.Build3Command;
using BuildCommand = Microsoft.DotNet.Tools.Test.Utilities.BuildCommand;
using System.Runtime.Loader;

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
            var projectDirectory = TestAssetsManager.CreateTestInstance(projectName, callingMethod: "i")
                                                    .WithLockFiles()
                                                    .Path;

            CleanBinObj(projectDirectory);

            var outputComparisonData = BuildProjectJsonMigrateBuildMSBuild(projectDirectory, projectName);

            var outputsIdentical =
                outputComparisonData.ProjectJsonBuildOutputs.SetEquals(outputComparisonData.MSBuildBuildOutputs);

            if (!outputsIdentical)
            {
                OutputDiagnostics(outputComparisonData);
            }

            outputsIdentical.Should().BeTrue();

            VerifyAllMSBuildOutputsRunnable(projectDirectory);
        }

        public void It_migrates_signed_apps(string projectName)
        {
            var projectDirectory = TestAssetsManager.CreateTestInstance("TestAppWithSigning", callingMethod: "i").WithLockFiles().Path;

            CleanBinObj(projectDirectory);

            var outputComparisonData = BuildProjectJsonMigrateBuildMSBuild(projectDirectory, projectName);

            var outputsIdentical =
                outputComparisonData.ProjectJsonBuildOutputs.SetEquals(outputComparisonData.MSBuildBuildOutputs);

            if (!outputsIdentical)
            {
                OutputDiagnostics(outputComparisonData);
            }

            outputsIdentical.Should().BeTrue();

            VerifyAllMSBuildOutputsRunnable(projectDirectory);

            VerifyAllMSBuildOutputsAreSigned(projectDirectory);
        }

        [Fact]
        public void It_migrates_dotnet_new_console_with_identical_outputs()
        {
            var testInstance = TestAssetsManager
                .CreateTestInstance("ProjectJsonConsoleTemplate");
            
            var projectDirectory = testInstance.Path;

            var outputComparisonData = GetComparisonData(projectDirectory);

            var outputsIdentical =
                outputComparisonData.ProjectJsonBuildOutputs.SetEquals(outputComparisonData.MSBuildBuildOutputs);

            if (!outputsIdentical)
            {
                OutputDiagnostics(outputComparisonData);
            }

            outputsIdentical.Should().BeTrue();

            VerifyAllMSBuildOutputsRunnable(projectDirectory);
        }

        [Fact(Skip="https://github.com/dotnet/cli/issues/4299")]
        public void It_migrates_dotnet_new_web_with_outputs_containing_project_json_outputs()
        {
            var testInstance = TestAssetsManager
                .CreateTestInstance("ProjectJsonWebTemplate");

            var projectDirectory = testInstance.Path;

            var outputComparisonData = GetComparisonData(projectDirectory);

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

            var outputComparisonData = BuildProjectJsonMigrateBuildMSBuild(projectDirectory, projectName);

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

            var outputComparisonData = BuildProjectJsonMigrateBuildMSBuild(projectDirectory, Path.GetFileNameWithoutExtension(projectName));

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

            MigrateProject(new [] { Path.Combine(projectDirectory, projectName) });

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

            MigrateProject(new [] { Path.Combine(projectDirectory, projectName), "--skip-project-references" });

            VerifyMigration(Enumerable.Repeat(projectName, 1), projectDirectory);
         }

         [Theory]
         [InlineData(true)]
         [InlineData(false)]
         public void It_migrates_all_projects_in_given_directory(bool skipRefs)
         {
            var projectDirectory = TestAssetsManager.CreateTestInstance("TestAppDependencyGraph", callingMethod: $"MigrateDirectory.SkipRefs.{skipRefs}").Path;

            if (skipRefs)
            {
                MigrateProject(new [] { projectDirectory, "--skip-project-references" });
            }
            else
            {
                MigrateProject(new [] { projectDirectory });
            }

            string[] migratedProjects = new string[] { "ProjectA", "ProjectB", "ProjectC", "ProjectD", "ProjectE", "ProjectF", "ProjectG", "ProjectH", "ProjectI", "ProjectJ" };

            VerifyMigration(migratedProjects, projectDirectory);
         }

         [Fact]
         public void It_migrates_given_project_json()
         {
            var projectDirectory = TestAssetsManager.CreateTestInstance("TestAppDependencyGraph").Path;

            var project = Path.Combine(projectDirectory, "ProjectA", "project.json");

            MigrateProject(new [] { project });

            string[] migratedProjects = new string[] { "ProjectA", "ProjectB", "ProjectC", "ProjectD", "ProjectE" };

            VerifyMigration(migratedProjects, projectDirectory);
         }

         [Fact]
         // regression test for https://github.com/dotnet/cli/issues/4269
         public void It_migrates_and_builds_P2P_references()
         {
            var assetsDir = TestAssetsManager.CreateTestInstance("TestAppDependencyGraph").WithLockFiles().Path;

            var projectDirectory = Path.Combine(assetsDir, "ProjectF");

            var restoreDirectories = new string[]
            {
                projectDirectory, 
                Path.Combine(assetsDir, "ProjectG")
            };

            var outputComparisonData = BuildProjectJsonMigrateBuildMSBuild(projectDirectory, "ProjectF", new [] { projectDirectory }, restoreDirectories);

            var outputsIdentical = outputComparisonData.ProjectJsonBuildOutputs
                                                       .SetEquals(outputComparisonData.MSBuildBuildOutputs);

            if (!outputsIdentical)
            {
                OutputDiagnostics(outputComparisonData);
            }

            outputsIdentical.Should().BeTrue();

            VerifyAllMSBuildOutputsRunnable(projectDirectory);
         }

         [Theory]
         [InlineData("src", "ProjectH")]
         [InlineData("src with spaces", "ProjectJ")]
         public void It_migrates_and_builds_projects_in_global_json(string path, string projectName)
         {
            var assetsDir = TestAssetsManager.CreateTestInstance(Path.Combine("TestAppDependencyGraph", "ProjectsWithGlobalJson"), 
                                                                 callingMethod: $"ProjectsWithGlobalJson.{projectName}")
                                             .WithLockFiles().Path;
            var globalJson = Path.Combine(assetsDir, "global.json");

            var restoreDirectories = new string[]
            {
                Path.Combine(assetsDir, "src", "ProjectH"),
                Path.Combine(assetsDir, "src", "ProjectI"),
                Path.Combine(assetsDir, "src with spaces", "ProjectJ")
            };

            var projectDirectory = Path.Combine(assetsDir, path, projectName);

            var outputComparisonData = BuildProjectJsonMigrateBuildMSBuild(projectDirectory, 
                                                                           projectName,
                                                                           new [] { globalJson },
                                                                           restoreDirectories);

            var outputsIdentical = outputComparisonData.ProjectJsonBuildOutputs
                                                       .SetEquals(outputComparisonData.MSBuildBuildOutputs);

            if (!outputsIdentical)
            {
                OutputDiagnostics(outputComparisonData);
            }

            outputsIdentical.Should().BeTrue();
            VerifyAllMSBuildOutputsRunnable(projectDirectory);
         }
        
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Migration_outputs_error_when_no_projects_found(bool useGlobalJson)
        {
            var projectDirectory = TestAssetsManager.CreateTestDirectory("Migration_outputs_error_when_no_projects_found");

            string argstr = string.Empty;

            string errorMessage = string.Empty;

            if (useGlobalJson)
            {
                var globalJsonPath = Path.Combine(projectDirectory.Path, "global.json");

                using (FileStream fs = File.Create(globalJsonPath))
                {
                    using (StreamWriter sw = new StreamWriter(fs))
                    {
                        sw.WriteLine("{");
                        sw.WriteLine("\"projects\": [ \".\" ]");
                        sw.WriteLine("}");
                    }
                }

                argstr = globalJsonPath;

                errorMessage = "Unable to find any projects in global.json";
            }
            else
            {
                argstr = projectDirectory.Path;

                errorMessage = $"No project.json file found in '{projectDirectory.Path}'";
            }

            var result = new TestCommand("dotnet")
                .WithWorkingDirectory(projectDirectory.Path)
                .ExecuteWithCapturedOutput($"migrate {argstr}");

            // Expecting an error exit code.
            result.ExitCode.Should().Be(1);

            // Verify the error messages. Note that debug builds also show the call stack, so we search
            // for the error strings that should be present (rather than an exact match).
            result.StdErr.Should().Contain(errorMessage);
            result.StdErr.Should().Contain("Migration failed.");
        }

        [WindowsOnlyTheory]
        [InlineData("DesktopTestProjects", "AutoAddDesktopReferencesDuringMigrate", true)]
        [InlineData("TestProjects", "TestAppSimple", false)]
        public void It_auto_add_desktop_references_during_migrate(string testGroup, string projectName, bool isDesktopApp)
        {
            var testAssetManager = GetTestGroupTestAssetsManager(testGroup);
            var projectDirectory = testAssetManager.CreateTestInstance(projectName, callingMethod: "i").WithLockFiles().Path;
            
            CleanBinObj(projectDirectory);
            MigrateProject(new string[] { projectDirectory });
            Restore3(projectDirectory);
            BuildMSBuild(projectDirectory, projectName);
            VerifyAutoInjectedDesktopReferences(projectDirectory, projectName, isDesktopApp);
            VerifyAllMSBuildOutputsRunnable(projectDirectory);
        }

        private void VerifyAutoInjectedDesktopReferences(string projectDirectory, string projectName, bool shouldBePresent)
        {
            if (projectName != null)
            {
                projectName = projectName + ".csproj";
            }

            var root = ProjectRootElement.Open(Path.Combine(projectDirectory, projectName));
            var autoInjectedReferences = root.Items.Where(i => i.ItemType == "Reference" && (i.Include == "System" || i.Include == "Microsoft.CSharp"));
            if (shouldBePresent)
            {
                autoInjectedReferences.Should().HaveCount(2);
            }
            else
            {
                autoInjectedReferences.Should().BeEmpty();
            }
        }

        private void VerifyMigration(IEnumerable<string> expectedProjects, string rootDir)
         {
             var migratedProjects = Directory.EnumerateFiles(rootDir, "project.json", SearchOption.AllDirectories)
                                             .Where(s => Directory.EnumerateFiles(Path.GetDirectoryName(s), "*.csproj").Count() == 1)
                                             .Where(s => Path.GetFileName(Path.GetDirectoryName(s)).Contains("Project"))
                                             .Select(s => Path.GetFileName(Path.GetDirectoryName(s)));

             migratedProjects.Should().BeEquivalentTo(expectedProjects);
         }

        private MigratedBuildComparisonData GetComparisonData(string projectDirectory)
        {
            File.Copy("NuGet.tempaspnetpatch.config", Path.Combine(projectDirectory, "NuGet.Config"));
            
            Restore(projectDirectory);

            var outputComparisonData =
                BuildProjectJsonMigrateBuildMSBuild(projectDirectory, Path.GetFileNameWithoutExtension(projectDirectory));

            return outputComparisonData;
        }

        private void VerifyAllMSBuildOutputsRunnable(string projectDirectory)
        {
            var dllFileName = Path.GetFileName(projectDirectory) + ".dll";

            var runnableDlls = Directory.EnumerateFiles(Path.Combine(projectDirectory, "bin"), dllFileName,
                SearchOption.AllDirectories);

            foreach (var dll in runnableDlls)
            {
                new TestCommand("dotnet").ExecuteWithCapturedOutput($"\"{dll}\"").Should().Pass();
            }
        }

        private void VerifyAllMSBuildOutputsAreSigned(string projectDirectory)
        {
            var dllFileName = Path.GetFileName(projectDirectory) + ".dll";

            var runnableDlls = Directory.EnumerateFiles(Path.Combine(projectDirectory, "bin"), dllFileName,
                SearchOption.AllDirectories);

            foreach (var dll in runnableDlls)
            {
                var assemblyName = AssemblyLoadContext.GetAssemblyName(dll);

                var token = assemblyName.GetPublicKeyToken();

                token.Should().NotBeNullOrEmpty();
            }
        }

        private MigratedBuildComparisonData BuildProjectJsonMigrateBuildMSBuild(string projectDirectory, 
                                                                                string projectName)
        {
            return BuildProjectJsonMigrateBuildMSBuild(projectDirectory, 
                                                       projectName,
                                                       new [] { projectDirectory }, 
                                                       new [] { projectDirectory });
        }

        private MigratedBuildComparisonData BuildProjectJsonMigrateBuildMSBuild(string projectDirectory, 
                                                                                string projectName,
                                                                                string[] migrateArgs,
                                                                                string[] restoreDirectories)
        {
            BuildProjectJson(projectDirectory);

            var projectJsonBuildOutputs = new HashSet<string>(CollectBuildOutputs(projectDirectory));

            CleanBinObj(projectDirectory);

            // Remove lock file for migration
            foreach(var dir in restoreDirectories)
            {
                File.Delete(Path.Combine(dir, "project.lock.json"));
            }

            MigrateProject(migrateArgs);

            DeleteXproj(projectDirectory);

            foreach(var dir in restoreDirectories)
            {
                Restore3(dir);
            }

            BuildMSBuild(projectDirectory, projectName);

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
                if(Directory.Exists(dir))
                {
                    Directory.Delete(dir, true);
                }
            }
        }

        private void BuildProjectJson(string projectDirectory)
        {
            var projectFile = Path.Combine(projectDirectory, "project.json");

            var result = new BuildCommand(projectPath: projectFile)
                .ExecuteWithCapturedOutput();

            result.Should().Pass();
        }

        private void MigrateProject(string[] migrateArgs)
        {
            var result =
                MigrateCommand.Run(migrateArgs);

            result.Should().Be(0);
        }

        private void Restore(string projectDirectory)
        {
            new TestCommand("dotnet")
                .WithWorkingDirectory(projectDirectory)
                .Execute("restore")
                .Should().Pass();
        }

        private void Restore3(string projectDirectory, string projectName=null)
        {
            var command = new Restore3Command()
                .WithWorkingDirectory(projectDirectory);

            if (projectName != null)
            {
                command.Execute($"{projectName}.csproj /p:SkipInvalidConfigurations=true")
                    .Should().Pass();
            }
            else
            {
                command.Execute("/p:SkipInvalidConfigurations=true")
                    .Should().Pass(); 
            }
        }

        private string BuildMSBuild(string projectDirectory, string projectName, string configuration="Debug")
        {
            if (projectName != null)
            {
                projectName = projectName + ".csproj";
            }

            DeleteXproj(projectDirectory);

            var result = new Build3Command()
                .WithWorkingDirectory(projectDirectory)
                .ExecuteWithCapturedOutput($"{projectName} /p:Configuration={configuration}");

            result
                .Should().Pass();

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
