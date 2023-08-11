// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.ToolPackage;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Versioning;

namespace Microsoft.DotNet.PackageInstall.Tests
{
    public class NuGetPackageInstallerExtractTests : SdkTest
    {
        public NuGetPackageInstallerExtractTests(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public async Task ItCanExtractNugetPackage()
        {
            string packageId = "Newtonsoft.Json";
            string packageVersion = ToolsetInfo.GetNewtonsoftJsonPackageVersion();
            NuGetTestLogger logger = new NuGetTestLogger(Log);
            NuGetPackageDownloader installer =
                new NuGetPackageDownloader(new DirectoryPath(Directory.GetCurrentDirectory()), null,
                    new MockFirstPartyNuGetPackageSigningVerifier(), logger, restoreActionConfig: new RestoreActionConfig(NoCache: true));
            string packagePath =
                await installer.DownloadPackageAsync(new PackageId(packageId), new NuGetVersion(packageVersion));
            string targetPath = Path.Combine(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()),
                "ExtractedPackage");
            IEnumerable<string> result = await installer.ExtractPackageAsync(packagePath, new DirectoryPath(targetPath));

            Directory.Exists(targetPath).Should().BeTrue();
            string[] extractedFiles = Directory.GetFiles(targetPath, "*", SearchOption.AllDirectories);
            extractedFiles.Should().Contain(Path.Combine(targetPath, $"{packageId}.nuspec"));
            extractedFiles.Should().BeEquivalentTo(result);
        }

        [UnixOnlyFact]
        public void ItCanGetAllFilesNeedToSetExecutablePermission()
        {
            NuGetTestLogger logger = new NuGetTestLogger(Log);
            NuGetPackageDownloader installer =
                new NuGetPackageDownloader(
                    new DirectoryPath(Directory.GetCurrentDirectory()),
                    null,
                    new MockFirstPartyNuGetPackageSigningVerifier(), logger, restoreActionConfig: new RestoreActionConfig(NoCache: true));
            var allFiles = new List<string>()
            {
                "/ExtractedPackage/Microsoft.Android.Sdk.Darwin.nuspec",
                "/ExtractedPackage/LICENSE",
                "/ExtractedPackage/PreserveLists/Java.Interop.xml",
                "/ExtractedPackage/tools/javadoc-to-mdoc.dll",
                "/ExtractedPackage/tools/javadoc-to-mdoc.runtimeconfig.json",
                "/ExtractedPackage/tools/Darwin/mono",
            };
            var result = installer.FindAllFilesNeedExecutablePermission(allFiles, "/ExtractedPackage");
            result.Should().HaveCount(1);
            result.First().Value.Should().Be("/ExtractedPackage/tools/Darwin/mono",
                "file without extension under tools folder");
        }

        [UnixOnlyFact]
        public void GivenPackageNotInAllowListItCannotGetAllFilesNeedToSetExecutablePermission()
        {
            NuGetTestLogger logger = new NuGetTestLogger(Log);
            NuGetPackageDownloader installer =
                new NuGetPackageDownloader(new DirectoryPath(Directory.GetCurrentDirectory()), null,
                    new MockFirstPartyNuGetPackageSigningVerifier(), logger, restoreActionConfig: new RestoreActionConfig(NoCache: true));
            var allFiles = new List<string>()
            {
                "/ExtractedPackage/Not.In.Allow.List.nuspec",
                "/ExtractedPackage/LICENSE",
                "/ExtractedPackage/PreserveLists/Java.Interop.xml",
                "/ExtractedPackage/tools/javadoc-to-mdoc.dll",
                "/ExtractedPackage/tools/javadoc-to-mdoc.runtimeconfig.json",
                "/ExtractedPackage/tools/Darwin/mono",
            };
            var result = installer.FindAllFilesNeedExecutablePermission(allFiles, "/ExtractedPackage");
            result.Should().BeEmpty("Not in allow list package");
        }
    }
}
