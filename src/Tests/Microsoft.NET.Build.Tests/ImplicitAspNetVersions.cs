// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using NuGet.Common;
using NuGet.Frameworks;
using NuGet.ProjectModel;
using NuGet.Versioning;

namespace Microsoft.NET.Build.Tests
{
    public class ImplicitAspNetVersions : SdkTest
    {
        public ImplicitAspNetVersions(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData("Microsoft.AspNetCore.App")]
        [InlineData("Microsoft.AspNetCore.All")]
        public void AspNetCoreVersionIsSetImplicitly(string aspnetPackageName)
        {
            var testProject = new TestProject()
            {
                Name = "AspNetImplicitVersion",
                TargetFrameworks = "netcoreapp2.1",
                IsExe = true
            };

            //  Add versionless PackageReference
            testProject.PackageReferences.Add(new TestPackageReference(aspnetPackageName, null));

            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: aspnetPackageName);

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute()
                .Should()
                .Pass();

            var aspnetVersion = GetLibraryVersion(testProject, buildCommand, aspnetPackageName);

            //  Version of AspNetCore packages is 2.1.1 because 2.1.0 packages had exact version constraints, which was broken
            aspnetVersion.ToString().Should().Be("2.1.1");
        }

        [Theory]
        [InlineData("Microsoft.AspNetCore.App")]
        [InlineData("Microsoft.AspNetCore.All")]
        public void AspNetCoreVersionRollsForward(string aspnetPackageName)
        {
            var testProject = new TestProject()
            {
                Name = "AspNetImplicitVersion",
                TargetFrameworks = "netcoreapp2.1",
                IsExe = true,

            };

            testProject.RuntimeIdentifier = EnvironmentInfo.GetCompatibleRid(testProject.TargetFrameworks);

            //  Add versionless PackageReference
            testProject.PackageReferences.Add(new TestPackageReference(aspnetPackageName, null));

            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: aspnetPackageName);

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute()
                .Should()
                .Pass();

            var aspnetVersion = GetLibraryVersion(testProject, buildCommand, aspnetPackageName);

            //  Self-contained app (because RID is specified) should roll forward to later patch
            aspnetVersion.CompareTo(new SemanticVersion(2, 1, 1)).Should().BeGreaterThan(0);
        }

        [Theory]
        [InlineData("Microsoft.AspNetCore.App")]
        [InlineData("Microsoft.AspNetCore.All")]
        public void ExplicitVersionsOfAspNetCoreWarn(string aspnetPackageName)
        {
            var testProject = new TestProject()
            {
                Name = "AspNetExplicitVersion",
                TargetFrameworks = "netcoreapp2.1",
                IsExe = true
            };

            string explicitVersion = "2.1.0";

            testProject.PackageReferences.Add(new TestPackageReference(aspnetPackageName, explicitVersion));

            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: aspnetPackageName);

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("NETSDK1071");

            var aspnetVersion = GetLibraryVersion(testProject, buildCommand, aspnetPackageName);

            aspnetVersion.ToString().Should().Be(explicitVersion);
        }

        [Fact]
        public void MultipleWarningsAreGeneratedForMultipleExplicitReferences()
        {
            var testProject = new TestProject()
            {
                Name = "MultipleExplicitReferences",
                TargetFrameworks = "netcoreapp2.1",
                IsExe = true
            };

            testProject.PackageReferences.Add(new TestPackageReference("Microsoft.NETCore.App", "2.1.0"));
            testProject.PackageReferences.Add(new TestPackageReference("Microsoft.AspNetCore.App", "2.1.0"));

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var restoreCommand = new RestoreCommand(testAsset);
            restoreCommand
                .Execute()
                .Should()
                .Pass()
                .And
                .NotHaveStdOutContaining("NETSDK1071");


            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("NETSDK1071")
                .And
                .HaveStdOutContaining("NETSDK1023");
        }

        [Theory]
        [InlineData(true, null)]
        [InlineData(true, "2.1.1")]
        [InlineData(false, null)]
        public void WhenTargetingNetCore3_0AspNetCoreAllPackageReferenceErrors(bool useWebSdk, string packageVersion)
        {
            var testProject = new TestProject()
            {
                Name = "AspNetCoreAll_On3_0",
                TargetFrameworks = "netcoreapp3.0",
                ProjectSdk = useWebSdk ? "Microsoft.NET.Sdk.Web" : null,
                IsExe = true
            };

            //  Add PackageReference
            testProject.PackageReferences.Add(new TestPackageReference("Microsoft.AspNetCore.All", packageVersion));

            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: $"{useWebSdk}_{packageVersion}");

            var restoreCommand = new RestoreCommand(testAsset);
            restoreCommand.Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1079");

            var buildCommand = new BuildCommand(testAsset);

            buildCommand.Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1079");
        }

        [Theory]
        [InlineData(true, null)]
        [InlineData(true, "2.1.1")]
        [InlineData(false, null)]
        public void WhenTargetingNetCore3_0AspNetCoreAppPackageReferenceWarns(bool useWebSdk, string packageVersion)
        {
            var testProject = new TestProject()
            {
                Name = "AspNetCoreApp_On3_0",
                TargetFrameworks = "netcoreapp3.0",
                ProjectSdk = useWebSdk ? "Microsoft.NET.Sdk.Web" : null,
                IsExe = true
            };

            //  Add PackageReference
            testProject.PackageReferences.Add(new TestPackageReference("Microsoft.AspNetCore.App", packageVersion));

            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: $"{useWebSdk}_{packageVersion}");

            var restoreCommand = new RestoreCommand(testAsset);
            restoreCommand.Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("NETSDK1080");

            var buildCommand = new BuildCommand(testAsset);

            buildCommand.Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("NETSDK1080");
        }

        static NuGetVersion GetLibraryVersion(TestProject testProject, BuildCommand buildCommand, string libraryName)
        {
            LockFile lockFile = LockFileUtilities.GetLockFile(
                Path.Combine(buildCommand.GetBaseIntermediateDirectory().FullName, "project.assets.json"),
                NullLogger.Instance);

            var target = lockFile.GetTarget(NuGetFramework.Parse(testProject.TargetFrameworks), testProject.RuntimeIdentifier);
            var lockFileLibrary = target.Libraries.Single(l => l.Name == libraryName);

            return lockFileLibrary.Version;
        }
    }
}
