// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using FluentAssertions;
using System.Xml.Linq;
using System.Linq;
using System;
using Xunit.Abstractions;
using Microsoft.Extensions.DependencyModel;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildAnAppWithTransitiveNonSdkProjectRefs : SdkTest
    {
        public GivenThatWeWantToBuildAnAppWithTransitiveNonSdkProjectRefs(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_builds_the_project_successfully()
        {
            // NOTE the project dependencies in AppWithTransitiveNonSdkProjectRefs:
            // TestApp --depends on--> MainLibrary --depends on--> AuxLibrary (non-SDK)
            // (TestApp transitively depends on AuxLibrary)

            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithTransitiveNonSdkProjectRefs", "BuildAppWithTransitiveProjectRef")
                .WithSource();

            VerifyAppBuilds(testAsset);
        }

        [Fact]
        public void It_builds_deps_correctly_when_projects_do_not_get_restored()
        {
            // NOTE the project dependencies in AppWithTransitiveProjectRefs:
            // TestApp --depends on--> MainLibrary --depends on--> AuxLibrary
            // (TestApp transitively depends on AuxLibrary)
            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithTransitiveNonSdkProjectRefs", "BuildAppWithTransitiveNonSdkProjectRefsNoRestore")
                .WithSource()
                .WithProjectChanges(
                    (projectName, project) =>
                    {
                        if (StringComparer.OrdinalIgnoreCase.Equals(Path.GetFileNameWithoutExtension(projectName), "AuxLibrary") ||
                            StringComparer.OrdinalIgnoreCase.Equals(Path.GetFileNameWithoutExtension(projectName), "MainLibrary"))
                        {
                            var ns = project.Root.Name.Namespace;

                            // indicate that project restore is not supported for these projects:
                            var target = new XElement(ns + "Target",
                                new XAttribute("Name", "_IsProjectRestoreSupported"),
                                new XAttribute("Returns", "@(_ValidProjectsForRestore)"));

                            project.Root.Add(target);
                        }
                    });

            string outputDirectory = VerifyAppBuilds(testAsset);

            using (var depsJsonFileStream = File.OpenRead(Path.Combine(outputDirectory, "TestApp.deps.json")))
            {
                var dependencyContext = new DependencyContextJsonReader().Read(depsJsonFileStream);

                var projectNames = dependencyContext.RuntimeLibraries.Select(library => library.Name).ToList();
                projectNames.Should().BeEquivalentTo(new[] { "TestApp", "AuxLibrary", "MainLibrary" });
            }
        }

        private string VerifyAppBuilds(TestAsset testAsset)
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
                "MainLibrary.dll",
                "MainLibrary.pdb",
                "AuxLibrary.dll",
                "AuxLibrary.pdb",
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
