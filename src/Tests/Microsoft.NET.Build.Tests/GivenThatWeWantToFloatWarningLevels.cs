// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;

using FluentAssertions;

using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;

using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToFloatWarningLevels : SdkTest
    {
        private const string targetFrameworkNet6 = "net6.0";
        private const string targetFrameworkNet7 = "net7.0";
        private const string targetFrameworkNetFramework472 = "net472";

        public GivenThatWeWantToFloatWarningLevels(ITestOutputHelper log) : base(log)
        {
        }

        [InlineData(targetFrameworkNet6, 6)]
        [InlineData(targetFrameworkNet7, 7)]
        [InlineData(targetFrameworkNetFramework472, 4)]
        [RequiresMSBuildVersionTheory("16.8")]
        public void It_defaults_WarningLevel_To_The_Current_TFM_When_Net(string tfm, int warningLevel)
        {
            var testProject = new TestProject
            {
                Name = "HelloWorld",
                TargetFrameworks = tfm,
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
                                }
                            }
                        }
                    ",
                }
            };

            var testAsset = _testAssetsManager
                .CreateTestProject(testProject, identifier: "warningLevelConsoleApp", targetExtension: ".csproj");

            var buildCommand = new GetValuesCommand(
                Log,
                Path.Combine(testAsset.TestRoot, testProject.Name),
                tfm, "WarningLevel")
            {
                DependsOnTargets = "Build"
            };
            var buildResult = buildCommand.Execute();
            var computedWarningLevel = buildCommand.GetValues()[0];
            buildResult.StdErr.Should().Be(string.Empty);
            computedWarningLevel.Should().Be(warningLevel.ToString());
        }

        [InlineData(1, 1)]
        [InlineData(null, 7)]
        [RequiresMSBuildVersionTheory("16.8")]
        public void It_always_accepts_user_defined_WarningLevel(int? warningLevel, int expectedWarningLevel)
        {
            var testProject = new TestProject
            {
                Name = "HelloWorld",
                TargetFrameworks = "net7.0",
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
                                }
                            }
                        }
                    ",
                }
            };
            testProject.AdditionalProperties.Add("WarningLevel", warningLevel?.ToString());
            var testAsset = _testAssetsManager
                .CreateTestProject(testProject, identifier: "customWarningLevelConsoleApp", targetExtension: ".csproj");

            var buildCommand = new GetValuesCommand(
                Log,
                Path.Combine(testAsset.TestRoot, testProject.Name),
                "net7.0", "WarningLevel")
            {
                DependsOnTargets = "Build"
            };
            var buildResult = buildCommand.Execute();
            var computedWarningLevel = buildCommand.GetValues()[0];
            buildResult.StdErr.Should().Be(string.Empty);
            computedWarningLevel.Should().Be(expectedWarningLevel.ToString());
        }

    }
}
