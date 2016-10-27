// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using FluentAssertions;
using Microsoft.Extensions.DependencyModel;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using static Microsoft.NET.TestFramework.Commands.MSBuildTest;

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToPreserveCompilationContext
    {
        private TestAssetsManager _testAssetsManager = TestAssetsManager.TestProjectsAssetsManager;

        [Fact]
        public void It_publishes_the_project_with_a_refs_folder_and_correct_deps_file()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("CompilationContext", "PreserveCompilationContext")
                .WithSource();

            testAsset.Restore("TestApp");
            testAsset.Restore("TestLibrary");

            var appProjectDirectory = Path.Combine(testAsset.TestRoot, "TestApp");

            foreach (var targetFramework in new[] { "net46", "netcoreapp1.0" })
            {
                var publishCommand = new PublishCommand(Stage0MSBuild, appProjectDirectory);

                if (targetFramework == "net46" && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    continue;
                }

                publishCommand
                    .Execute($"/p:TargetFramework={targetFramework}")
                    .Should()
                    .Pass();

                var publishDirectory = publishCommand.GetOutputDirectory(targetFramework, runtimeIdentifier: "win7-x86");

                publishDirectory.Should().HaveFiles(new[] {
                    targetFramework == "net46" ? "TestApp.exe" : "TestApp.dll",
                    "TestLibrary.dll",
                    "Newtonsoft.Json.dll"});

                var refsDirectory = new DirectoryInfo(Path.Combine(publishDirectory.FullName, "refs"));
                // Should have compilation time assemblies
                refsDirectory.Should().HaveFile("System.IO.dll");
                // Libraries in which lib==ref should be deduped
                refsDirectory.Should().NotHaveFile("TestLibrary.dll");
                refsDirectory.Should().NotHaveFile("Newtonsoft.Json.dll");

                using (var depsJsonFileStream = File.OpenRead(Path.Combine(publishDirectory.FullName, "TestApp.deps.json")))
                {
                    var dependencyContext = new DependencyContextJsonReader().Read(depsJsonFileStream);

                    string[] expectedDefines;
                    if (targetFramework == "net46")
                    {
                        expectedDefines = new[] { "DEBUG", "TRACE", "NET46" };
                    }
                    else
                    {
                        expectedDefines = new[] { "DEBUG", "TRACE", "NETCOREAPP1_0" };
                    }

                    dependencyContext.CompilationOptions.Defines.Should().BeEquivalentTo(expectedDefines);
                    dependencyContext.CompilationOptions.LanguageVersion.Should().Be("");
                    dependencyContext.CompilationOptions.Platform.Should().Be("x86");
                    dependencyContext.CompilationOptions.Optimize.Should().Be(false);
                    dependencyContext.CompilationOptions.KeyFile.Should().Be("");
                    dependencyContext.CompilationOptions.EmitEntryPoint.Should().Be(true);
                    dependencyContext.CompilationOptions.DebugType.Should().Be("portable");

                    dependencyContext.CompileLibraries.Count.Should().Be(targetFramework == "net46" ? 53 : 149);

                    // Ensure P2P references are specified correctly
                    var testLibrary = dependencyContext
                        .CompileLibraries
                        .FirstOrDefault(l => string.Equals(l.Name, "testlibrary", StringComparison.OrdinalIgnoreCase));

                    testLibrary.Assemblies.Count.Should().Be(1);
                    testLibrary.Assemblies[0].Should().Be("TestLibrary.dll");

                    // Ensure framework references are specified correctly
                    if (targetFramework == "net46")
                    {
                        var mscorlibLibrary = dependencyContext
                            .CompileLibraries
                            .FirstOrDefault(l => string.Equals(l.Name, "mscorlib", StringComparison.OrdinalIgnoreCase));
                        mscorlibLibrary.Assemblies.Count.Should().Be(1);
                        mscorlibLibrary.Assemblies[0].Should().Be(".NETFramework/v4.6/mscorlib.dll");

                        var systemCoreLibrary = dependencyContext
                            .CompileLibraries
                            .FirstOrDefault(l => string.Equals(l.Name, "system.core", StringComparison.OrdinalIgnoreCase));
                        systemCoreLibrary.Assemblies.Count.Should().Be(1);
                        systemCoreLibrary.Assemblies[0].Should().Be(".NETFramework/v4.6/System.Core.dll");
                    }
                }
            }
        }
    }
}