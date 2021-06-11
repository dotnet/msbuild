// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;
using FluentAssertions;
using Microsoft.DotNet.Cli;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Versioning;
using Xunit;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.ToolPackage;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Utilities;
using Xunit.Abstractions;

namespace Microsoft.DotNet.PackageInstall.Tests
{
    public class NuGetPackageInstallerTests : SdkTest
    {
        private const string TestPackageVersion = "1.0.4";
        private const string TestPreviewPackageVersion = "2.0.1-preview1";
        private static readonly PackageId TestPackageId = new PackageId("global.tool.console.demo");
        private readonly NuGetPackageDownloader _installer;

        private readonly DirectoryPath _tempDirectory;

        private readonly string _testTargetframework = BundledTargetFramework.GetTargetFrameworkMoniker();
        private readonly NuGetTestLogger _logger;

        public NuGetPackageInstallerTests(ITestOutputHelper log) : base(log)
        {
            _tempDirectory = GetUniqueTempProjectPathEachTest();
            _logger = new NuGetTestLogger();
            _installer =
                new NuGetPackageDownloader(_tempDirectory, null, new MockFirstPartyNuGetPackageSigningVerifier(), _logger);
        }

        [Fact]
        public async Task GivenNoFeedInstallFailsWithException() =>
            await Assert.ThrowsAsync<NuGetPackageInstallerException>(() =>
                _installer.DownloadPackageAsync(TestPackageId, new NuGetVersion(TestPackageVersion)));

        [Fact]
        public async Task GivenASourceInstallSucceeds()
        {
            string packagePath = await _installer.DownloadPackageAsync(
                TestPackageId,
                new NuGetVersion(TestPackageVersion),
                new PackageSourceLocation(sourceFeedOverrides: new[] {GetTestLocalFeedPath()}));
            File.Exists(packagePath).Should().BeTrue();
            packagePath.Should().Contain(_tempDirectory.Value, "Package should be downloaded to the input folder");
        }

        [Fact]
        public async Task GivenAFailedSourceItShouldError()
        {
            DirectoryPath nonExistFeed =
                new DirectoryPath(Path.GetTempPath()).WithSubDirectories(Path.GetRandomFileName());

            await Assert.ThrowsAsync<NuGetPackageInstallerException>(() =>
                _installer.DownloadPackageAsync(
                    TestPackageId,
                    new NuGetVersion(TestPackageVersion),
                    new PackageSourceLocation(sourceFeedOverrides: new[] {nonExistFeed.Value})));
        }

        [Fact]
        public async Task GivenNugetConfigInstallSucceeds()
        {
            FilePath nugetConfigPath = GenerateRandomNugetConfigFilePath();
            FileSystemWrapper fileSystem = new FileSystemWrapper();
            WriteNugetConfigFileToPointToTheFeed(fileSystem, nugetConfigPath);

            string packagePath = await _installer.DownloadPackageAsync(
                TestPackageId,
                new NuGetVersion(TestPackageVersion),
                new PackageSourceLocation(nugetConfigPath));
            File.Exists(packagePath).Should().BeTrue();
        }

        [Fact]
        public async Task GivenAValidNugetConfigAndFailedSourceItShouldError()
        {
            DirectoryPath nonExistFeed =
                new DirectoryPath(Path.GetTempPath()).WithSubDirectories(Path.GetRandomFileName());

            FilePath validNugetConfigPath = GenerateRandomNugetConfigFilePath();
            FileSystemWrapper fileSystem = new FileSystemWrapper();
            WriteNugetConfigFileToPointToTheFeed(fileSystem, validNugetConfigPath);

            // "source" option will override everything like nuget.config just like "dotner restore --source ..."
            await Assert.ThrowsAsync<NuGetPackageInstallerException>(() =>
                _installer.DownloadPackageAsync(
                    TestPackageId,
                    new NuGetVersion(TestPackageVersion),
                    new PackageSourceLocation(validNugetConfigPath,
                        sourceFeedOverrides: new[] {nonExistFeed.Value})));
        }

