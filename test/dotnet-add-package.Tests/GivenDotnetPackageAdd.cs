// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.Build.Construction;
using Microsoft.DotNet.Cli.Sln.Internal;
using Microsoft.DotNet.Tools.Test.Utilities;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Microsoft.DotNet.Cli.Package.Add.Tests
{
    public class GivenDotnetPackageAdd : TestBase
    {

        [Fact]
        public void WhenValidPackageIsPassedBeforeVersionItGetsAdded()
        {
            var testAsset = "TestAppSimple";
            var projectDirectory = TestAssets
                .Get(testAsset)
                .CreateInstance()
                .WithSourceFiles()
                .Root
                .FullName;

            var packageName = "Newtonsoft.Json";
            var packageVersion = "9.0.1";
            var cmd = new DotnetCommand()
                .WithWorkingDirectory(projectDirectory)
                .ExecuteWithCapturedOutput($"add package {packageName} --version {packageVersion}");
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain($"PackageReference for package '{packageName}' version '{packageVersion}' " +
                $"added to file '{projectDirectory + Path.DirectorySeparatorChar + testAsset}.csproj'.");
            cmd.StdErr.Should().BeEmpty();
        }

        [Fact]
        public void WhenValidPackageIsPassedAfterVersionItGetsAdded()
        {
            var testAsset = "TestAppSimple";
            var projectDirectory = TestAssets
                .Get(testAsset)
                .CreateInstance()
                .WithSourceFiles()
                .Root
                .FullName;

            var packageName = "Newtonsoft.Json";
            var packageVersion = "9.0.1";
            var cmd = new DotnetCommand()
                .WithWorkingDirectory(projectDirectory)
                .ExecuteWithCapturedOutput($"add package --version {packageVersion} {packageName}");
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain($"PackageReference for package '{packageName}' version '{packageVersion}' " +
                $"added to file '{projectDirectory + Path.DirectorySeparatorChar + testAsset}.csproj'.");
            cmd.StdErr.Should().BeEmpty();
        }

        [Fact]
        public void WhenValidPackageIsPassedWithFrameworkItGetsAdded()
        {
            var testAsset = "TestAppSimple";
            var projectDirectory = TestAssets
                .Get(testAsset)
                .CreateInstance()
                .WithSourceFiles()
                .Root
                .FullName;

            var packageName = "Newtonsoft.Json";
            var packageVersion = "9.0.1";
            var framework = "netcoreapp1.0";
            var cmd = new DotnetCommand()
                .WithWorkingDirectory(projectDirectory)
                .ExecuteWithCapturedOutput($"add package {packageName} --version {packageVersion} --framework {framework}");
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain($"PackageReference for package '{packageName}' version '{packageVersion}' " +
                $"added to file '{projectDirectory + Path.DirectorySeparatorChar + testAsset}.csproj'.");
            cmd.StdErr.Should().BeEmpty();
        }

        [Fact]
        public void WhenMultiplePackagesArePassedCommandFails()
        {
            var projectDirectory = TestAssets
                .Get("TestAppSimple")
                .CreateInstance()
                .WithSourceFiles()
                .Root
                .FullName;

            var cmd = new DotnetCommand()
                .WithWorkingDirectory(projectDirectory)
                .ExecuteWithCapturedOutput($"add package package1 package2 package3");
            cmd.Should().Fail();
            cmd.StdErr.Should().Contain("Please specify one package reference to add.");
        }

        [Fact]
        public void WhenNoPackageisPassedCommandFails()
        {
            var projectDirectory = TestAssets
                .Get("TestAppSimple")
                .CreateInstance()
                .WithSourceFiles()
                .Root
                .FullName;

            var cmd = new DotnetCommand()
                .WithWorkingDirectory(projectDirectory)
                .ExecuteWithCapturedOutput($"add package");
            cmd.Should().Fail();
            cmd.StdErr.Should().Contain("Please specify one package reference to add.");
        }
    }
}
