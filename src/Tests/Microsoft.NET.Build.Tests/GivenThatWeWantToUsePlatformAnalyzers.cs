// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;

using FluentAssertions;

using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;

using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToUsePlatformAnalyzers : SdkTest
    {
        private const string targetFramework = "net5.0";

        public GivenThatWeWantToUsePlatformAnalyzers(ITestOutputHelper log) : base(log)
        {
        }

        [RequiresMSBuildVersionFact("16.8")]
        public void It_referecnes_platform_analyzers()
        {
            var testProject = new TestProject
            {
                Name = "HelloWorld",
                IsSdkProject = true,
                TargetFrameworks = targetFramework,
                IsExe = true,
                SourceFiles =
                {
                    ["Program.cs"] = @"
                        using System;

                        namespace ConsoleCore
                        {
                            class Program
                            {
                                static void Main()
                                {
                                    while (true)
                                    {
                                        Span<char> c = stackalloc char[5];
                                    }
                                }
                            }
                        }
                    ",
                }
            };

            var testAsset = _testAssetsManager
                .CreateTestProject(testProject, identifier: "analyzerConsoleApp", targetExtension: ".csproj");

            var buildCommand = new GetValuesCommand(
                Log,
                Path.Combine(testAsset.TestRoot, testProject.Name),
                targetFramework, "Analyzer")
            {
                DependsOnTargets = "Build"
            };
            var buildResult = buildCommand.Execute();
            buildResult.StdErr.Should().Be(string.Empty);
            buildResult.StdOut.Should().Contain("Program.cs(12,56): warning CA2014: Potential stack overflow. Move the stackalloc out of the loop.");
        }
    }
}