        [Fact]
        public async Task GivenAConfigFileRootDirectoryPackageInstallSucceedsViaFindingNugetConfigInParentDir()
        {
            FilePath nugetConfigPath = GenerateRandomNugetConfigFilePath();
            DirectoryPath directoryBelowNugetConfig = nugetConfigPath.GetDirectoryPath().WithSubDirectories("subDir");
            Directory.CreateDirectory(directoryBelowNugetConfig.Value);

            FileSystemWrapper fileSystem = new FileSystemWrapper();
            WriteNugetConfigFileToPointToTheFeed(fileSystem, nugetConfigPath);

            string packagePath = await _installer.DownloadPackageAsync(
                TestPackageId,
                new NuGetVersion(TestPackageVersion),
                new PackageSourceLocation(rootConfigDirectory: directoryBelowNugetConfig));
            File.Exists(packagePath).Should().BeTrue();
        }

        [Fact]
        public async Task GivenNoPackageVersionItCanInstallLatestVersionOfPackage()
        {
            string packagePath = await _installer.DownloadPackageAsync(
                TestPackageId,
                packageSourceLocation: new PackageSourceLocation(sourceFeedOverrides: new[] {GetTestLocalFeedPath()}));
            packagePath.Should().Contain("global.tool.console.demo.1.0.4.nupkg", "It can get the latest non preview version");
            File.Exists(packagePath).Should().BeTrue();
        }

        [Fact]
        public async Task GivenARelativeSourcePathInstallSucceeds()
        {
            string getTestLocalFeedPath = GetTestLocalFeedPath();
            string relativePath = Path.GetRelativePath(Environment.CurrentDirectory, getTestLocalFeedPath);
            Log.WriteLine(relativePath);
            string packagePath = await _installer.DownloadPackageAsync(
                TestPackageId,
                new NuGetVersion(TestPackageVersion),
                new PackageSourceLocation(sourceFeedOverrides: new[] {relativePath}));
            File.Exists(packagePath).Should().BeTrue();
            packagePath.Should().Contain(_tempDirectory.Value, "Package should be downloaded to the input folder");
        }

        [Fact]
        public async Task WhenPassedIncludePreviewItInstallSucceeds()
        {
            string getTestLocalFeedPath = GetTestLocalFeedPath();
            string relativePath = Path.GetRelativePath(Environment.CurrentDirectory, getTestLocalFeedPath);
            Log.WriteLine(relativePath);
            string packagePath = await _installer.DownloadPackageAsync(
                TestPackageId,
                packageSourceLocation: new PackageSourceLocation(sourceFeedOverrides: new[] {relativePath}),
                includePreview: true);
            File.Exists(packagePath).Should().BeTrue();
            packagePath.Should().Contain(TestPackageId + "." + TestPreviewPackageVersion,
                "Package should download higher package version");
        }

        [WindowsOnlyFact]
        public async Task GivenANonSignedSdkItShouldPrintMessageOnce()
        {
            BufferedReporter bufferedReporter = new BufferedReporter();
            NuGetPackageDownloader nuGetPackageDownloader = new NuGetPackageDownloader(_tempDirectory, null,
                new MockFirstPartyNuGetPackageSigningVerifier(isExecutableIsFirstPartySignedWithoutValidation: false),
                _logger, bufferedReporter);
            await nuGetPackageDownloader.DownloadPackageAsync(
                TestPackageId,
                new NuGetVersion(TestPackageVersion),
                new PackageSourceLocation(sourceFeedOverrides: new[] {GetTestLocalFeedPath()}));

            // download 2 packages should only print the message once
            string packagePath = await nuGetPackageDownloader.DownloadPackageAsync(
                TestPackageId,
                new NuGetVersion(TestPackageVersion),
                new PackageSourceLocation(sourceFeedOverrides: new[] {GetTestLocalFeedPath()}));

            bufferedReporter.Lines.Should()
                .ContainSingle(
                    LocalizableStrings.SkipNuGetpackageSigningValidationSDKNotFirstParty);
            File.Exists(packagePath).Should().BeTrue();
        }

