// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.DotNet.Tools.Add.PackageReference;
using System;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;

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
            var packageVersion = "9.0.1";
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute("add", "package", packageName, "--version",  packageVersion);
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain($"PackageReference for package '{packageName}' version '{packageVersion}' " +
                $"added to file '{projectDirectory + Path.DirectorySeparatorChar + testAsset}.csproj'.");
            cmd.StdErr.Should().BeEmpty();
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
            var packageVersion = "9.0.1";
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute("add", csproj, "package", packageName, "--version", packageVersion);

            cmd.Should().Pass();

            cmd.StdOut.Should()
               .Contain($"PackageReference for package \'{packageName}\' version \'{packageVersion}\' added to file '{csproj}'.");

            cmd.StdErr.Should().BeEmpty();
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
            var packageVersion = "9.0.1";
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute("add", csproj, "package", packageName, "--version", packageVersion, "--package-directory", packageDirectory);

            cmd.Should().Pass();

            cmd.StdOut.Should()
               .Contain($"PackageReference for package \'{packageName}\' version \'{packageVersion}\' added to file '{csproj}'.");

            cmd.StdErr.Should().BeEmpty();

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
            var packageVersion = "9.0.1";
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"add", "package", "--version", packageVersion, packageName);
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain($"PackageReference for package '{packageName}' version '{packageVersion}' " +
                $"added to file '{projectDirectory + Path.DirectorySeparatorChar + testAsset}.csproj'.");
            cmd.StdErr.Should().BeEmpty();
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
            var packageVersion = "9.0.1";
            var framework = "netcoreapp3.0";
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"add", "package", packageName, "--version", packageVersion, "--framework", framework);
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain($"PackageReference for package '{packageName}' version '{packageVersion}' " +
                $"added to file '{projectDirectory + Path.DirectorySeparatorChar + testAsset}.csproj'.");
            cmd.StdErr.Should().BeEmpty();
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
            var packageVersion = "9.0.1";
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"add", "package", packageName, "--version", packageVersion);
            cmd.Should().Pass();
            cmd.StdOut.Should().NotContain("Microsoft (R) Build Engine version");
            cmd.StdErr.Should().BeEmpty();
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
                .Execute("add", "package", "package1", "package2", "package3");
            cmd.Should().Fail();
            cmd.StdErr.Should().Contain(LocalizableStrings.SpecifyExactlyOnePackageReference);
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
                .Execute($"add", "package");
            cmd.Should().Fail();
            cmd.StdErr.Should().Contain(LocalizableStrings.SpecifyExactlyOnePackageReference);
        }
    }
}
