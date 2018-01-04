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
    public class GivenThatWeWantToRestoreProjectsUsingNuGetConfigProperties : SdkTest
    {
        public GivenThatWeWantToRestoreProjectsUsingNuGetConfigProperties(ITestOutputHelper log) : base(log)
        {
        }

        // https://github.com/dotnet/sdk/issues/1327
        [CoreMSBuildOnlyTheory]
        [InlineData("netstandard1.3", "1.3", false)]
        [InlineData("netcoreapp1.0", "1.0", true)]
        [InlineData("netcoreapp1.1", "1.1", true)]
        [InlineData("netstandard2.0", "2.0", false)]
        [InlineData("netcoreapp2.0", "2.0app", false)]
        [InlineData("net461", "461app", false)]
        [InlineData("netcoreapp2.0;net461", "multiTFM20app", false)]
        [InlineData("netcoreapp1.0;netcoreapp2.0", "multiTFM1020app", true)]
        [InlineData("netcoreapp1.0;net461", "multiTFM1046app", true)]
        public void I_can_restore_a_project_with_implicit_msbuild_nuget_config(
            string frameworks,
            string projectPrefix,
            bool fileExists)
        {
            string testProjectName = $"{projectPrefix}Fallback";
            TestAsset testProjectTestAsset = CreateTestAsset(testProjectName, frameworks);

            var packagesFolder = Path.Combine(TestContext.Current.TestExecutionDirectory, "packages", testProjectName);

            var restoreCommand = testProjectTestAsset.GetRestoreCommand(Log, relativePath: testProjectName);
            restoreCommand.Execute($"/p:RestorePackagesPath={packagesFolder}", $"/p:_NugetFallbackFolder={TestContext.Current.NuGetFallbackFolder}").Should().Pass();

            File.Exists(Path.Combine(
                packagesFolder,
                "projectinfallbackfolder",
                "1.0.0",
                "projectinfallbackfolder.1.0.0.nupkg")).Should().Be(fileExists);
        }

        [Theory]
        [InlineData("netstandard1.3", "1.3")]
        [InlineData("netcoreapp1.0", "1.0")]
        [InlineData("netcoreapp1.1", "1.1")]
        [InlineData("netstandard2.0", "2.0")]
        [InlineData("netcoreapp2.0", "2.0app")]
        public void I_can_disable_implicit_msbuild_nuget_config(string frameworks, string projectPrefix)
        {
            string testProjectName = $"{projectPrefix}DisabledFallback";
            TestAsset testProjectTestAsset = CreateTestAsset(testProjectName, frameworks);

            var restoreCommand = testProjectTestAsset.GetRestoreCommand(Log, relativePath: testProjectName);
            restoreCommand.Execute($"/p:DisableImplicitNuGetFallbackFolder=true").Should().Fail();
        }

        private TestAsset CreateTestAsset(string testProjectName, string frameworks)
        {
            var packageInNuGetFallbackFolder = CreatePackageInNuGetFallbackFolder();

            var testProject =
                new TestProject
                {
                    Name = testProjectName,
                    TargetFrameworks = frameworks,
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
                    TestContext.Current.NuGetFallbackFolder);

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
                    .Execute($"/p:PackageOutputPath={TestContext.Current.NuGetFallbackFolder}").Should().Pass();

                ExtractNupkg(
                    TestContext.Current.NuGetFallbackFolder,
                    Path.Combine(TestContext.Current.NuGetFallbackFolder, $"{projectInNuGetFallbackFolder.Name}.1.0.0.nupkg"));
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
                    PackageExtractor.InstallFromSourceAsync(
                        identity,
                        stream => fileStream.CopyToAsync(stream, 4096, CancellationToken.None),
                        pathResolver,
                        new PackageExtractionContext(
                            PackageSaveMode.Defaultv3,
                            XmlDocFileSaveMode.None,
                            NullLogger.Instance,
                            signedPackageVerifier: null),
                        CancellationToken.None).Wait();
                }
            }
        }
    }
}
