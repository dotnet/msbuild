// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using System.IO;
using System.Threading;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Restore.Tests
{
    public class GivenThatWeWantToRestoreProjectsWithPackageDowngrades : SdkTest
    {
        public GivenThatWeWantToRestoreProjectsWithPackageDowngrades(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void DowngradeWarningsAreErrorsByDefault()
        {
            const string testProjectName = "ProjectWithDowngradeWarning";
            var testProject = new TestProject()
            {
                Name = testProjectName,
                TargetFrameworks = "netstandard2.0",
                IsSdkProject = true
            };

            testProject.PackageReferences.Add(new TestPackageReference("NuGet.Packaging", "3.5.0", null));
            testProject.PackageReferences.Add(new TestPackageReference("NuGet.Commands", "4.0.0", null));

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var packagesFolder = Path.Combine(TestContext.Current.TestExecutionDirectory, "packages", testProjectName);

            var restoreCommand = testAsset.GetRestoreCommand(Log, relativePath: testProjectName);
            restoreCommand
                .Execute($"/p:RestorePackagesPath={packagesFolder}")
                .Should().Fail()
                .And.HaveStdOutContaining("NU1605");

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.Path, testProjectName));
            buildCommand
                .Execute()
                .Should().Fail()
                .And.HaveStdOutContaining("NU1605");
        }

        [CoreMSBuildOnlyFact]
        public void ItIsPossibleToTurnOffDowngradeWarningsAsErrors()
        {
            const string testProjectName = "ProjectWithDowngradeWarning";
            var testProject = new TestProject()
            {
                Name = testProjectName,
                TargetFrameworks = "netstandard2.0",
                IsSdkProject = true
            };

            testProject.AdditionalProperties.Add("WarningsAsErrors", string.Empty);
            testProject.PackageReferences.Add(new TestPackageReference("NuGet.Packaging", "3.5.0", null));
            testProject.PackageReferences.Add(new TestPackageReference("NuGet.Commands", "4.0.0", null));

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var packagesFolder = Path.Combine(TestContext.Current.TestExecutionDirectory, "packages", testProjectName);

            var restoreCommand = testAsset.GetRestoreCommand(Log, relativePath: testProjectName);
            restoreCommand
                .Execute($"/p:RestorePackagesPath={packagesFolder}")
                .Should().Pass();;
        }
    }
}
