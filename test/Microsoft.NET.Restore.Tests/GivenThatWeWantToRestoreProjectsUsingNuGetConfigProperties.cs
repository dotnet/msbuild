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

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToRestoreProjectsUsingNuGetConfigProperties : SdkTest
    {
        public GivenThatWeWantToRestoreProjectsUsingNuGetConfigProperties(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData("netstandard1.3", true)]
        [InlineData("netcoreapp1.0", true)]
        [InlineData("netcoreapp1.1", true)]
        [InlineData("netstandard2.0", false)]
        [InlineData("netcoreapp2.0", false)]
        public void I_can_restore_a_project_with_implicit_msbuild_nuget_config(string framework, bool fileExists)
        {
            string testProjectName = framework.Replace(".", "") + "ProjectWithFallbackFolderReference";   
            TestAsset testProjectTestAsset = CreateTestAsset(testProjectName, framework);

            var packagesFolder = Path.Combine(testProjectTestAsset.Path, "packages");

            var restoreCommand = testProjectTestAsset.GetRestoreCommand(Log, relativePath: testProjectName);
            restoreCommand.Execute($"/p:RestorePackagesPath={packagesFolder}").Should().Pass();

            File.Exists(Path.Combine(
                packagesFolder,
                "projectinfallbackfolder",
                "1.0.0",
                "projectinfallbackfolder.1.0.0.nupkg")).Should().Be(fileExists);
        }

        [Theory]
        [InlineData("netstandard1.3")]
        [InlineData("netcoreapp1.0")]
        [InlineData("netcoreapp1.1")]
        [InlineData("netstandard2.0")]
        [InlineData("netcoreapp2.0")]
        public void I_can_disable_implicit_msbuild_nuget_config(string framework)
        {
            string testProjectName = framework.Replace(".", "") + "ProjectWithDisabledFallbackFolder";   
            TestAsset testProjectTestAsset = CreateTestAsset(testProjectName, framework);

            var restoreCommand = testProjectTestAsset.GetRestoreCommand(Log, relativePath: testProjectName);
            restoreCommand.Execute($"/p:DisableImplicitNuGetFallbackFolder=true").Should().Fail();
        }

        private TestAsset CreateTestAsset(string testProjectName, string framework)
        {
            var packageInNuGetFallbackFolder = CreatePackageInNuGetFallbackFolder();

            var testProject =
                new TestProject
                {
                    Name = testProjectName,
                    TargetFrameworks = framework,
                    IsSdkProject = true
                };

            testProject.PackageReferences.Add(packageInNuGetFallbackFolder);

            var testProjectTestAsset = _testAssetsManager.CreateTestProject(
                testProject,
                string.Empty,
                testProjectName);

            return testProjectTestAsset;
        }

        private TestPackageReference CreatePackageInNuGetFallbackFolder()
        {
            var projectInNuGetFallbackFolder =
                new TestProject
                {
                    Name = $"ProjectInFallbackFolder",
                    TargetFrameworks = "netstandard1.3",
                    IsSdkProject = true
                };

            var projectInNuGetFallbackFolderPackageReference =
                new TestPackageReference(
                    projectInNuGetFallbackFolder.Name,
                    "1.0.0",
                    RepoInfo.NuGetFallbackFolder);

            if (!projectInNuGetFallbackFolderPackageReference.NuGetPackageExists())
            {
                var projectInNuGetFallbackFolderTestAsset =
                    _testAssetsManager.CreateTestProject(projectInNuGetFallbackFolder);
                var packageRestoreCommand = projectInNuGetFallbackFolderTestAsset.GetRestoreCommand(
                    Log,
                    relativePath: projectInNuGetFallbackFolder.Name).Execute().Should().Pass();
                var dependencyProjectDirectory = Path.Combine(
                    projectInNuGetFallbackFolderTestAsset.TestRoot,
                    projectInNuGetFallbackFolder.Name);
                var packagePackCommand =
                    new PackCommand(Log, dependencyProjectDirectory)
                    .Execute($"/p:PackageOutputPath={RepoInfo.NuGetFallbackFolder}").Should().Pass();

                ExtractNupkg(
                    RepoInfo.NuGetFallbackFolder,
                    Path.Combine(RepoInfo.NuGetFallbackFolder, $"{projectInNuGetFallbackFolder.Name}.1.0.0.nupkg"));
            }

            return projectInNuGetFallbackFolderPackageReference;
        }

        private void ExtractNupkg(string nugetCache, string nupkg)
        {
            var pathResolver = new VersionFolderPathResolver(nugetCache);

            PackageIdentity identity = null;

            using (var reader = new PackageArchiveReader(File.OpenRead(nupkg)))
            {
                identity = reader.GetIdentity();
            }

            if (!File.Exists(pathResolver.GetHashPath(identity.Id, identity.Version)))
            {
                using (var fileStream = File.OpenRead(nupkg))
                {
                    PackageExtractor.InstallFromSourceAsync((stream) =>
                        fileStream.CopyToAsync(stream, 4096, CancellationToken.None),
                        new VersionFolderPathContext(
                            identity,
                            nugetCache,
                            NullLogger.Instance,
                            PackageSaveMode.Defaultv3,
                            XmlDocFileSaveMode.None),
                        CancellationToken.None).Wait();
                }
            }
        }
    }
}