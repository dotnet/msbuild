// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.IO;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Cli.Build.Tests
{
    public class GivenDotnetBuildBuildsCsproj : SdkTest
    {
        public GivenDotnetBuildBuildsCsproj(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void ItBuildsARunnableOutput()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                .WithSource();

            var buildCommand = new DotnetBuildCommand(Log, testInstance.Path);

            buildCommand
                .Execute()
                .Should().Pass();

            var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";

            var outputDll = Path.Combine(testInstance.Path, "bin", configuration, ToolsetInfo.CurrentTargetFramework, $"{testAppName}.dll");

            var outputRunCommand = new DotnetCommand(Log);

            outputRunCommand.Execute(outputDll)
                .Should().Pass()
                     .And.HaveStdOutContaining("Hello World");
        }

        [Fact]
        public void ItBuildsOnlyTheSpecifiedTarget()
        {
            var testAppName = "NonDefaultTarget";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                .WithSource();

            new DotnetBuildCommand(Log, testInstance.Path)
                .Execute("--no-restore", "--nologo", "/t:PrintMessage")
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello World");
        }

        [Fact]
        public void ItImplicitlyRestoresAProjectWhenBuilding()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                .WithSource();

            new DotnetBuildCommand(Log, testInstance.Path)
                .Execute()
                .Should().Pass();
        }

        [Fact]
        public void ItCanBuildAMultiTFMProjectWithImplicitRestore()
        {
            var testInstance = _testAssetsManager.CopyTestAsset(
                    "NETFrameworkReferenceNETStandard20",
                    testAssetSubdirectory: TestAssetSubdirectories.DesktopTestProjects)
                .WithSource();

            string projectDirectory = Path.Combine(testInstance.Path, "MultiTFMTestApp");

            new DotnetBuildCommand(Log, projectDirectory)
                .Execute("--framework", "netcoreapp3.1")
                .Should().Pass();
        }

        [Fact]
        public void ItDoesNotImplicitlyRestoreAProjectWhenBuildingWithTheNoRestoreOption()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                .WithSource();

            new DotnetBuildCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute("--no-restore")
                .Should().Fail()
                .And.HaveStdOutContaining("project.assets.json");
        }

        [Fact]
        public void ItRunsWhenRestoringToSpecificPackageDir()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("TestAppSimple")
                .WithSource();
            var rootPath = testInstance.Path;

            string dir = "pkgs";

            new DotnetRestoreCommand(Log)
                .WithWorkingDirectory(rootPath)
                .Execute("--packages", dir)
                .Should()
                .Pass()
                .And.NotHaveStdErr();

            new DotnetBuildCommand(Log)
                .WithWorkingDirectory(rootPath)
                .Execute("--no-restore")
                .Should().Pass();

            var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";

            var outputDll = Directory.EnumerateFiles(
                Path.Combine(rootPath, "bin", configuration, ToolsetInfo.CurrentTargetFramework), "*.dll",
                SearchOption.TopDirectoryOnly)
                .Single();

            var outputRunCommand = new DotnetCommand(Log);

            outputRunCommand.Execute(outputDll)
                .Should().Pass()
                     .And.HaveStdOutContaining("Hello World");
        }

        [Fact]
        public void ItPrintsBuildSummary()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                .WithSource()
                .Restore(Log);

            string expectedBuildSummary = @"Build succeeded.
    0 Warning(s)
    0 Error(s)";

            var cmd = new DotnetBuildCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute();
            cmd.Should().Pass();
            cmd.StdOut.Should().ContainVisuallySameFragmentIfNotLocalized(expectedBuildSummary);
        }

        [Fact]
        public void DotnetBuildDoesNotPrintCopyrightInfo()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("MSBuildTestApp")
                .WithSource()
                .Restore(Log);

            var cmd = new DotnetBuildCommand(Log)
               .WithWorkingDirectory(testInstance.Path)
               .Execute("--nologo");

            cmd.Should().Pass();

            if (!TestContext.IsLocalized())
            {
                cmd.Should().NotHaveStdOutContaining("Copyright (C) Microsoft Corporation. All rights reserved.");
            }
        }

        [Fact]
        public void It_warns_on_rid_without_self_contained_options()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("HelloWorld")
                .WithSource()
                .WithTargetFrameworkOrFrameworks("net6.0", false)
                .Restore(Log);

            new DotnetBuildCommand(Log)
               .WithWorkingDirectory(testInstance.Path)
               .Execute("-r", "win-x64")
               .Should()
               .Pass()
               .And
               .HaveStdOutContaining("NETSDK1179");
        }

        [Fact]
        public void It_does_not_warn_on_rid_with_self_contained_set_in_project()
        {
            var testProject = new TestProject()
            {
                IsExe = true,
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
            };
            testProject.AdditionalProperties["SelfContained"] = "true";
            
            var testInstance = _testAssetsManager.CreateTestProject(testProject);

            new DotnetBuildCommand(Log)
               .WithWorkingDirectory(Path.Combine(testInstance.Path, testProject.Name))
               .Execute("-r", "win-x64")
               .Should()
               .Pass()
               .And
               .NotHaveStdOutContaining("NETSDK1179");
        }

        [WindowsOnlyTheory]
        [InlineData("build")]
        [InlineData("run")]
        public void It_does_not_warn_on_rid_with_self_contained_options(string commandName)
        {
            var testInstance = _testAssetsManager.CopyTestAsset("HelloWorld", identifier: commandName)
                .WithSource()
                .WithTargetFrameworkOrFrameworks("net6.0", false)
                .Restore(Log);

            new DotnetCommand(Log)
               .WithWorkingDirectory(testInstance.Path)
               .Execute(commandName, "-r", "win-x64", "--self-contained")
               .Should()
               .Pass()
               .And
               .NotHaveStdOutContaining("NETSDK1179");
        }

        [Fact]
        public void It_does_not_warn_on_rid_with_self_contained_options_prior_to_net6()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("HelloWorld")
                .WithSource()
                .WithTargetFramework("netcoreapp3.1")
                .Restore(Log);

            new DotnetBuildCommand(Log)
               .WithWorkingDirectory(testInstance.Path)
               .Execute("-r", "win-x64")
               .Should()
               .Pass()
               .And
               .NotHaveStdOutContaining("NETSDK1179");
        }

        [Fact]
        public void It_builds_with_implicit_rid_with_self_contained_option()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("HelloWorld")
                .WithSource()
                .WithTargetFrameworkOrFrameworks("net6.0", false)
                .Restore(Log);

            new DotnetBuildCommand(Log)
               .WithWorkingDirectory(testInstance.Path)
               .Execute("--self-contained")
               .Should()
               .Pass()
               .And
               .NotHaveStdOutContaining("NETSDK1031");
        }

        [Theory]
        [InlineData("roslyn3.9")]
        [InlineData("roslyn4.0")]
        public void It_resolves_analyzers_targeting_mulitple_roslyn_versions(string compilerApiVersion)
        {
            var testProject = new TestProject()
            {
                TargetFrameworks = "netstandard2.0"
            };

            //  Disable analyzers built in to the SDK so we can more easily test the ones coming from NuGet packages
            testProject.AdditionalProperties["EnableNETAnalyzers"] = "false";

            testProject.ProjectChanges.Add(project =>
            {
                var itemGroup = XElement.Parse(@"
  <ItemGroup>
    <PackageReference Include=""Library.ContainsAnalyzer"" Version=""1.0.0"" />
    <PackageReference Include=""Library.ContainsAnalyzer2"" Version=""1.0.0"" />
  </ItemGroup>");

                project.Root.Add(itemGroup);
            });

            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: compilerApiVersion);

            NuGetConfigWriter.Write(testAsset.Path, TestContext.Current.TestPackages);

            var command = new GetValuesCommand(testAsset,
                "Analyzer",
                GetValuesCommand.ValueType.Item);

            // set the CompilerApiVersion through a command line property to override any value brought in by
            // the CodeAnalysis targets.
            command.Properties.Add("CompilerApiVersion", compilerApiVersion);

            command.Execute().Should().Pass();

            var analyzers = command.GetValues();

            switch (compilerApiVersion)
            {
                case "roslyn3.9":
                    analyzers.Select(RelativeNuGetPath).Should().BeEquivalentTo(
                        "library.containsanalyzer/1.0.0/analyzers/dotnet/roslyn3.9/cs/Library.ContainsAnalyzer.dll",
                        "library.containsanalyzer2/1.0.0/analyzers/dotnet/roslyn3.8/cs/Library.ContainsAnalyzer2.dll"
                        );
                    break;

                case "roslyn4.0":
                    analyzers.Select(RelativeNuGetPath).Should().BeEquivalentTo(
                        "library.containsanalyzer/1.0.0/analyzers/dotnet/roslyn4.0/cs/Library.ContainsAnalyzer.dll",
                        "library.containsanalyzer2/1.0.0/analyzers/dotnet/roslyn3.10/cs/Library.ContainsAnalyzer2.dll"
                        );
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(compilerApiVersion));
            }
        }

        static readonly List<string> nugetRoots = new List<string>()
        {
            TestContext.Current.NuGetCachePath,
            Path.Combine(FileConstants.UserProfileFolder, ".dotnet", "NuGetFallbackFolder")
        };

        static string RelativeNuGetPath(string absoluteNuGetPath)
        {
            foreach (var nugetRoot in nugetRoots)
            {
                if (absoluteNuGetPath.StartsWith(nugetRoot + Path.DirectorySeparatorChar))
                {
                    return absoluteNuGetPath.Substring(nugetRoot.Length + 1)
                                .Replace(Path.DirectorySeparatorChar, '/');
                }
            }
            throw new InvalidDataException("Expected path to be under a NuGet root: " + absoluteNuGetPath);
        }

        [Theory]
        [InlineData("build")]
        [InlineData("run")]
        public void It_uses_correct_runtime_help_description(string command)
        {
            var console = new TestConsole();
            var parseResult = Parser.Instance.Parse(new string[] { command, "-h" });
            parseResult.Invoke(console);
            console.Out.ToString().Should().Contain(command.Equals("build") ?
                Tools.Build.LocalizableStrings.RuntimeOptionDescription :
                Tools.Run.LocalizableStrings.RuntimeOptionDescription);
        }
    }
}
