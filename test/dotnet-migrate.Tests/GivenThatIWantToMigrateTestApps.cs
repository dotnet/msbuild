// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Construction;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Common;
using Microsoft.DotNet.Tools.Test.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using FluentAssertions;
using System.IO;
using Microsoft.DotNet.Tools.Migrate;
using BuildCommand = Microsoft.DotNet.Tools.Test.Utilities.BuildCommand;
using System.Runtime.Loader;
using Newtonsoft.Json.Linq;

using MigrateCommand = Microsoft.DotNet.Tools.Migrate.MigrateCommand;

namespace Microsoft.DotNet.Migration.Tests
{
    public class GivenThatIWantToMigrateTestApps : TestBase
    {
        [Theory]
        [InlineData("TestAppWithRuntimeOptions")]
        [InlineData("TestAppWithContents")]
        [InlineData("AppWithAssemblyInfo")]
        [InlineData("TestAppWithEmbeddedResources")]
        public void ItMigratesApps(string projectName)
        {
            var projectDirectory = TestAssets
                .GetProjectJson(projectName)
                .CreateInstance(identifier: projectName)
                .WithSourceFiles()
                .WithRestoreFiles()
                .Root;

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

            var outputCsProj = projectDirectory.GetFile(projectName + ".csproj");

            outputCsProj.ReadAllText()
                .Should().EndWith("\n");
        }

        [WindowsOnlyTheory]
        [InlineData("TestAppMultipleFrameworksNoRuntimes", null)]
        [InlineData("TestAppWithMultipleFullFrameworksOnly", "net461")]
        public void ItMigratesAppsWithFullFramework(string projectName, string framework)
        {
            var projectDirectory = TestAssets
                .GetProjectJson(projectName)
                .CreateInstance(identifier: projectName)
                .WithSourceFiles()
                .WithRestoreFiles()
                .Root;

            CleanBinObj(projectDirectory);

            MigrateProject(new [] { projectDirectory.FullName });

            Restore(projectDirectory);

            BuildMSBuild(projectDirectory, projectName, framework: framework);
        }

