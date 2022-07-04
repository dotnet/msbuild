// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;

using FluentAssertions;

using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;

using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToUsePlatformAnalyzers : SdkTest
    {
        private const string currentTargetFramework = ToolsetInfo.CurrentTargetFramework;
        private const string targetFrameworkNetCore31 = "netcoreapp3.1";

        public GivenThatWeWantToUsePlatformAnalyzers(ITestOutputHelper log) : base(log)
        {
        }

        [RequiresMSBuildVersionFact("16.8")]
        public void It_referecnes_platform_analyzers()
        {
            var testProject = new TestProject
            {
                Name = "HelloWorld",
                TargetFrameworks = currentTargetFramework,
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
                currentTargetFramework, "Analyzer")
            {
                DependsOnTargets = "Build"
            };
            var buildResult = buildCommand.Execute();
            buildResult.StdErr.Should().Be(string.Empty);
            buildResult.StdOut.Should().Contain("Program.cs(12,56): warning CA2014: Potential stack overflow. Move the stackalloc out of the loop.");
        }

        [RequiresMSBuildVersionFact("16.8")]
        public void Analysis_is_disabled_when_analysislevel_is_below_5()
        {
            var testProject = new TestProject
            {
                Name = "HelloWorld",
                TargetFrameworks = currentTargetFramework,
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

            testProject.AdditionalProperties.Add("AnalysisLevel", "4");
            var testAsset = _testAssetsManager
                .CreateTestProject(testProject, identifier: "analyzerConsoleApp", targetExtension: ".csproj");

            var buildCommand = new GetValuesCommand(
                Log,
                Path.Combine(testAsset.TestRoot, testProject.Name),
                currentTargetFramework, "Analyzer")
            {
                DependsOnTargets = "Build"
            };
            var buildResult = buildCommand.Execute();
            buildResult.StdErr.Should().Be(string.Empty);
            buildResult.StdOut.Should().NotContain("Program.cs(12,56): warning CA2014: Potential stack overflow. Move the stackalloc out of the loop.");
        }

        [RequiresMSBuildVersionFact("16.8")]
        public void Analysis_is_enabled_when_analysislevel_set_to_5()
        {
            var testProject = new TestProject
            {
                Name = "HelloWorld",
                TargetFrameworks = targetFrameworkNetCore31,
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

            testProject.AdditionalProperties.Add("AnalysisLevel", "5");
            var testAsset = _testAssetsManager
                .CreateTestProject(testProject, identifier: "analyzerConsoleApp", targetExtension: ".csproj");

            var buildCommand = new GetValuesCommand(
                Log,
                Path.Combine(testAsset.TestRoot, testProject.Name),
                targetFrameworkNetCore31, "Analyzer")
            {
                DependsOnTargets = "Build"
            };
            var buildResult = buildCommand.Execute();
            buildResult.StdErr.Should().Be(string.Empty);
            buildResult.StdOut.Should().Contain("Program.cs(12,56): warning CA2014: Potential stack overflow. Move the stackalloc out of the loop.");
        }

        [RequiresMSBuildVersionFact("16.8")]
        public void Analysis_is_disabled_when_EnableNETAnalyzers_is_set_to_false()
        {
            var testProject = new TestProject
            {
                Name = "HelloWorld",
                TargetFrameworks = currentTargetFramework,
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

            testProject.AdditionalProperties.Add("EnableNETAnalyzers", "false");
            var testAsset = _testAssetsManager
                .CreateTestProject(testProject, identifier: "analyzerConsoleApp", targetExtension: ".csproj");

            var buildCommand = new GetValuesCommand(
                Log,
                Path.Combine(testAsset.TestRoot, testProject.Name),
                currentTargetFramework, "Analyzer")
            {
                DependsOnTargets = "Build"
            };
            var buildResult = buildCommand.Execute();
            buildResult.StdErr.Should().Be(string.Empty);
            buildResult.StdOut.Should().NotContain("Program.cs(12,56): warning CA2014: Potential stack overflow. Move the stackalloc out of the loop.");
        }

        [RequiresMSBuildVersionFact("16.8")]
        public void Analysis_is_enabled_when_EnableNETAnalyzers_is_set_to_true()
        {
            var testProject = new TestProject
            {
                Name = "HelloWorld",
                TargetFrameworks = targetFrameworkNetCore31,
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

            testProject.AdditionalProperties.Add("EnableNETAnalyzers", "true");
            var testAsset = _testAssetsManager
                .CreateTestProject(testProject, identifier: "analyzerConsoleApp", targetExtension: ".csproj");

            var buildCommand = new GetValuesCommand(
                Log,
                Path.Combine(testAsset.TestRoot, testProject.Name),
                targetFrameworkNetCore31, "Analyzer")
            {
                DependsOnTargets = "Build"
            };
            var buildResult = buildCommand.Execute();
            buildResult.StdErr.Should().Be(string.Empty);
            buildResult.StdOut.Should().Contain("Program.cs(12,56): warning CA2014: Potential stack overflow. Move the stackalloc out of the loop.");
        }

        [RequiresMSBuildVersionFact("16.8")]
        public void SDK_imports_the_analyzer_props_file()
        {
            var testProject = new TestProject
            {
                Name = "HelloWorld",
                TargetFrameworks = targetFrameworkNetCore31,
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

            testProject.AdditionalProperties.Add("CodeAnalysisTreatWarningsAsErrors", "false");
            testProject.AdditionalProperties.Add("TreatWarningsAsErrors", "true");
            var testAsset = _testAssetsManager
                .CreateTestProject(testProject, identifier: "analyzerConsoleApp", targetExtension: ".csproj");

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));
            buildCommand.Execute().Should().Pass();
        }

        [RequiresMSBuildVersionFact("16.8")]
        public void Analysis_is_disabled_when_user_has_specified_AnalysisLevel_None()
        {
            var testProject = new TestProject
            {
                Name = "HelloWorld",
                TargetFrameworks = currentTargetFramework,
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

            testProject.AdditionalProperties.Add("AnalysisLevel", "none");
            var testAsset = _testAssetsManager
                .CreateTestProject(testProject, identifier: "analyzerConsoleApp", targetExtension: ".csproj");

            var buildCommand = new GetValuesCommand(
                Log,
                Path.Combine(testAsset.TestRoot, testProject.Name),
                currentTargetFramework, "Analyzer")
            {
                DependsOnTargets = "Build"
            };
            var buildResult = buildCommand.Execute();
            buildResult.StdErr.Should().Be(string.Empty);
            buildResult.StdOut.Should().NotContain("Program.cs(12,56): warning CA2014: Potential stack overflow. Move the stackalloc out of the loop.");
        }

        [RequiresMSBuildVersionFact("16.8")]
        public void Analysis_is_enabled_when_user_has_specified_AnalysisLevel_Latest()
        {
            var testProject = new TestProject
            {
                Name = "HelloWorld",
                TargetFrameworks = targetFrameworkNetCore31,
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

            testProject.AdditionalProperties.Add("AnalysisLevel", "latest");
            var testAsset = _testAssetsManager
                .CreateTestProject(testProject, identifier: "analyzerConsoleApp", targetExtension: ".csproj");

            var buildCommand = new GetValuesCommand(
                Log,
                Path.Combine(testAsset.TestRoot, testProject.Name),
                targetFrameworkNetCore31, "Analyzer")
            {
                DependsOnTargets = "Build"
            };
            var buildResult = buildCommand.Execute();
            buildResult.StdErr.Should().Be(string.Empty);
            buildResult.StdOut.Should().Contain("Program.cs(12,56): warning CA2014: Potential stack overflow. Move the stackalloc out of the loop.");
        }

        [RequiresMSBuildVersionFact("16.8")]
        public void Analysis_is_enabled_when_user_has_specified_AnalysisLevel_Preview()
        {
            var testProject = new TestProject
            {
                Name = "HelloWorld",
                TargetFrameworks = targetFrameworkNetCore31,
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

            testProject.AdditionalProperties.Add("AnalysisLevel", "latest");
            var testAsset = _testAssetsManager
                .CreateTestProject(testProject, identifier: "analyzerConsoleApp", targetExtension: ".csproj");

            var buildCommand = new GetValuesCommand(
                Log,
                Path.Combine(testAsset.TestRoot, testProject.Name),
                targetFrameworkNetCore31, "Analyzer")
            {
                DependsOnTargets = "Build"
            };
            var buildResult = buildCommand.Execute();
            buildResult.StdErr.Should().Be(string.Empty);
            buildResult.StdOut.Should().Contain("Program.cs(12,56): warning CA2014: Potential stack overflow. Move the stackalloc out of the loop.");
        }
    }
}
