// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.List.PackageReferences;

namespace Microsoft.DotNet.Cli.List.Package.Tests
{
    public class GivenDotnetListPackage : SdkTest
    {
        public GivenDotnetListPackage(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void ItShowsCoreOutputOnMinimalVerbosity()
        {
            var testAssetName = "NewtonSoftDependentProject";
            var testAsset = _testAssetsManager
                .CopyTestAsset(testAssetName)
                .WithSource();
            var projectDirectory = testAsset.Path;

            new RestoreCommand(testAsset)
                .Execute()
                .Should()
                .Pass()
                .And.NotHaveStdErr();

            new ListPackageCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute("--verbosity", "quiet")
                .Should()
                .Pass()
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("NewtonSoft.Json");
        }

        [Fact]
        public void RequestedAndResolvedVersionsMatch()
        {
            var testAssetName = "TestAppSimple";
            var testAsset = _testAssetsManager
                .CopyTestAsset(testAssetName)
                .WithSource();

            var projectDirectory = testAsset.Path;

            var packageName = "Newtonsoft.Json";
            var packageVersion = ToolsetInfo.GetNewtonsoftJsonPackageVersion();
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute("add", "package", packageName, "--version", packageVersion);
            cmd.Should().Pass();

            new RestoreCommand(testAsset)
                .Execute()
                .Should()
                .Pass()
                .And.NotHaveStdErr();

            new ListPackageCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute()
                .Should()
                .Pass()
                .And.NotHaveStdErr()
                .And.HaveStdOutContainingIgnoreSpaces(packageName + packageVersion + packageVersion);
        }

        [Fact]
        public void ItListsAutoReferencedPackages()
        {
            var testAssetName = "TestAppSimple";
            var testAsset = _testAssetsManager
                .CopyTestAsset(testAssetName)
                .WithSource()
                .WithProjectChanges(ChangeTargetFrameworkTo2_1);
            var projectDirectory = testAsset.Path;

            new RestoreCommand(testAsset)
                .Execute()
                .Should()
                .Pass()
                .And.NotHaveStdErr();

            new ListPackageCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute()
                .Should()
                .Pass()
                .And.NotHaveStdErr()
                .And.HaveStdOutContainingIgnoreSpaces("Microsoft.NETCore.App(A)")
                .And.HaveStdOutContainingIgnoreSpaces("(A):Auto-referencedpackage");

            static void ChangeTargetFrameworkTo2_1(XDocument project)
            {
                project.Descendants()
                       .Single(e => e.Name.LocalName == "TargetFramework")
                       .Value = "netcoreapp2.1";
            }
        }

        [Fact]
        public void ItRunOnSolution()
        {
            var sln = "TestAppWithSlnAndSolutionFolders";
            var testAsset = _testAssetsManager
                .CopyTestAsset(sln)
                .WithSource();
            var projectDirectory = testAsset.Path;

            new RestoreCommand(testAsset, "App.sln")
                .Execute()
                .Should()
                .Pass()
                .And.NotHaveStdErr();

            new ListPackageCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute()
                .Should()
                .Pass()
                .And.NotHaveStdErr()
                .And.HaveStdOutContainingIgnoreSpaces("NewtonSoft.Json");
        }

        [Fact]
        public void AssetsPathExistsButNotRestored()
        {
            var testAsset = "NewtonSoftDependentProject";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource()
                .Path;

            new ListPackageCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute()
                .Should()
                .Fail()
                .And.HaveStdErr();
        }

        [Fact]
        public void ItListsTransitivePackage()
        {
            var testProject = new TestProject
            {
                Name = "NewtonSoftDependentProject",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
                SourceFiles =
                {
["Program.cs"] = @"
using System;
using System.Collections;
using Newtonsoft.Json.Linq;

class Program
{
    public static void Main(string[] args)
    {
        ArrayList argList = new ArrayList(args);
        JObject jObject = new JObject();

        foreach (string arg in argList)
        {
            jObject[arg] = arg;
        }
        Console.WriteLine(jObject.ToString());
    }
}
",
                }
            };

            testProject.PackageReferences.Add(new TestPackageReference("NewtonSoft.Json", "9.0.1"));
            var testAsset = _testAssetsManager.CreateTestProject(testProject);
            var projectDirectory = Path.Combine(testAsset.Path, testProject.Name);

            new RestoreCommand(testAsset)
                .Execute()
                .Should()
                .Pass()
                .And.NotHaveStdErr();

            new ListPackageCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute()
                .Should()
                .Pass()
                .And.NotHaveStdErr()
                .And.NotHaveStdOutContaining("System.IO.FileSystem");

            new ListPackageCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(args: "--include-transitive")
                .Should()
                .Pass()
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("System.IO.FileSystem");
        }

        [Theory]
        [InlineData("", "[net451]", null)]
        [InlineData("", $"[{ToolsetInfo.CurrentTargetFramework}]", null)]
        [InlineData($"--framework {ToolsetInfo.CurrentTargetFramework} --framework net451", "[net451]", null)]
        [InlineData($"--framework {ToolsetInfo.CurrentTargetFramework} --framework net451", $"[{ToolsetInfo.CurrentTargetFramework}]", null)]
        [InlineData($"--framework {ToolsetInfo.CurrentTargetFramework}", $"[{ToolsetInfo.CurrentTargetFramework}]", "[net451]")]
        [InlineData("--framework net451", "[net451]", "[netcoreapp3.0]")]
        public void ItListsValidFrameworks(string args, string shouldInclude, string shouldntInclude)
        {
            var testAssetName = "MSBuildAppWithMultipleFrameworks";
            var testAsset = _testAssetsManager
                .CopyTestAsset(testAssetName, identifier: args.GetHashCode().ToString() + shouldInclude)
                .WithSource();
            var projectDirectory = testAsset.Path;

            new RestoreCommand(testAsset)
                .Execute()
                .Should()
                .Pass()
                .And.NotHaveStdErr();

            if (shouldntInclude == null)
            {
                new ListPackageCommand(Log)
                    .WithWorkingDirectory(projectDirectory)
                    .Execute(args.Split(' ', options: StringSplitOptions.RemoveEmptyEntries))
                    .Should()
                    .Pass()
                    .And.NotHaveStdErr()
                    .And.HaveStdOutContainingIgnoreSpaces(shouldInclude.Replace(" ", ""));
            }
            else
            {
                new ListPackageCommand(Log)
                    .WithWorkingDirectory(projectDirectory)
                    .Execute(args.Split(' ', options: StringSplitOptions.RemoveEmptyEntries))
                    .Should()
                    .Pass()
                    .And.NotHaveStdErr()
                    .And.HaveStdOutContainingIgnoreSpaces(shouldInclude.Replace(" ", ""))
                    .And.NotHaveStdOutContaining(shouldntInclude.Replace(" ", ""));
            }

        }

        [Fact]
        public void ItDoesNotAcceptInvalidFramework()
        {
            var testAssetName = "MSBuildAppWithMultipleFrameworks";
            var testAsset = _testAssetsManager
                .CopyTestAsset(testAssetName)
                .WithSource();
            var projectDirectory = testAsset.Path;

            new RestoreCommand(testAsset)
                .Execute()
                .Should()
                .Pass();

            new ListPackageCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute("--framework", "invalid")
                .Should()
                .Fail();
        }

        [FullMSBuildOnlyFact]
        public void ItListsFSharpProject()
        {
            var testAssetName = "FSharpTestAppSimple";
            var testAsset = _testAssetsManager
                .CopyTestAsset(testAssetName)
                .WithSource();
            var projectDirectory = testAsset.Path;

            new RestoreCommand(testAsset)
                .Execute()
                .Should()
                .Pass()
                .And.NotHaveStdErr();

            new ListPackageCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute()
                .Should()
                .Pass()
                .And.NotHaveStdErr();
        }

        [Theory]
        [InlineData(false, "--vulnerable")]
        [InlineData(false, "--vulnerable", "--include-transitive")]
        [InlineData(false, "--vulnerable", "--include-prerelease")]
        [InlineData(false, "--deprecated", "--highest-minor")]
        [InlineData(false, "--deprecated", "--highest-patch")]
        [InlineData(false, "--outdated", "--include-prerelease")]
        [InlineData(false, "--outdated", "--highest-minor")]
        [InlineData(false, "--outdated", "--highest-patch")]
        [InlineData(false, "--config")]
        [InlineData(false, "--configfile")]
        [InlineData(false, "--source")]
        [InlineData(false, "--config", "--deprecated")]
        [InlineData(false, "--configfile", "--deprecated")]
        [InlineData(false, "--source", "--vulnerable")]
        [InlineData(true, "--vulnerable", "--deprecated")]
        [InlineData(true, "--vulnerable", "--outdated")]
        [InlineData(true, "--deprecated", "--outdated")]
        public void ItEnforcesOptionRules(bool throws, params string[] options)
        {
            var parseResult = Parser.Instance.Parse($"dotnet list package {string.Join(' ', options)}");
            Action checkRules = () => ListPackageReferencesCommand.EnforceOptionRules(parseResult);

            if (throws)
            {
                Assert.Throws<GracefulException>(checkRules);
            }
            else
            {
                checkRules(); // Test for no throw
            }
        }

        [UnixOnlyFact]
        public void ItRunsInCurrentDirectoryWithPoundInPath()
        {
            // Regression test for https://github.com/dotnet/sdk/issues/19654
            var testAssetName = "TestAppSimple";
            var testAsset = _testAssetsManager
                .CopyTestAsset(testAssetName, "C#")
                .WithSource();
            var projectDirectory = testAsset.Path;

            new RestoreCommand(testAsset)
                .Execute()
                .Should()
                .Pass();

            new ListPackageCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute()
                .Should()
                .Pass();
        }
    }
}
