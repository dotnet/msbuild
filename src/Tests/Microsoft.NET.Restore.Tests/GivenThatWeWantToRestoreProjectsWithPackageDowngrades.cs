// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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

            var buildCommand = new BuildCommand(testAsset);
            buildCommand
                .Execute()
                .Should().Fail()
                .And.HaveStdOutContaining("NU1605");
        }

        [Fact]
        public void ItIsPossibleToTurnOffDowngradeWarningsAsErrors()
        {
            const string testProjectName = "ProjectWithDowngradeWarning";
            var testProject = new TestProject()
            {
                Name = testProjectName,
                TargetFrameworks = "netstandard2.0",
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
