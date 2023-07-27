// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using NuGet.Common;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Packaging.Signing;

namespace Microsoft.NET.Restore.Tests
{
    public class GivenThatWeWantToRestoreProjectsUsingNuGetConfigProperties : SdkTest
    {
        public GivenThatWeWantToRestoreProjectsUsingNuGetConfigProperties(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData("netstandard1.3", "1.3", false)]
        [InlineData("netcoreapp1.0", "1.0", true)]
        [InlineData("netcoreapp1.1", "1.1", true)]
        [InlineData("netstandard2.0", "2.0", false)]
        [InlineData("netcoreapp2.0", "2.0app", false)]
        [InlineData("net462", "461app", false)]
        [InlineData("netcoreapp2.0;net462", "multiTFM20app", false)]
        [InlineData("netcoreapp1.0;netcoreapp2.0", "multiTFM1020app", true)]
        [InlineData("netcoreapp1.0;net462", "multiTFM1046app", true)]
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
                GetUniquePackageNameForEachTestProject(testProjectName),
                "1.0.0",
                $"{GetUniquePackageNameForEachTestProject(testProjectName)}.1.0.0.nupkg")).Should().Be(fileExists);
        }

        [Theory]
        [InlineData("netstandard1.3", "1.3")]
        [InlineData(ToolsetInfo.CurrentTargetFramework, ToolsetInfo.CurrentTargetFrameworkVersion)]
        // base line of the following tests
        public void I_can_restore_with_implicit_msbuild_nuget_config(string frameworks, string projectPrefix)
        {
            string testProjectName = $"{projectPrefix}EnableFallback";
            TestAsset testProjectTestAsset = CreateTestAsset(testProjectName, frameworks);

            var restoreCommand = testProjectTestAsset.GetRestoreCommand(Log, relativePath: testProjectName);
            restoreCommand.Execute($"/p:_NugetFallbackFolder={TestContext.Current.NuGetFallbackFolder}").Should().Pass();
        }

        [Theory]
        [InlineData("netstandard1.3", "1.3")]
        [InlineData(ToolsetInfo.CurrentTargetFramework, ToolsetInfo.CurrentTargetFrameworkVersion)]
        [InlineData("netcoreapp1.1", "1.1")]
        [InlineData("netstandard2.0", "2.0")]
        [InlineData("netcoreapp2.0", "2.0app")]
        public void I_can_disable_implicit_msbuild_nuget_config(string frameworks, string projectPrefix)
        {
            string testProjectName = $"{projectPrefix}DisabledFallback";
            TestAsset testProjectTestAsset = CreateTestAsset(testProjectName, frameworks);

            var restoreCommand = testProjectTestAsset.GetRestoreCommand(Log, relativePath: testProjectName);
            restoreCommand.Execute($"/p:_NugetFallbackFolder={TestContext.Current.NuGetFallbackFolder}", "/p:DisableImplicitNuGetFallbackFolder=true").Should().Fail();
        }

        [Theory]
        [InlineData("netstandard1.3", "1.3", true)]
        [InlineData("netcoreapp1.0", "1.0", false)]
        [InlineData("netcoreapp1.1", "1.1", false)]
        [InlineData("netstandard2.0", "2.0", true)]
        [InlineData("netcoreapp2.0", "2.0app", true)]
        public void I_can_disable_1_x_implicit_msbuild_nuget_config(string frameworks, string projectPrefix, bool shouldExecutePass)
        {
            string testProjectName = $"{projectPrefix}1xDisabledFallback";
            TestAsset testProjectTestAsset = CreateTestAsset(testProjectName, frameworks);

            var restoreCommand = testProjectTestAsset.GetRestoreCommand(Log, relativePath: testProjectName);
            var executeResult = restoreCommand.Execute($"/p:_NugetFallbackFolder={TestContext.Current.NuGetFallbackFolder}", "/p:DisableImplicit1xNuGetFallbackFolder=true");

            if (shouldExecutePass)
            {
                executeResult.Should().Pass();
            }
            else
            {
                executeResult.Should().Fail();
            }
        }

        private TestAsset CreateTestAsset(string testProjectName, string frameworks)
        {
            var packageInNuGetFallbackFolder = CreatePackageInNuGetFallbackFolder(testProjectName);

            var testProject =
                new TestProject
                {
                    Name = testProjectName,
                    TargetFrameworks = frameworks,
                };

            testProject.PackageReferences.Add(packageInNuGetFallbackFolder);

            var testProjectTestAsset = _testAssetsManager.CreateTestProject(
                testProject,
                string.Empty,
                testProjectName);

            return testProjectTestAsset;
        }

        private TestPackageReference CreatePackageInNuGetFallbackFolder(string testProjectName)
        {
            var projectInNuGetFallbackFolder =
                new TestProject
                {
                    Name = GetUniquePackageNameForEachTestProject(testProjectName),
                    TargetFrameworks = "netstandard1.3",
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

                // make sure there is no package in cache
                DeleteFolder(Path.Combine(TestContext.Current.NuGetCachePath, projectInNuGetFallbackFolder.Name.ToLowerInvariant()));
            }

            return projectInNuGetFallbackFolderPackageReference;
        }

        private static string GetUniquePackageNameForEachTestProject(string testProjectName)
        {
            return "for" + testProjectName.Replace(".", "").ToLower();
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
                    ClientPolicyContext clientPolicyContext = null;

                    PackageExtractor.InstallFromSourceAsync(
                        source: null,
                        packageIdentity: identity,
                        copyToAsync: stream => fileStream.CopyToAsync(stream, 4096, CancellationToken.None),
                        versionFolderPathResolver: pathResolver,
                        packageExtractionContext: new PackageExtractionContext(
                            PackageSaveMode.Defaultv3,
                            XmlDocFileSaveMode.None,
                            clientPolicyContext,
                            NullLogger.Instance),
                        token: CancellationToken.None).Wait();
                }
            }
        }

        private static void DeleteFolder(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
    }
}