        [Fact]
        public void ItMigratesSignedApps()
        {
            var projectDirectory = TestAssets
                .GetProjectJson("TestAppWithSigning")
                .CreateInstance()
                .WithSourceFiles()
                .WithRestoreFiles()
                .Root;

            CleanBinObj(projectDirectory);

            var outputComparisonData = BuildProjectJsonMigrateBuildMSBuild(projectDirectory, "TestAppWithSigning");

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
        public void ItMigratesDotnetNewConsoleWithIdenticalOutputs()
        {
            var projectDirectory = TestAssets
                .GetProjectJson("ProjectJsonConsoleTemplate")
                .CreateInstance()
                .WithSourceFiles()
                .Root;

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

        [Fact]
        public void ItMigratesOldDotnetNewWebWithoutToolsWithOutputsContainingProjectJsonOutputs()
        {
            var projectDirectory = TestAssets
                .GetProjectJson("ProjectJsonWebTemplate")
                .CreateInstance()
                .WithSourceFiles()
                .WithRestoreFiles()
                .Root;

            var globalDirectory = projectDirectory.Parent;
              
            WriteGlobalJson(globalDirectory);

            var outputComparisonData = GetComparisonData(projectDirectory);

            var outputsIdentical =
                outputComparisonData.ProjectJsonBuildOutputs.SetEquals(outputComparisonData.MSBuildBuildOutputs);

            if (!outputsIdentical)
            {
                OutputDiagnostics(outputComparisonData);
            }

            outputsIdentical.Should().BeTrue();
        }

        [Fact]
        public void ItMigratesAndPublishesWebApp()
        {
            var projectName = "WebAppWithMissingFileInPublishOptions";

            var projectDirectory = TestAssets
                .GetProjectJson(projectName)
                    .CreateInstance()
                    .WithSourceFiles()
                    .Root;
            
            File.Copy("NuGet.tempaspnetpatch.config", projectDirectory.GetFile("NuGet.Config").FullName);

            MigrateProject(new [] { projectDirectory.FullName });

            Restore(projectDirectory);
            PublishMSBuild(projectDirectory, projectName);
        }

        [Fact]
        public void ItMigratesAPackageReferenceAsSuchEvenIfAFolderWithTheSameNameExistsInTheRepo()
        {
            var solutionDirectory = TestAssets
                .GetProjectJson("AppWithPackageNamedAfterFolder")
                .CreateInstance()
                .WithSourceFiles()
                .Root;

            var appProject = solutionDirectory
                .GetDirectory("src", "App")
                .GetFile("App.csproj");

            MigrateProject(solutionDirectory.FullName);

            var projectRootElement = ProjectRootElement.Open(appProject.FullName);

            projectRootElement.Items.Where(
                i => i.Include == "EntityFramework" && i.ItemType == "PackageReference")
                .Should().HaveCount(2);
        }
        [Fact]
        public void ItMigratesAProjectThatDependsOnAMigratedProjectWithTheSkipProjectReferenceFlag()
        {
            const string dependentProject = "ProjectA";
            const string dependencyProject = "ProjectB";

            var projectDirectory = TestAssets
                .GetProjectJson("TestAppDependencyGraph")
                .CreateInstance()
                .WithSourceFiles()
                .Root;

            MigrateProject(projectDirectory.GetDirectory(dependencyProject).FullName);

            MigrateProject("--skip-project-references", projectDirectory.GetDirectory(dependentProject).FullName);
        }

        [Fact]
        public void ItAddsMicrosoftNetWebSdkToTheSdkAttributeOfAWebApp()
        {
            var projectDirectory = TestAssets
                .Get("ProjectJsonWebTemplate")
                .CreateInstance()
                .WithSourceFiles()
                .WithRestoreFiles()
                .Root;

            var globalDirectory = projectDirectory.Parent;
            var projectJsonFile = projectDirectory.GetFile("project.json");  
              
            MigrateProject(new [] { projectDirectory.FullName });

            var csProj = projectDirectory.GetFile($"{projectDirectory.Name}.csproj");

            csProj.ReadAllText().Should().Contain(@"Sdk=""Microsoft.NET.Sdk.Web""");
        }

        [Theory]
        [InlineData("TestLibraryWithTwoFrameworks")]
        public void ItMigratesProjectsWithMultipleTFMs(string projectName)
        {
            var projectDirectory = TestAssets
                .GetProjectJson(projectName)
                .CreateInstance(identifier: projectName)
                .WithSourceFiles()
                .WithRestoreFiles()
                .Root;

            var outputComparisonData = BuildProjectJsonMigrateBuildMSBuild(projectDirectory, projectName);

            var outputsIdentical =
                outputComparisonData.ProjectJsonBuildOutputs.SetEquals(outputComparisonData.MSBuildBuildOutputs);

            if (!outputsIdentical)
            {
                OutputDiagnostics(outputComparisonData);
            }

            outputsIdentical.Should().BeTrue();
        }

        [WindowsOnlyFact]
        public void ItMigratesLibraryWithMultipleTFMsAndFullFramework()
        {
            var projectName = "PJLibWithMultipleFrameworks";

            var projectDirectory = TestAssets
                .GetProjectJson(projectName)
                .CreateInstance(identifier: projectName)
                .WithSourceFiles()
                .WithRestoreFiles()
                .Root;

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
        [InlineData("PJTestLibraryWithConfiguration")]
        public void ItMigratesALibrary(string projectName)
        {
            var projectDirectory = TestAssets
                .GetProjectJson(projectName)
                .CreateInstance(identifier: projectName)
                .WithSourceFiles()
                .WithRestoreFiles()
                .Root;

            var outputComparisonData = BuildProjectJsonMigrateBuildMSBuild(
                projectDirectory, 
                Path.GetFileNameWithoutExtension(projectName));

            var outputsIdentical = outputComparisonData
                .ProjectJsonBuildOutputs
                .SetEquals(outputComparisonData.MSBuildBuildOutputs);

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
        public void ItMigratesRootProjectAndReferences(string projectName, string expectedProjects)
        {
            var projectDirectory = TestAssets
                .GetProjectJson("TestAppDependencyGraph")
                .CreateInstance(identifier: $"{projectName}.RefsTest")
                .WithSourceFiles()
                .Root;

            MigrateProject(new [] { projectDirectory.GetDirectory(projectName).FullName });

            string[] migratedProjects = expectedProjects.Split(new char[] { ',' });

            VerifyMigration(migratedProjects, projectDirectory);
         }

        [Theory]
        [InlineData("ProjectA")]
        [InlineData("ProjectB")]
        [InlineData("ProjectC")]
        [InlineData("ProjectD")]
        [InlineData("ProjectE")]
        public void ItMigratesRootProjectAndSkipsReferences(string projectName)
        {
            var projectDirectory = TestAssets
                .GetProjectJson("TestAppDependencyGraph")
                .CreateInstance($"{projectName}.SkipRefsTest")
                .WithSourceFiles()
                .Root;
                
            MigrateProject(new [] { projectDirectory.GetDirectory(projectName).FullName, "--skip-project-references" });

            VerifyMigration(Enumerable.Repeat(projectName, 1), projectDirectory);
         }

         [Theory]
         [InlineData(true)]
         [InlineData(false)]
         public void ItMigratesAllProjectsInGivenDirectory(bool skipRefs)
         {
            var projectDirectory = TestAssets
                .GetProjectJson("TestAppDependencyGraph")
                .CreateInstance(callingMethod: $"MigrateDirectory.SkipRefs.{skipRefs}")
                .WithSourceFiles()
                .Root;

            if (skipRefs)
            {
                MigrateProject(new [] { projectDirectory.FullName, "--skip-project-references" });
            }
            else
            {
                MigrateProject(new [] { projectDirectory.FullName });
            }

            string[] migratedProjects = new string[] { "ProjectA", "ProjectB", "ProjectC", "ProjectD", "ProjectE", "ProjectF", "ProjectG", "ProjectH", "ProjectI", "ProjectJ" };

            VerifyMigration(migratedProjects, projectDirectory);
         }

         [Fact]
         public void ItMigratesGivenProjectJson()
         {
            var projectDirectory = TestAssets
                .GetProjectJson("TestAppDependencyGraph")
                .CreateInstance()
                .WithSourceFiles()
                .Root;

            var project = projectDirectory
                .GetDirectory("ProjectA")
                .GetFile("project.json");

            MigrateProject(new [] { project.FullName });

            string[] migratedProjects = new string[] { "ProjectA", "ProjectB", "ProjectC", "ProjectD", "ProjectE" };

            VerifyMigration(migratedProjects, projectDirectory);
         }

         [Fact]
         // regression test for https://github.com/dotnet/cli/issues/4269
         public void ItMigratesAndBuildsP2PReferences()
         {
            var assetsDir = TestAssets
                .GetProjectJson("TestAppDependencyGraph")
                .CreateInstance()
                .WithSourceFiles()
                .WithRestoreFiles()
                .Root;

            var projectDirectory = assetsDir.GetDirectory("ProjectF");

            var restoreDirectories = new DirectoryInfo[]
            {
                projectDirectory, 
                assetsDir.GetDirectory("ProjectG")
            };

            var outputComparisonData = BuildProjectJsonMigrateBuildMSBuild(projectDirectory, "ProjectF", new [] { projectDirectory.FullName }, restoreDirectories);

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
         [InlineData("src", "H")]
         [InlineData("src with spaces", "J")]
         public void ItMigratesAndBuildsProjectsInGlobalJson(string path, string projectNameSuffix)
         {
            var assetsDir = TestAssets
                .GetProjectJson("ProjectsWithGlobalJson")
                .CreateInstance(identifier: projectNameSuffix)
                .WithSourceFiles()
                .WithRestoreFiles()
                .Root;

            var projectName = $"Project{projectNameSuffix}";

            var globalJson = assetsDir.GetFile("global.json");

            var restoreDirectories = new DirectoryInfo[]
            {
                assetsDir.GetDirectory("src", "ProjectH"),
                assetsDir.GetDirectory("src", "ProjectI"),
                assetsDir.GetDirectory("src with spaces", "ProjectJ")
            };

            var projectDirectory = assetsDir.GetDirectory(path, projectName);

            var outputComparisonData = BuildProjectJsonMigrateBuildMSBuild(projectDirectory, 
                                                                           projectName,
                                                                           new [] { globalJson.FullName },
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
        public void MigrationOutputsErrorWhenNoProjectsFound(bool useGlobalJson)
        {
            var projectDirectory = TestAssets.CreateTestDirectory("Migration_outputs_error_when_no_projects_found");

            string argstr = string.Empty;

            string errorMessage = string.Empty;

            if (useGlobalJson)
            {
                var globalJson = projectDirectory.GetFile("global.json");

                using (StreamWriter sw = globalJson.CreateText())
                {
                    sw.WriteLine("{");
                    sw.WriteLine("\"projects\": [ \".\" ]");
                    sw.WriteLine("}");
                }

                argstr = globalJson.FullName;

                errorMessage = "Unable to find any projects in global.json";
            }
            else
            {
                argstr = projectDirectory.FullName;

                errorMessage = $"No project.json file found in '{projectDirectory.FullName}'";
            }

            var result = new TestCommand("dotnet")
                .WithWorkingDirectory(projectDirectory)
                .ExecuteWithCapturedOutput($"migrate {argstr}");

            // Expecting an error exit code.
            result.ExitCode.Should().Be(1);

            // Verify the error messages. Note that debug builds also show the call stack, so we search
            // for the error strings that should be present (rather than an exact match).
            result.StdErr
                .Should().Contain(errorMessage)
                .And.Contain("Migration failed.");
        }

        [Fact]
        public void ItMigratesAndPublishesProjectsWithRuntimes()
        {
            var projectName = "PJTestAppSimple";
            var projectDirectory = TestAssets
                .GetProjectJson(projectName)
                .CreateInstance()
                .WithSourceFiles()
                .WithRestoreFiles()
                .Root;

            CleanBinObj(projectDirectory);
            BuildProjectJsonMigrateBuildMSBuild(projectDirectory, projectName);
            PublishMSBuild(projectDirectory, projectName, "win7-x64");
        }

        [WindowsOnlyTheory]
        [InlineData("DesktopTestProjects", "AutoAddDesktopReferencesDuringMigrate", true)]
        [InlineData("TestProjects", "PJTestAppSimple", false)]
        public void ItAutoAddDesktopReferencesDuringMigrate(string testGroup, string projectName, bool isDesktopApp)
        {
            var runtime = DotnetLegacyRuntimeIdentifiers.InferLegacyRestoreRuntimeIdentifier();

            var projectDirectory = TestAssets
                .GetProjectJson(testGroup, projectName)
                .CreateInstance()
                .WithSourceFiles()
                .WithRestoreFiles()
                .Root;

            CleanBinObj(projectDirectory);
            MigrateProject(new string[] { projectDirectory.FullName });
            Restore(projectDirectory, runtime: runtime);
            BuildMSBuild(projectDirectory, projectName, runtime:runtime);
            VerifyAutoInjectedDesktopReferences(projectDirectory, projectName, isDesktopApp);
            VerifyAllMSBuildOutputsRunnable(projectDirectory);
        }

        [Fact]
        public void ItBuildsAMigratedAppWithAnIndirectDependency()
        {
            const string projectName = "ProjectA";

            var solutionDirectory = TestAssets
                .GetProjectJson("TestAppDependencyGraph")
                .CreateInstance()
                .WithSourceFiles()
                .Root;

            var projectDirectory = solutionDirectory.GetDirectory(projectName);

            MigrateProject(new string[] { projectDirectory.FullName });

            Restore(projectDirectory);

            BuildMSBuild(projectDirectory, projectName);

            VerifyAllMSBuildOutputsRunnable(projectDirectory);
        }

        [Fact]
        public void ItMigratesProjectWithOutputName()
        {
            var projectName = "AppWithOutputAssemblyName";
            var expectedOutputName = "MyApp";

            var projectDirectory = TestAssets
                .GetProjectJson(projectName)
                .CreateInstance()
                .WithSourceFiles()
                .WithRestoreFiles()
                .Root;

            var expectedCsprojPath = projectDirectory.GetFile($"{projectName}.csproj");

            if (expectedCsprojPath.Exists)
            {
                expectedCsprojPath.Delete();
            }

            CleanBinObj(projectDirectory);
            MigrateProject(projectDirectory.FullName);

            expectedCsprojPath.Refresh();

            expectedCsprojPath.Should().Exist();

            Restore(projectDirectory, projectName);
            BuildMSBuild(projectDirectory, projectName);
            projectDirectory
                .GetDirectory("bin")
                .EnumerateFiles($"{expectedOutputName}.pdb", SearchOption.AllDirectories)
                .Count().Should().Be(1);

            PackMSBuild(projectDirectory, projectName);

            projectDirectory
                .GetDirectory("bin")
                .EnumerateFiles($"{projectName}.1.0.0.nupkg", SearchOption.AllDirectories)
                .Count().Should().Be(1);
        }

        [Theory]
        [InlineData("LibraryWithoutNetStandardLibRef")]
        [InlineData("LibraryWithNetStandardLibRef")]
        public void ItMigratesAndBuildsLibrary(string projectName)
        {
            var projectDirectory = TestAssets
                .GetProjectJson(projectName)
                .CreateInstance(identifier: projectName)
                .WithSourceFiles()
                .Root;

            MigrateProject(projectDirectory.FullName);
            Restore(projectDirectory, projectName);
            BuildMSBuild(projectDirectory, projectName);
        }
        
        private void VerifyAutoInjectedDesktopReferences(DirectoryInfo projectDirectory, string projectName, bool shouldBePresent)
        {
            if (projectName != null)
            {
                projectName = projectName + ".csproj";
            }

            var root = ProjectRootElement.Open(projectDirectory.GetFile(projectName).FullName);

            var autoInjectedReferences = root
                .Items
                .Where(i => i.ItemType == "Reference" 
                         && (i.Include == "System" || i.Include == "Microsoft.CSharp"));

            if (shouldBePresent)
            {
                autoInjectedReferences.Should().HaveCount(2);
            }
            else
            {
                autoInjectedReferences.Should().BeEmpty();
            }
        }

        private void VerifyMigration(IEnumerable<string> expectedProjects, DirectoryInfo rootDir)
         {
             var backupDir = rootDir.GetDirectory("backup");

             var migratedProjects = rootDir.EnumerateFiles("*.csproj", SearchOption.AllDirectories)
                                           .Where(s => !PathUtility.IsChildOfDirectory(backupDir.FullName, s.FullName))
                                           .Where(s => Directory.EnumerateFiles(Path.GetDirectoryName(s.FullName), "*.csproj").Count() == 1)
                                           .Where(s => Path.GetFileName(Path.GetDirectoryName(s.FullName)).Contains("Project"))
                                           .Select(s => Path.GetFileName(Path.GetDirectoryName(s.FullName)));

             migratedProjects.Should().BeEquivalentTo(expectedProjects);
         }

        private MigratedBuildComparisonData GetComparisonData(DirectoryInfo projectDirectory)
        {
            File.Copy("NuGet.tempaspnetpatch.config", projectDirectory.GetFile("NuGet.Config").FullName);
            
            RestoreProjectJson(projectDirectory);

            var outputComparisonData =
                BuildProjectJsonMigrateBuildMSBuild(projectDirectory, Path.GetFileNameWithoutExtension(projectDirectory.FullName));

            return outputComparisonData;
        }

        private void VerifyAllMSBuildOutputsRunnable(DirectoryInfo projectDirectory)
        {
            var dllFileName = Path.GetFileName(projectDirectory.FullName) + ".dll";

            var runnableDlls = projectDirectory
                .GetDirectory("bin")
                .GetFiles(dllFileName, SearchOption.AllDirectories);

            foreach (var dll in runnableDlls)
            {
                new TestCommand("dotnet").ExecuteWithCapturedOutput($"\"{dll.FullName}\"").Should().Pass();
            }
        }

        private void VerifyAllMSBuildOutputsAreSigned(DirectoryInfo projectDirectory)
        {
            var dllFileName = Path.GetFileName(projectDirectory.FullName) + ".dll";

            var runnableDlls = projectDirectory
                .GetDirectory("bin")
                .EnumerateFiles(dllFileName, SearchOption.AllDirectories);

            foreach (var dll in runnableDlls)
            {
                var assemblyName = AssemblyLoadContext.GetAssemblyName(dll.FullName);

                var token = assemblyName.GetPublicKeyToken();

                token.Should().NotBeNullOrEmpty();
            }
        }

        private MigratedBuildComparisonData BuildProjectJsonMigrateBuildMSBuild(DirectoryInfo projectDirectory, 
                                                                                string projectName)
        {
            return BuildProjectJsonMigrateBuildMSBuild(projectDirectory, 
                                                       projectName,
                                                       new [] { projectDirectory.FullName }, 
                                                       new [] { projectDirectory });
        }

        private MigratedBuildComparisonData BuildProjectJsonMigrateBuildMSBuild(DirectoryInfo projectDirectory, 
                                                                                string projectName,
                                                                                string[] migrateArgs,
                                                                                DirectoryInfo[] restoreDirectories)
        {
            BuildProjectJson(projectDirectory);

            var projectJsonBuildOutputs = new HashSet<string>(CollectBuildOutputs(projectDirectory.FullName));

            CleanBinObj(projectDirectory);

            // Remove lock file for migration
            foreach(var dir in restoreDirectories)
            {
                dir.GetFile("project.lock.json").Delete();
            }

            MigrateProject(migrateArgs);

            DeleteXproj(projectDirectory);

            foreach(var dir in restoreDirectories)
            {
                Restore(dir);
            }

            BuildMSBuild(projectDirectory, projectName);

            var msbuildBuildOutputs = new HashSet<string>(CollectBuildOutputs(projectDirectory.FullName));

            return new MigratedBuildComparisonData(projectJsonBuildOutputs, msbuildBuildOutputs);
        }

        private IEnumerable<string> CollectBuildOutputs(string projectDirectory)
        {
            var fullBinPath = Path.GetFullPath(Path.Combine(projectDirectory, "bin"));

            return Directory.EnumerateFiles(fullBinPath, "*", SearchOption.AllDirectories)
                            .Select(p => Path.GetFullPath(p).Substring(fullBinPath.Length));
        }

        private void CleanBinObj(DirectoryInfo projectDirectory)
        {
            var dirs = new DirectoryInfo[] { projectDirectory.GetDirectory("bin"), projectDirectory.GetDirectory("obj") };

            foreach (var dir in dirs)
            {
                if(dir.Exists)
                {
                    dir.Delete(true);
                }
            }
        }

        private void BuildProjectJson(DirectoryInfo projectDirectory)
        {
            Console.WriteLine(projectDirectory);
            
            var projectFile = $"\"{projectDirectory.GetFile("project.json").FullName}\"";

            var result = new BuildPJCommand()
                .WithCapturedOutput()
                .WithForwardingToConsole()
                .Execute(projectFile);

            result.Should().Pass();
        }

        private void MigrateProject(params string[] migrateArgs)
        {
            new TestCommand("dotnet")
                    .WithForwardingToConsole()
                    .Execute($"migrate {string.Join(" ", migrateArgs)}")
                    .Should()
                    .Pass();
        }

        private void RestoreProjectJson(DirectoryInfo projectDirectory)
        {
            var projectFile = $"\"{projectDirectory.GetFile("project.json").FullName}\"";
            new RestoreProjectJsonCommand()
                .Execute(projectFile)
                .Should().Pass();
        }

        private void Restore(DirectoryInfo projectDirectory, string projectName=null, string runtime=null)
        {
            var command = new RestoreCommand()
                .WithWorkingDirectory(projectDirectory)
                .WithRuntime(runtime);

            if (projectName != null)
            {
                if (!Path.HasExtension(projectName))
                {
                    projectName += ".csproj";
                }
                command.Execute($"{projectName} /p:SkipInvalidConfigurations=true;_InvalidConfigurationWarning=false")
                    .Should().Pass();
            }
            else
            {
                command.Execute("/p:SkipInvalidConfigurations=true;_InvalidConfigurationWarning=false")
                    .Should().Pass(); 
            }
        }

        private string BuildMSBuild(
            DirectoryInfo projectDirectory,
            string projectName,
            string configuration="Debug",
            string runtime=null,
            string framework=null)
        {
            if (projectName != null && !Path.HasExtension(projectName))
            {
                projectName = projectName + ".csproj";
            }

            DeleteXproj(projectDirectory);

            var result = new BuildCommand()
                .WithWorkingDirectory(projectDirectory)
                .WithRuntime(runtime)
                .WithFramework(framework)
                .ExecuteWithCapturedOutput($"{projectName} /p:Configuration={configuration}");

            result
                .Should().Pass();

            return result.StdOut;
        }

        private string PublishMSBuild(
            DirectoryInfo projectDirectory,
            string projectName,
            string runtime = null,
            string configuration = "Debug")
        {
            if (projectName != null)
            {
                projectName = projectName + ".csproj";
            }

            DeleteXproj(projectDirectory);

            var result = new PublishCommand()
                .WithRuntime(runtime)
                .WithWorkingDirectory(projectDirectory)
                .ExecuteWithCapturedOutput($"{projectName} /p:Configuration={configuration}");

            result.Should().Pass();

            return result.StdOut;
        }

        private string PackMSBuild(DirectoryInfo projectDirectory, string projectName)
        {
            if (projectName != null && !Path.HasExtension(projectName))
            {
                projectName = projectName + ".csproj";
            }

            var result = new PackCommand()
                .WithWorkingDirectory(projectDirectory)
                .ExecuteWithCapturedOutput($"{projectName}");

            result.Should().Pass();

            return result.StdOut;
        }

        private void DeleteXproj(DirectoryInfo projectDirectory)
        {
            var xprojFiles = projectDirectory.EnumerateFiles("*.xproj");

            foreach (var xprojFile in xprojFiles)
            {
                xprojFile.Delete();
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

        private void WriteGlobalJson(DirectoryInfo globalDirectory)  
        {  
            var file = globalDirectory.GetFile("global.json");  

            File.WriteAllText(file.FullName, @"  
            {  
                ""projects"": [ ]  
            }");  
        }
    }
}
