// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantDiagnosticsWhenPackageCannotBeFound : SdkTest
    {
        public GivenThatWeWantDiagnosticsWhenPackageCannotBeFound(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_reports_missing_package_deleted_since_restore()
        {
            var package = GeneratePackageToGoMissing();

            var project = new TestProject
            {
                Name = "MissingPackageDeletedSinceRestore",
                TargetFrameworks = "netstandard1.3",
                PackageReferences = { package },
            };

            project.AdditionalProperties.Add(
                "RestoreAdditionalProjectSources",
                Path.GetDirectoryName(package.NupkgPath));

            var asset = _testAssetsManager
                .CreateTestProject(project, project.Name)
                .Restore(Log, project.Name);

            RemovePackageFromCache(package);

            var build = new BuildCommand(asset);

            build.ExecuteWithoutRestore()
                 .Should()
                 .Fail()
                 .And.HaveStdOutContaining(package.ID)
                 .And.HaveStdOutContaining(package.Version)
                 .And.NotHaveStdOutContaining("MSB4018"); // unhandled task exception

            // check that incremental build succeeds after a second restore to put back the package
            asset.Restore(Log, project.Name);
            build.Execute()
                 .Should()
                 .Pass();
        }

        private static void RemovePackageFromCache(TestPackageReference package)
        {
            // NuGet resolver returns null if sha512 file is not found. This is because
            // it writes it last to mitigate risk of using half-restore packaged. Deleting
            // only that file here to confirm that behavior and mitigate risk of a typo
            // here resulting in an overly aggressive recursive directory deletion.
            var shaFile = Path.Combine(
               TestContext.Current.NuGetCachePath,
               package.ID,
               package.Version,
               $"{package.ID}.{package.Version}.nupkg.sha512");

            var nupkgMetadataFile =  Path.Combine(
               TestContext.Current.NuGetCachePath,
               package.ID,
               package.Version,
               $".nupkg.metadata");

            File.Delete(shaFile);
            File.Delete(nupkgMetadataFile);
        }

        private TestPackageReference GeneratePackageToGoMissing()
        {
            var project = new TestProject
            {
                Name = "packagethatwillgomissing",
                TargetFrameworks = "netstandard1.3",
            };

            var asset = _testAssetsManager
               .CreateTestProject(project, project.Name);

            var pack = new PackCommand(
                Log,
                Path.Combine(asset.TestRoot, project.Name));

            pack.Execute().Should().Pass();

            return new TestPackageReference(
                project.Name,
                "1.0.0",
                pack.GetNuGetPackage(project.Name));
        }
    }
}
