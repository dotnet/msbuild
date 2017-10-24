// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
#pragma warning disable xUnit1004 // Test methods should not be skipped

    public class GivenThatWeWantToTargetNet471 : SdkTest
    {
        public GivenThatWeWantToTargetNet471(ITestOutputHelper log) : base(log)
        {
        }

        [WindowsOnlyFact(Skip = "https://github.com/dotnet/sdk/issues/1625")]
        public void It_builds_a_net471_app()
        {
            var testProject = new TestProject()
            {
                Name = "Net471App",
                TargetFrameworks = "net471",
                IsSdkProject = true,
                IsExe = true
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .Restore(Log, testProject.Name);

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            buildCommand
                .Execute("/v:normal")
                .Should()
                .Pass()
                .And.NotHaveStdOutContaining("MSB3277") // MSB3277: Found conflicts between different versions of the same dependent assembly that could not be resolved.
                .And.NotHaveStdOutContaining("Could not determine");

            var outputDirectory = buildCommand.GetOutputDirectory(testProject.TargetFrameworks);

            outputDirectory.Should().OnlyHaveFiles(new[] {
                $"{testProject.Name}.exe",
                $"{testProject.Name}.pdb",
            });
        }

        [WindowsOnlyFact(Skip = "https://github.com/dotnet/sdk/issues/1625")]
        public void It_builds_a_net471_app_referencing_netstandard20()
        {
            var testProject = new TestProject()
            {
                Name = "Net471App_Referencing_NetStandard20",
                TargetFrameworks = "net471",
                IsSdkProject = true,
                IsExe = true
            };

            var netStandardProject = new TestProject()
            {
                Name="NetStandard20_Library",
                TargetFrameworks = "netstandard2.0",
                IsSdkProject = true
            };

            testProject.ReferencedProjects.Add(netStandardProject);

            var testAsset = _testAssetsManager.CreateTestProject(testProject, "net471_ref_ns20")
                .Restore(Log, testProject.Name);

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            buildCommand
                .Execute("/v:normal")
                .Should()
                .Pass()
                .And.NotHaveStdOutContaining("MSB3277") // MSB3277: Found conflicts between different versions of the same dependent assembly that could not be resolved.
                .And.NotHaveStdOutContaining("Could not determine");

            var outputDirectory = buildCommand.GetOutputDirectory(testProject.TargetFrameworks);

            outputDirectory.Should().OnlyHaveFiles(new[] {
                $"{testProject.Name}.exe",
                $"{testProject.Name}.pdb",
                $"{netStandardProject.Name}.dll",
                $"{netStandardProject.Name}.pdb",
            });
        }

        [WindowsOnlyFact(Skip = "https://github.com/dotnet/sdk/issues/1625")]
        public void It_does_not_include_facades_from_nuget_packages()
        {
            var testProject = new TestProject()
            {
                Name = "Net471_NuGetFacades",
                TargetFrameworks = "net471",
                IsSdkProject = true,
                IsExe = true
            };

            testProject.PackageReferences.Add(new TestPackageReference("NETStandard.Library", "1.6.1"));

            var testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name)
                .Restore(Log, testProject.Name);

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            buildCommand
                .Execute("/v:normal")
                .Should()
                .Pass()
                .And.NotHaveStdOutContaining("MSB3277") // MSB3277: Found conflicts between different versions of the same dependent assembly that could not be resolved.
                .And.NotHaveStdOutContaining("Could not determine");

            var outputDirectory = buildCommand.GetOutputDirectory(testProject.TargetFrameworks);

            outputDirectory.Should().OnlyHaveFiles(new[] {
                $"{testProject.Name}.exe",
                $"{testProject.Name}.pdb",

                //  Remove these two once https://github.com/dotnet/sdk/issues/1647 is fixed
                "System.Net.Http.dll",
                "System.IO.Compression.dll",

                //  This is an implementation dependency of the System.Net.Http package, which won't get conflict resolved out
                "System.Diagnostics.DiagnosticSource.dll",
            });
        }
    }
}
