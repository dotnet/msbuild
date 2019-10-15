// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Cli.List.Package.Tests
{
    public class GivenDotnetListPackage : TestBase
    {
        private readonly ITestOutputHelper _output;

        public GivenDotnetListPackage(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void RequestedAndResolvedVersionsMatch()
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

            new RestoreCommand()
                .WithWorkingDirectory(projectDirectory)
                .Execute()
                .Should()
                .Pass()
                .And.NotHaveStdErr();

            new ListPackageCommand()
                .WithPath(projectDirectory)
                .Execute()
                .Should()
                .Pass()
                .And.NotHaveStdErr()
                .And.HaveStdOutContainingIgnoreSpaces(packageName+packageVersion+packageVersion);
        }

        [Fact]
        public void ItListsAutoReferencedPackages()
        {
            var testAsset = "TestAppSimple";
            var projectDirectory = TestAssets
                .Get(testAsset)
                .CreateInstance()
                .WithSourceFiles()
                .WithProjectChanges(ChangeTargetFrameworkTo2_1)
                .Root
                .FullName;

            new RestoreCommand()
                .WithWorkingDirectory(projectDirectory)
                .Execute()
                .Should()
                .Pass()
                .And.NotHaveStdErr();

            new ListPackageCommand()
                .WithPath(projectDirectory)
                .Execute()
                .Should()
                .Pass()
                .And.NotHaveStdErr()
                .And.HaveStdOutContainingIgnoreSpaces("Microsoft.NETCore.App(A)")
                .And.HaveStdOutContainingIgnoreSpaces("(A):Auto-referencedpackage");

            void ChangeTargetFrameworkTo2_1(XDocument project)
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
            var projectDirectory = TestAssets
                .Get(sln)
                .CreateInstance()
                .WithSourceFiles()
                .Root
                .FullName;

            new RestoreCommand()
                .WithWorkingDirectory(projectDirectory)
                .Execute()
                .Should()
                .Pass()
                .And.NotHaveStdErr();

            new ListPackageCommand()
                .WithPath(projectDirectory)
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
            var projectDirectory = TestAssets
                .Get(testAsset)
                .CreateInstance()
                .WithSourceFiles()
                .Root
                .FullName;

            new ListPackageCommand()
                .WithPath(projectDirectory)
                .Execute()
                .Should()
                .Pass()
                .And.HaveStdErr();
        }

        [Fact]
        public void ItListsTransitivePackage()
        {
            var testAsset = "NewtonSoftDependentProject";
            var projectDirectory = TestAssets
                .Get(testAsset)
                .CreateInstance()
                .WithSourceFiles()
                .Root
                .FullName;

            new RestoreCommand()
                .WithWorkingDirectory(projectDirectory)
                .Execute()
                .Should()
                .Pass()
                .And.NotHaveStdErr();

            new ListPackageCommand()
                .WithPath(projectDirectory)
                .Execute()
                .Should()
                .Pass()
                .And.NotHaveStdErr()
                .And.NotHaveStdOutContaining("System.IO.FileSystem");

            new ListPackageCommand()
                .WithPath(projectDirectory)
                .Execute(args:"--include-transitive")
                .Should()
                .Pass()
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("System.IO.FileSystem");
        }

        [Theory]
        [InlineData("", "[net451]", null)]
        [InlineData("", "[netcoreapp3.0]", null)]
        [InlineData("--framework netcoreapp3.0 --framework net451", "[net451]", null)]
        [InlineData("--framework netcoreapp3.0 --framework net451", "[netcoreapp3.0]", null)]
        [InlineData("--framework netcoreapp3.0", "[netcoreapp3.0]", "[net451]")]
        [InlineData("--framework net451", "[net451]", "[netcoreapp3.0]")]
        public void ItListsValidFrameworks(string args, string shouldInclude, string shouldntInclude)
        {
            var testAsset = "MSBuildAppWithMultipleFrameworks";
            var projectDirectory = TestAssets
                .Get(testAsset)
                .CreateInstance()
                .WithSourceFiles()
                .Root
                .FullName;

            new RestoreCommand()
                .WithWorkingDirectory(projectDirectory)
                .Execute()
                .Should()
                .Pass()
                .And.NotHaveStdErr();

            if (shouldntInclude == null)
            {
                new ListPackageCommand()
                    .WithPath(projectDirectory)
                    .Execute(args)
                    .Should()
                    .Pass()
                    .And.NotHaveStdErr()
                    .And.HaveStdOutContainingIgnoreSpaces(shouldInclude.Replace(" ", ""));
            }
            else
            {
                new ListPackageCommand()
                    .WithPath(projectDirectory)
                    .Execute(args)
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
            var testAsset = "MSBuildAppWithMultipleFrameworks";
            var projectDirectory = TestAssets
                .Get(testAsset)
                .CreateInstance()
                .WithSourceFiles()
                .Root
                .FullName;

            new RestoreCommand()
                .WithWorkingDirectory(projectDirectory)
                .Execute()
                .Should()
                .Pass();

            new ListPackageCommand()
                .WithPath(projectDirectory)
                .Execute("--framework invalid")
                .Should()
                .Fail();
        }

        [Fact]
        public void ItListsFSharpProject()
        {
            var testAsset = "FSharpTestAppSimple";
            var projectDirectory = TestAssets
                .Get(testAsset)
                .CreateInstance()
                .WithSourceFiles()
                .Root
                .FullName;

            new RestoreCommand()
                .WithWorkingDirectory(projectDirectory)
                .Execute()
                .Should()
                .Pass()
                .And.NotHaveStdErr();

            new ListPackageCommand()
                .WithPath(projectDirectory)
                .Execute()
                .Should()
                .Pass()
                .And.NotHaveStdErr();
        }

    }
}
