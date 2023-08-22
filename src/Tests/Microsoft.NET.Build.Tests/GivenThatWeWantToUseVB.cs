// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Utilities;
using Microsoft.DotNet.Cli.Utils;

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
        [InlineData("net472", true)]
        [InlineData("netstandard2.0", false)]
        [InlineData("netcoreapp2.1", true)]
        [InlineData("netcoreapp3.0", true)]
        [InlineData("netcoreapp3.0", false)]
        public void It_builds_a_simple_vb_project(string targetFramework, bool isExe)
        {
            var (expectedVBRuntime, expectedOutputFiles) = GetExpectedOutputs(targetFramework, isExe);

            var testProject = new TestProject
            {
                Name = "HelloWorld",
                TargetFrameworks = targetFramework,
                IsExe = isExe,
                SourceFiles =
                {
                    ["Program.vb"] = @"
                        Imports System

                        Module Program
                            #If NETFRAMEWORK Or NETCOREAPP3_0
                                ' https://github.com/dotnet/sdk/issues/2793
                                Private Const TabChar As Char = Chr(9)
                            #End If

                            Function MyComputerName() As String
                                #If NETFRAMEWORK
                                    Return My.Computer.Name
                                #End If

                                #If NETFRAMEWORK Or NETCOREAPP_3_0
                                    ' https://github.com/dotnet/sdk/issues/3379
                                    End
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
                .CreateTestProject(testProject, identifier: targetFramework + isExe, targetExtension: ".vbproj");

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

        private static (VBRuntime, string[]) GetExpectedOutputs(string targetFramework, bool isExe)
        {
            switch ((targetFramework, isExe))
            {
                case ("net472", true):
                    var files = new[]
                        {
                            "HelloWorld.exe",
                            "HelloWorld.exe.config",
                            "HelloWorld.pdb"
                        };
                    if (TestProject.ReferenceAssembliesAreInstalled(TargetDotNetFrameworkVersion.Version471))
                    {
                        return (VBRuntime.Default, files);
                    }
                    else
                    {
                        return (VBRuntime.Referenced, files);
                    }

                case ("netcoreapp2.1", true):
                    return (VBRuntime.Embedded, new[]
                    {
                        "HelloWorld.dll",
                        "HelloWorld.pdb",
                        "HelloWorld.runtimeconfig.json",
                        "HelloWorld.runtimeconfig.dev.json",
                        "HelloWorld.deps.json",
                    });

                case ("netcoreapp3.0", true):
                    return (VBRuntime.Referenced, new[]
                    {
                        $"HelloWorld{Constants.ExeSuffix}",
                        "HelloWorld.dll",
                        "HelloWorld.pdb",
                        "HelloWorld.runtimeconfig.json",
                        "HelloWorld.runtimeconfig.dev.json",
                        "HelloWorld.deps.json",
                    });

                case ("netcoreapp3.0", false):
                    return (VBRuntime.Referenced, new[]
                    {
                        "HelloWorld.dll",
                        "HelloWorld.pdb",
                        "HelloWorld.deps.json",
                    });

                case ("netstandard2.0", false):
                    return (VBRuntime.Embedded, new[]
                    {
                        "HelloWorld.dll",
                        "HelloWorld.pdb",
                        "HelloWorld.deps.json",
                    });

                default:
                    throw new ArgumentOutOfRangeException();
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

            new DotnetNewCommand(Log, "wpf", "-lang", "vb")
                .WithVirtualHive()
                .WithWorkingDirectory(testDirectory)
                .Execute()
                .Should()
                .Pass();

            var buildCommand = new BuildCommand(Log, testDirectory);
            buildCommand.Execute().Should().Pass();
        }
    }
}