        [WindowsOnlyFact]
        public async Task WhenCalledWithNotSignedPackageItShouldThrowWithCommandOutput()
        {
            string commandOutput = "COMMAND OUTPUT";
            NuGetPackageDownloader nuGetPackageDownloader = new NuGetPackageDownloader(_tempDirectory, null,
                new MockFirstPartyNuGetPackageSigningVerifier(verifyResult: false, commandOutput: commandOutput),
                _logger);

            NuGetPackageInstallerException ex = await Assert.ThrowsAsync<NuGetPackageInstallerException>(() =>
                nuGetPackageDownloader.DownloadPackageAsync(
                    TestPackageId,
                    new NuGetVersion(TestPackageVersion),
                    new PackageSourceLocation(sourceFeedOverrides: new[] {GetTestLocalFeedPath()})));

            ex.Message.Should().Contain(commandOutput);
        }

        [UnixOnlyFact]
        public async Task GivenANonWindowsMachineItShouldPrintMessageOnce()
        {
            BufferedReporter bufferedReporter = new BufferedReporter();
            NuGetPackageDownloader nuGetPackageDownloader = new NuGetPackageDownloader(_tempDirectory, null,
                new MockFirstPartyNuGetPackageSigningVerifier(isExecutableIsFirstPartySignedWithoutValidation: false),
                _logger, bufferedReporter);
            await nuGetPackageDownloader.DownloadPackageAsync(
                TestPackageId,
                new NuGetVersion(TestPackageVersion),
                new PackageSourceLocation(sourceFeedOverrides: new[] {GetTestLocalFeedPath()}));

            // download 2 packages should only print the message once
            string packagePath = await nuGetPackageDownloader.DownloadPackageAsync(
                TestPackageId,
                new NuGetVersion(TestPackageVersion),
                new PackageSourceLocation(sourceFeedOverrides: new[] {GetTestLocalFeedPath()}));

            bufferedReporter.Lines.Should()
                .ContainSingle(
                    LocalizableStrings.SkipNuGetpackageSigningValidationmacOSLinux);
            File.Exists(packagePath).Should().BeTrue();
        }

        private static DirectoryPath GetUniqueTempProjectPathEachTest()
        {
            DirectoryPath tempProjectDirectory =
                new DirectoryPath(Path.GetTempPath()).WithSubDirectories(Path.GetRandomFileName());

            return tempProjectDirectory;
        }

        private static void WriteNugetConfigFileToPointToTheFeed(IFileSystem fileSystem, FilePath? filePath)
        {
            if (!filePath.HasValue) return;

            fileSystem.Directory.CreateDirectory(filePath.Value.GetDirectoryPath().Value);

            fileSystem.File.WriteAllText(filePath.Value.Value, FormatNuGetConfig(
                GetTestLocalFeedPath()));
        }

        public static string FormatNuGetConfig(string localFeedPath)
        {
            const string template = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
<packageSources>
<!--To inherit the global NuGet package sources remove the <clear/> line below -->
<clear />
<add key=""Test Source"" value=""{0}"" />
<add key=""dotnet-public"" value=""https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-public/nuget/v3/index.json"" />
<add key=""myget-legacy"" value=""https://pkgs.dev.azure.com/dnceng/public/_packaging/myget-legacy/nuget/v3/index.json"" />
</packageSources>
</configuration>";
            return string.Format(template, localFeedPath);
        }

        private static FilePath GenerateRandomNugetConfigFilePath()
        {
            const string nugetConfigName = "nuget.config";
            string tempPathForNugetConfigWithWhiteSpace =
                Path.Combine(Path.GetTempPath(),
                    Path.GetRandomFileName() + " " + Path.GetRandomFileName());

            FilePath nugetConfigFullPath =
                new FilePath(Path.GetFullPath(Path.Combine(tempPathForNugetConfigWithWhiteSpace, nugetConfigName)));
            return nugetConfigFullPath;
        }

        private static string GetTestLocalFeedPath() =>
            Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "TestAssetLocalNugetFeed");
    }
}
