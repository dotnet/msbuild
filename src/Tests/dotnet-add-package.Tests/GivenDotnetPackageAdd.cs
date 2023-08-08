// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Tools.Add.PackageReference;
using System.IO;
using Xunit;
using Xunit.Abstractions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Microsoft.DotNet.Cli.Package.Add.Tests
{
    public class GivenDotnetPackageAdd : SdkTest
    {
        public GivenDotnetPackageAdd(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void WhenValidPackageIsPassedBeforeVersionItGetsAdded()
        {
            var testAsset = "TestAppSimple";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource()
                .Path;

            var packageName = "Newtonsoft.Json";
            var packageVersion = ToolsetInfo.GetNewtonsoftJsonPackageVersion();
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute("add", "package", packageName, "--version",  packageVersion);
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain($"PackageReference for package '{packageName}' version '{packageVersion}' " +
                $"added to file '{projectDirectory + Path.DirectorySeparatorChar + testAsset}.csproj'.");
            cmd.StdErr.Should().BeEmpty();
        }

        public static readonly List<object[]> AddPkg_PackageVersionsLatestPrereleaseSucessData
            = new List<object[]>
            {
                    new object[] { new string[] { "0.0.5", "0.9.0", "1.0.0-preview.3" }, "1.0.0-preview.3" },
                    new object[] { new string[] { "0.0.5", "0.9.0", "1.0.0-preview.3", "1.1.1-preview.7" }, "1.1.1-preview.7" },
                    new object[] { new string[] { "0.0.5", "0.9.0", "1.0.0" }, "1.0.0" },
                    new object[] { new string[] { "0.0.5", "0.9.0", "1.0.0-preview.3", "2.0.0" }, "2.0.0" },
                    new object[] { new string[] { "1.0.0-preview.1", "1.0.0-preview.2", "1.0.0-preview.3" }, "1.0.0-preview.3" },
            };

        [Theory]
        [MemberData(nameof(AddPkg_PackageVersionsLatestPrereleaseSucessData))]
        public void WhenPrereleaseOptionIsPassed(string[] inputVersions, string expectedVersion)
        {
            var targetFramework = ToolsetInfo.CurrentTargetFramework;
            TestProject testProject = new TestProject()
            {
                Name = "Project",
                IsExe = false,
                TargetFrameworks = targetFramework,
            };

            var packages = inputVersions.Select(e => GetPackagePath(targetFramework, "A", e, identifier: expectedVersion + e +  inputVersions.GetHashCode().ToString())).ToArray(); 

            testProject.AdditionalProperties.Add("RestoreSources",
                                     "$(RestoreSources);" + string.Join(";", packages.Select(package => Path.GetDirectoryName(package))));

            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: inputVersions.GetHashCode().ToString());

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(Path.Combine(testAsset.TestRoot, testProject.Name))
                .Execute("add", "package", "--prerelease", "A")
                .Should()
                .Pass()
                .And.HaveStdOutContaining($"PackageReference for package 'A' version '{expectedVersion}' ")
                .And.NotHaveStdErr();
        }

        [Fact]
        public void WhenPrereleaseAndVersionOptionIsPassedFails()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppSimple")
                .WithSource()
                .Path;

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"add", "package", "--prerelease", "Newtonsoft.Json", "--version", ToolsetInfo.GetNewtonsoftJsonPackageVersion())
                .Should().Fail()
                .And.HaveStdOutContaining("The --prerelease and --version options are not supported in the same command.");
        }

        [Fact]
        public void 
            WhenValidProjectAndPackageArePassedItGetsAdded()
        {
            var testAsset = "TestAppSimple";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource()
                .Path;

            var csproj = $"{projectDirectory + Path.DirectorySeparatorChar + testAsset}.csproj";
            var packageName = "Newtonsoft.Json";
            var packageVersion = ToolsetInfo.GetNewtonsoftJsonPackageVersion();
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute("add", csproj, "package", packageName, "--version", packageVersion)
                .Should()
                .Pass()
                .And.HaveStdOutContaining($"PackageReference for package \'{packageName}\' version \'{packageVersion}\' added to file '{csproj}'.")
                .And.NotHaveStdErr();
        }

        [Fact]
        public void
            WhenValidProjectAndPackageWithPackageDirectoryContainingSpaceArePassedItGetsAdded()
        {
            var testAsset = "TestAppSimple";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource()
                .Path;

            var packageDirectory = Path.Combine(projectDirectory, "local packages"); 

            var csproj = $"{projectDirectory + Path.DirectorySeparatorChar + testAsset}.csproj";
            var packageName = "Newtonsoft.Json";
            var packageVersion = ToolsetInfo.GetNewtonsoftJsonPackageVersion();
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute("add", csproj, "package", packageName, "--version", packageVersion, "--package-directory", packageDirectory)
                .Should()
                .Pass()
                .And.HaveStdOutContaining($"PackageReference for package \'{packageName}\' version \'{packageVersion}\' added to file '{csproj}'.")
                .And.NotHaveStdErr();

            var restoredPackageDirectory = Path.Combine(packageDirectory, packageName.ToLowerInvariant(), packageVersion);
            var packageDirectoryExists = Directory.Exists(restoredPackageDirectory);
            Assert.True(packageDirectoryExists);
        }

        [Fact]
        public void WhenValidPackageIsPassedAfterVersionItGetsAdded()
        {
            var testAsset = "TestAppSimple";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource()
                .Path;

            var packageName = "Newtonsoft.Json";
            var packageVersion = ToolsetInfo.GetNewtonsoftJsonPackageVersion();
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"add", "package", "--version", packageVersion, packageName)
                .Should()
                .Pass()
                .And.HaveStdOutContaining($"PackageReference for package '{packageName}' version '{packageVersion}' " +
                $"added to file '{projectDirectory + Path.DirectorySeparatorChar + testAsset}.csproj'.")
                .And.NotHaveStdErr();
        }

        [Fact]
        public void WhenValidPackageIsPassedWithFrameworkItGetsAdded()
        {
            var testAsset = "TestAppSimple";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource()
                .Path;

            var packageName = "Newtonsoft.Json";
            var packageVersion = ToolsetInfo.GetNewtonsoftJsonPackageVersion();
            var framework = ToolsetInfo.CurrentTargetFramework;
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"add", "package", packageName, "--version", packageVersion, "--framework", framework)
                .Should()
                .Pass()
                .And.HaveStdOutContaining($"PackageReference for package '{packageName}' version '{packageVersion}' " +
                $"added to file '{projectDirectory + Path.DirectorySeparatorChar + testAsset}.csproj'.")
                .And.NotHaveStdErr();
        }

        [Fact]
        public void WhenValidPackageIsPassedMSBuildDoesNotPrintVersionHeader()
        {
            var testAsset = "TestAppSimple";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource()
                .Path;

            var packageName = "Newtonsoft.Json";
            var packageVersion = ToolsetInfo.GetNewtonsoftJsonPackageVersion();
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"add", "package", packageName, "--version", packageVersion)
                .Should()
                .Pass()
                .And.NotHaveStdOutContaining("Microsoft (R) Build Engine version")
                .And.NotHaveStdErr();
        }

        [Fact]
        public void WhenMultiplePackagesArePassedCommandFails()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppSimple")
                .WithSource()
                .Path;

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute("add", "package", "package1", "package2", "package3")
                .Should()
                .Fail();
        }

        [Fact]
        public void WhenNoPackageisPassedCommandFails()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppSimple")
                .WithSource()
                .Path;

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"add", "package")
                .Should()
                .Fail();
        }


        private static TestProject GetProject(string targetFramework, string referenceProjectName, string version)
        {
            var project = new TestProject()
            {
                Name = referenceProjectName,
                TargetFrameworks = targetFramework,
            };
            project.AdditionalProperties.Add("Version", version);
            return project;
        }

        private string GetPackagePath(string targetFramework, string packageName, string version, [CallerMemberName] string callingMethod = "", string identifier = null)
        {
            var project = GetProject(targetFramework, packageName, version);
            var packCommand = new PackCommand(Log, _testAssetsManager.CreateTestProject(project, callingMethod: callingMethod, identifier: identifier).TestRoot, packageName);

            packCommand
                .Execute()
                .Should()
                .Pass();
            return packCommand.GetNuGetPackage(packageName, packageVersion: version);
        }
    }
}

