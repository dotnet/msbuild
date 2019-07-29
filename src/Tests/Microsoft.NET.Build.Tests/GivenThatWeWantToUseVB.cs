// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;

using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;

using FluentAssertions;

using Xunit.Abstractions;
using Xunit;
using System;
using System.IO;
using Microsoft.NET.TestFramework.ProjectConstruction;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToUseVB : SdkTest
    {
        public GivenThatWeWantToUseVB(ITestOutputHelper log) : base(log)
        {
        }

        private enum VBRuntime
        {
            Unknown,
            Default,
            Embedded,
            Referenced
        }

        [Theory]
        [InlineData("net45")]
        [InlineData("netstandard2.0")]
        [InlineData("netcoreapp2.1")]
        [InlineData("netcoreapp3.0")]
        public void It_builds_a_simple_vb_project(string targetFramework)
        {
            if (targetFramework == "net45" && !TestProject.ReferenceAssembliesAreInstalled("v4.5"))
            {
                // skip net45 when we do not have .NET Framework 4.5 reference assemblies
                // due to https://github.com/dotnet/core-sdk/issues/3228
                return;
            }

            var (expectedVBRuntime, expectedOutputFiles) = GetExpectedOutputs(targetFramework);

            var testProject = new TestProject
            {
                Name = "HelloWorld",
                IsSdkProject = true,
                TargetFrameworks = targetFramework,
                IsExe = targetFramework != "netstandard2.0",
                AdditionalProperties =
                {
                    ["MyType"] = "Console" ,
                },
                SourceFiles =
                {
                    ["Program.vb"] = @"
                        Imports System

                        Module Program
                            Function MyComputerName() As String
                                #If NETFRAMEWORK Or NETCOREAPP3_0 Then
                                    Return My.Computer.Name
                                #End If
                            End Function

                            Sub Main(args As String())
                                Console.WriteLine(""Hello World from "" & MyComputerName())
                            End Sub
                        End Module
                        ",
                }
            };

            var testAsset = _testAssetsManager
                .CreateTestProject(testProject, identifier: targetFramework, targetExtension: ".vbproj")
                .Restore(Log, testProject.Name);

            var buildCommand = new GetValuesCommand(
                Log,
                Path.Combine(testAsset.TestRoot, testProject.Name),
                targetFramework,
                "VBRuntime")
            {
                DependsOnTargets = "Build"
            };

            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework);
            var actualVBRuntime = GetVBRuntime(buildCommand.GetValues().FirstOrDefault());
            File.Delete(outputDirectory.File("VBRuntimeValues.txt").FullName);

            outputDirectory.Should().OnlyHaveFiles(expectedOutputFiles);
            actualVBRuntime.Should().Be(expectedVBRuntime);
        }

        private static (VBRuntime, string[]) GetExpectedOutputs(string targetFramework)
        {
            switch (targetFramework)
            {
                case "net45":
                    return (VBRuntime.Default, new[]
                    {
                        "HelloWorld.exe",
                        "HelloWorld.exe.config",
                        "HelloWorld.pdb"
                    });

                case "netcoreapp2.1":
                    return (VBRuntime.Embedded, new[]
                    {
                        "HelloWorld.dll",
                        "HelloWorld.pdb",
                        "HelloWorld.runtimeconfig.json",
                        "HelloWorld.runtimeconfig.dev.json",
                        "HelloWorld.deps.json",
                    });

                case "netcoreapp3.0":
                    return (VBRuntime.Referenced, new[]
                    {
                        "HelloWorld.dll",
                        "HelloWorld.pdb",
                        "HelloWorld" + EnvironmentInfo.ExecutableExtension,
                        "HelloWorld.runtimeconfig.json",
                        "HelloWorld.runtimeconfig.dev.json",
                        "HelloWorld.deps.json",
                    });

                case "netstandard2.0":
                    return (VBRuntime.Embedded, new[]
                    {
                        "HelloWorld.dll",
                        "HelloWorld.pdb",
                        "HelloWorld.deps.json",
                    });

                default:
                    throw new ArgumentOutOfRangeException(nameof(targetFramework));
            }
        }

        private static VBRuntime GetVBRuntime(string property)
        {
            switch (property)
            {
                case null:
                    return VBRuntime.Default;

                case "Embed":
                    return VBRuntime.Embedded;

                default:
                    return Path.GetFileName(property) == "Microsoft.VisualBasic.dll"
                        ? VBRuntime.Referenced
                        : VBRuntime.Unknown;
            }
        }

        [WindowsOnlyFact]
        public void It_builds_a_vb_wpf_app()
        {
            var testDirectory = _testAssetsManager.CreateTestDirectory().Path;

            var newCommand = new DotnetCommand(Log, "new", "wpf", "-lang", "vb");
            newCommand.WorkingDirectory = testDirectory;
            newCommand.Execute().Should().Pass();

            var buildCommand = new BuildCommand(Log, testDirectory);
            buildCommand.Execute().Should().Pass();
        }
    }
}
