// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.DependencyModel;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildAnAppWithTransitiveNonSdkProjectRefs : SdkTest
    {
        public GivenThatWeWantToBuildAnAppWithTransitiveNonSdkProjectRefs(ITestOutputHelper log) : base(log)
        {
        }

        [WindowsOnlyFact]
        public void It_builds_the_project_successfully()
        {
            // NOTE the projects created by CreateTestProject:
            // TestApp --depends on--> MainLibrary --depends on--> AuxLibrary (non-SDK)
            // (TestApp transitively depends on AuxLibrary)
            var testAsset = _testAssetsManager
                .CreateTestProject(CreateTestProject());

            VerifyAppBuilds(testAsset, string.Empty);
        }

        [WindowsOnlyTheory]
        [InlineData("")]
        [InlineData("TestApp.")]
        public void It_builds_deps_correctly_when_projects_do_not_get_restored(string prefix)
        {
            // NOTE the projects created by CreateTestProject:
            // TestApp --depends on--> MainLibrary --depends on--> AuxLibrary
            // (TestApp transitively depends on AuxLibrary)
            var testAsset = _testAssetsManager
                .CreateTestProject(CreateTestProject())
                .WithProjectChanges(
                    (projectName, project) =>
                    {
                        string projectFileName = Path.GetFileNameWithoutExtension(projectName);
                        if (StringComparer.OrdinalIgnoreCase.Equals(projectFileName, "AuxLibrary") ||
                            StringComparer.OrdinalIgnoreCase.Equals(projectFileName, "MainLibrary"))
                        {
                            var ns = project.Root.Name.Namespace;

                            XElement propertyGroup = project.Root.Element(ns + "PropertyGroup");
                            if (!string.IsNullOrEmpty(prefix))
                            {
                                XElement assemblyName = propertyGroup.Element(ns + "AssemblyName");
                                assemblyName.RemoveAll();
                                assemblyName.Add(prefix + projectFileName);
                            }

                            // indicate that project restore is not supported for these projects:
                            var target = new XElement(ns + "Target",
                                new XAttribute("Name", "_IsProjectRestoreSupported"),
                                new XAttribute("Returns", "@(_ValidProjectsForRestore)"));

                            project.Root.Add(target);
                        }
                        else // if (StringComparer.OrdinalIgnoreCase.Equals(projectFileName, "TestApp"))
                        {
                            var ns = project.Root.Name.Namespace;

                            XElement propertyGroup = project.Root.Element(ns + "PropertyGroup");

                            XElement includeProjectsNotInAssetsFileInDepsFile = new(ns + "IncludeProjectsNotInAssetsFileInDepsFile");
                            includeProjectsNotInAssetsFileInDepsFile.Add("true");
                            propertyGroup.Add(includeProjectsNotInAssetsFileInDepsFile);
                        }
                    });

            string outputDirectory = VerifyAppBuilds(testAsset, prefix);

            using (var depsJsonFileStream = File.OpenRead(Path.Combine(outputDirectory, "TestApp.deps.json")))
            {
                var dependencyContext = new DependencyContextJsonReader().Read(depsJsonFileStream);

                var projectNames = dependencyContext.RuntimeLibraries.Select(library => library.Name).ToList();
                projectNames.Should().BeEquivalentTo(new[] { "TestApp", prefix + "AuxLibrary", prefix + "MainLibrary" });
            }
        }

        private TestProject CreateTestProject()
        {
            string targetFrameworkVersion = "v4.8";

            var auxLibraryProject = new TestProject("AuxLibrary")
            {
                IsSdkProject = false,
                TargetFrameworkVersion = targetFrameworkVersion
            };
            auxLibraryProject.SourceFiles["Helper.cs"] = @"
                using System;

                namespace AuxLibrary
                {
                    public static class Helper
                    {
                        public static void WriteMessage()
                        {
                            Console.WriteLine(""This string came from AuxLibrary!"");
                        }
                    }
                }
                ";

            var mainLibraryProject = new TestProject("MainLibrary")
            {
                IsSdkProject = false,
                TargetFrameworkVersion = targetFrameworkVersion
            };
            mainLibraryProject.ReferencedProjects.Add(auxLibraryProject);
            mainLibraryProject.SourceFiles["Helper.cs"] = @"
                using System;

                namespace MainLibrary
                {
                    public static class Helper
                    {
                        public static void WriteMessage()
                        {
                            Console.WriteLine(""This string came from MainLibrary!"");
                            AuxLibrary.Helper.WriteMessage();
                        }
                    }
                }
            ";

            var testAppProject = new TestProject("TestApp")
            {
                IsExe = true,
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework
            };
            testAppProject.AdditionalProperties["ProduceReferenceAssembly"] = "false";
            testAppProject.ReferencedProjects.Add(mainLibraryProject);
            testAppProject.SourceFiles["Program.cs"] = @"
                using System;

                namespace TestApp
                {
                    public class Program
                    {
                        public static void Main(string[] args)
                        {
                            Console.WriteLine(""TestApp --depends on--> MainLibrary --depends on--> AuxLibrary"");
                            MainLibrary.Helper.WriteMessage();
                        }
                    }
                }
                ";

            return testAppProject;
        }

        private string VerifyAppBuilds(TestAsset testAsset, string prefix)
        {
            var buildCommand = new BuildCommand(testAsset, "TestApp");
            var outputDirectory = buildCommand.GetOutputDirectory(ToolsetInfo.CurrentTargetFramework);

            buildCommand
                .Execute()
                .Should()
                .Pass();

            outputDirectory.Should().OnlyHaveFiles(new[] {
                "TestApp.dll",
                "TestApp.pdb",
                $"TestApp{EnvironmentInfo.ExecutableExtension}",
                "TestApp.deps.json",
                "TestApp.runtimeconfig.json",
                prefix + "MainLibrary.dll",
                prefix + "MainLibrary.pdb",
                prefix + "AuxLibrary.dll",
                prefix + "AuxLibrary.pdb",
            });

            new DotnetCommand(Log, Path.Combine(outputDirectory.FullName, "TestApp.dll"))
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("This string came from MainLibrary!")
                .And
                .HaveStdOutContaining("This string came from AuxLibrary!");

            return outputDirectory.FullName;
        }
    }
}
