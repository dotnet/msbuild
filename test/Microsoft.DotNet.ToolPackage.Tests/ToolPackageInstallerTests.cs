// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Transactions;
using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Tool.Install;
using Microsoft.DotNet.Tools.Tests.ComponentMocks;
using Microsoft.Extensions.DependencyModel.Tests;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.TemplateEngine.Cli;
using NuGet.Versioning;
using Xunit;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.Install.LocalizableStrings;

namespace Microsoft.DotNet.ToolPackage.Tests
{
    public class ToolPackageInstallerTests : TestBase
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenNoFeedInstallFailsWithException(bool testMockBehaviorIsInSync)
        {
            var (store, installer, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                feeds: new MockFeed[0]);

            Action a = () => installer.InstallPackage(
                packageId: TestPackageId,
                versionRange: VersionRange.Parse(TestPackageVersion),
                targetFramework: _testTargetframework);

            a.ShouldThrow<ToolPackageException>().WithMessage(LocalizableStrings.ToolInstallationRestoreFailed);

            reporter.Lines.Count.Should().Be(1);
            reporter.Lines[0].Should().Contain(TestPackageId.ToString());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenOfflineFeedInstallSucceeds(bool testMockBehaviorIsInSync)
        {
            var (store, installer, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                offlineFeed: new DirectoryPath(GetTestLocalFeedPath()),
                feeds: GetOfflineMockFeed());

            var package = installer.InstallPackage(
                packageId: TestPackageId,
                versionRange: VersionRange.Parse(TestPackageVersion),
                targetFramework: _testTargetframework);

            AssertPackageInstall(reporter, fileSystem, package, store);

            package.Uninstall();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenAEmptySourceAndOfflineFeedInstallSucceeds(bool testMockBehaviorIsInSync)
        {
            var emptySource = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(emptySource);

            var (store, installer, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                offlineFeed: new DirectoryPath(GetTestLocalFeedPath()),
                feeds: GetOfflineMockFeed());

            var package = installer.InstallPackage(
                packageId: TestPackageId,
                versionRange: VersionRange.Parse(TestPackageVersion),
                targetFramework: _testTargetframework,
                additionalFeeds: new[] {emptySource});

            AssertPackageInstall(reporter, fileSystem, package, store);

            package.Uninstall();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenNugetConfigInstallSucceeds(bool testMockBehaviorIsInSync)
        {
            var nugetConfigPath = WriteNugetConfigFileToPointToTheFeed();

            var (store, installer, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                feeds: GetMockFeedsForConfigFile(nugetConfigPath));

            var package = installer.InstallPackage(
                packageId: TestPackageId,
                versionRange: VersionRange.Parse(TestPackageVersion),
                targetFramework: _testTargetframework,
                nugetConfig: nugetConfigPath);

            AssertPackageInstall(reporter, fileSystem, package, store);

            package.Uninstall();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenNugetConfigInstallSucceedsInTransaction(bool testMockBehaviorIsInSync)
        {
            var nugetConfigPath = WriteNugetConfigFileToPointToTheFeed();

            var (store, installer, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                feeds: GetMockFeedsForConfigFile(nugetConfigPath));

            IToolPackage package = null;
            using (var transactionScope = new TransactionScope(
                TransactionScopeOption.Required,
                TimeSpan.Zero))
            {
                package = installer.InstallPackage(
                    packageId: TestPackageId,
                    versionRange: VersionRange.Parse(TestPackageVersion),
                    targetFramework: _testTargetframework,
                    nugetConfig: nugetConfigPath);

                transactionScope.Complete();
            }

            AssertPackageInstall(reporter, fileSystem, package, store);

            package.Uninstall();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenNugetConfigInstallCreatesAnAssetFile(bool testMockBehaviorIsInSync)
        {
            var nugetConfigPath = WriteNugetConfigFileToPointToTheFeed();

            var (store, installer, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                feeds: GetMockFeedsForConfigFile(nugetConfigPath));

            var package = installer.InstallPackage(
                packageId: TestPackageId,
                versionRange: VersionRange.Parse(TestPackageVersion),
                targetFramework: _testTargetframework,
                nugetConfig: nugetConfigPath);

            AssertPackageInstall(reporter, fileSystem, package, store);

            /*
              From mytool.dll to project.assets.json
               <root>/packageid/version/packageid/version/tools/framework/rid/mytool.dll
                                       /project.assets.json
             */
            var assetJsonPath = package.Commands[0].Executable
                .GetDirectoryPath()
                .GetParentPath()
                .GetParentPath()
                .GetParentPath()
                .GetParentPath()
                .GetParentPath()
                .WithFile("project.assets.json").Value;

            fileSystem.File.Exists(assetJsonPath).Should().BeTrue();

            package.Uninstall();
        }

        [Fact]
        public void GivenAConfigFileRootDirectoryPackageInstallSucceeds()
        {
            var nugetConfigPath = WriteNugetConfigFileToPointToTheFeed();

            var (store, installer, reporter, fileSystem) = Setup(useMock: false);

            var package = installer.InstallPackage(
                packageId: TestPackageId,
                versionRange: VersionRange.Parse(TestPackageVersion),
                targetFramework: _testTargetframework,
                rootConfigDirectory: nugetConfigPath.GetDirectoryPath());

            AssertPackageInstall(reporter, fileSystem, package, store);

            package.Uninstall();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenAllButNoPackageVersionItCanInstallThePackage(bool testMockBehaviorIsInSync)
        {
            var nugetConfigPath = WriteNugetConfigFileToPointToTheFeed();

            var (store, installer, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                feeds: GetMockFeedsForConfigFile(nugetConfigPath));

            var package = installer.InstallPackage(
                packageId: TestPackageId,
                targetFramework: _testTargetframework,
                nugetConfig: nugetConfigPath);

            AssertPackageInstall(reporter, fileSystem, package, store);

            package.Uninstall();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenAllButNoTargetFrameworkItCanDownloadThePackage(bool testMockBehaviorIsInSync)
        {
            var nugetConfigPath = WriteNugetConfigFileToPointToTheFeed();

            var (store, installer, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                feeds: GetMockFeedsForConfigFile(nugetConfigPath));

            var package = installer.InstallPackage(
                packageId: TestPackageId,
                versionRange: VersionRange.Parse(TestPackageVersion),
                nugetConfig: nugetConfigPath);

            AssertPackageInstall(reporter, fileSystem, package, store);

            package.Uninstall();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenASourceInstallSucceeds(bool testMockBehaviorIsInSync)
        {
            var source = GetTestLocalFeedPath();

            var (store, installer, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                feeds: GetMockFeedsForSource(source));

            var package = installer.InstallPackage(
                packageId: TestPackageId,
                versionRange: VersionRange.Parse(TestPackageVersion),
                targetFramework: _testTargetframework,
                additionalFeeds: new[] {source});

            AssertPackageInstall(reporter, fileSystem, package, store);

            package.Uninstall();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenARelativeSourcePathInstallSucceeds(bool testMockBehaviorIsInSync)
        {
            var source = GetTestLocalFeedPath();

            var (store, installer, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                feeds: GetMockFeedsForSource(source));

            var package = installer.InstallPackage(
                packageId: TestPackageId,
                versionRange: VersionRange.Parse(TestPackageVersion),
                targetFramework: _testTargetframework,
                additionalFeeds: new[] { Path.GetRelativePath(Directory.GetCurrentDirectory(), source) });

            AssertPackageInstall(reporter, fileSystem, package, store);

            package.Uninstall();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenAUriSourceInstallSucceeds(bool testMockBehaviorIsInSync)
        {
            var source = GetTestLocalFeedPath();

            var (store, installer, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                feeds: GetMockFeedsForSource(source));

            var package = installer.InstallPackage(
                packageId: TestPackageId,
                versionRange: VersionRange.Parse(TestPackageVersion),
                targetFramework: _testTargetframework,
                additionalFeeds: new[] { new Uri(source).AbsoluteUri });

            AssertPackageInstall(reporter, fileSystem, package, store);

            package.Uninstall();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenAEmptySourceAndNugetConfigInstallSucceeds(bool testMockBehaviorIsInSync)
        {
            var nugetConfigPath = WriteNugetConfigFileToPointToTheFeed();
            var emptySource = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(emptySource);

            var (store, installer, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                feeds: GetMockFeedsForSource(emptySource));

            var package = installer.InstallPackage(
                packageId: TestPackageId,
                versionRange: VersionRange.Parse(TestPackageVersion),
                targetFramework: _testTargetframework,
                nugetConfig: nugetConfigPath, additionalFeeds: new[] {emptySource});

            AssertPackageInstall(reporter, fileSystem, package, store);

            package.Uninstall();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenFailedRestoreInstallWillRollback(bool testMockBehaviorIsInSync)
        {
            var (store, installer, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync);

            Action a = () => {
                using (var t = new TransactionScope(
                    TransactionScopeOption.Required,
                    TimeSpan.Zero))
                {
                    installer.InstallPackage(new PackageId("non.existent.package.id"));

                    t.Complete();
                }
            };

            a.ShouldThrow<ToolPackageException>().WithMessage(LocalizableStrings.ToolInstallationRestoreFailed);

            AssertInstallRollBack(fileSystem, store);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenFailureAfterRestoreInstallWillRollback(bool testMockBehaviorIsInSync)
        {
            var source = GetTestLocalFeedPath();

            var (store, installer, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                feeds: GetMockFeedsForSource(source));

            void FailedStepAfterSuccessRestore() => throw new GracefulException("simulated error");

            Action a = () => {
                using (var t = new TransactionScope(
                    TransactionScopeOption.Required,
                    TimeSpan.Zero))
                {
                    installer.InstallPackage(
                        packageId: TestPackageId,
                        versionRange: VersionRange.Parse(TestPackageVersion),
                        targetFramework: _testTargetframework,
                        additionalFeeds: new[] {source});

                    FailedStepAfterSuccessRestore();
                    t.Complete();
                }
            };

            a.ShouldThrow<GracefulException>().WithMessage("simulated error");

            AssertInstallRollBack(fileSystem, store);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenSecondInstallInATransactionTheFirstInstallShouldRollback(bool testMockBehaviorIsInSync)
        {
            var source = GetTestLocalFeedPath();

            var (store, installer, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                feeds: GetMockFeedsForSource(source));

            Action a = () => {
                using (var t = new TransactionScope(
                    TransactionScopeOption.Required,
                    TimeSpan.Zero))
                {
                    Action first = () => installer.InstallPackage(
                        packageId: TestPackageId,
                        versionRange: VersionRange.Parse(TestPackageVersion),
                        targetFramework: _testTargetframework,
                        additionalFeeds: new[] {source});

                    first.ShouldNotThrow();

                    installer.InstallPackage(
                        packageId: TestPackageId,
                        versionRange: VersionRange.Parse(TestPackageVersion),
                        targetFramework: _testTargetframework,
                        additionalFeeds: new[] {source});

                    t.Complete();
                }
            };

            a.ShouldThrow<ToolPackageException>().Where(
                ex => ex.Message ==
                    string.Format(
                        CommonLocalizableStrings.ToolPackageConflictPackageId,
                        TestPackageId,
                        TestPackageVersion));

            AssertInstallRollBack(fileSystem, store);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenSecondInstallWithoutATransactionTheFirstShouldNotRollback(bool testMockBehaviorIsInSync)
        {
            var source = GetTestLocalFeedPath();

            var (store, installer, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                feeds: GetMockFeedsForSource(source));

            var package = installer.InstallPackage(
                packageId: TestPackageId,
                versionRange: VersionRange.Parse(TestPackageVersion),
                targetFramework: _testTargetframework,
                additionalFeeds: new[] {source});

            AssertPackageInstall(reporter, fileSystem, package, store);

            Action secondCall = () => installer.InstallPackage(
                packageId: TestPackageId,
                versionRange: VersionRange.Parse(TestPackageVersion),
                targetFramework: _testTargetframework,
                additionalFeeds: new[] {source});

            reporter.Lines.Should().BeEmpty();

            secondCall.ShouldThrow<ToolPackageException>().Where(
                ex => ex.Message ==
                    string.Format(
                        CommonLocalizableStrings.ToolPackageConflictPackageId,
                        TestPackageId,
                        TestPackageVersion));

            fileSystem
                .Directory
                .Exists(store.Root.WithSubDirectories(TestPackageId.ToString()).Value)
                .Should()
                .BeTrue();

            package.Uninstall();

            fileSystem
                .Directory
                .EnumerateFileSystemEntries(store.Root.WithSubDirectories(ToolPackageStore.StagingDirectory).Value)
                .Should()
                .BeEmpty();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenAnInstalledPackageUninstallRemovesThePackage(bool testMockBehaviorIsInSync)
        {
            var source = GetTestLocalFeedPath();

            var (store, installer, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                feeds: GetMockFeedsForSource(source));

            var package = installer.InstallPackage(
                packageId: TestPackageId,
                versionRange: VersionRange.Parse(TestPackageVersion),
                targetFramework: _testTargetframework,
                additionalFeeds: new[] {source});

            AssertPackageInstall(reporter, fileSystem, package, store);

            package.Uninstall();

            store.EnumeratePackages().Should().BeEmpty();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenAnInstalledPackageUninstallRollsbackWhenTransactionFails(bool testMockBehaviorIsInSync)
        {
            var source = GetTestLocalFeedPath();

            var (store, installer, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                feeds: GetMockFeedsForSource(source));

            var package = installer.InstallPackage(
                packageId: TestPackageId,
                versionRange: VersionRange.Parse(TestPackageVersion),
                targetFramework: _testTargetframework,
                additionalFeeds: new[] {source});

            AssertPackageInstall(reporter, fileSystem, package, store);

            using (var scope = new TransactionScope(
                TransactionScopeOption.Required,
                TimeSpan.Zero))
            {
                package.Uninstall();

                store.EnumeratePackages().Should().BeEmpty();
            }

            package = store.EnumeratePackageVersions(TestPackageId).First();

            AssertPackageInstall(reporter, fileSystem, package, store);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenAnInstalledPackageUninstallRemovesThePackageWhenTransactionCommits(bool testMockBehaviorIsInSync)
        {
            var source = GetTestLocalFeedPath();

            var (store, installer, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                feeds: GetMockFeedsForSource(source));

            var package = installer.InstallPackage(
                packageId: TestPackageId,
                versionRange: VersionRange.Parse(TestPackageVersion),
                targetFramework: _testTargetframework,
                additionalFeeds: new[] {source});

            AssertPackageInstall(reporter, fileSystem, package, store);

            using (var scope = new TransactionScope(
                TransactionScopeOption.Required,
                TimeSpan.Zero))
            {
                package.Uninstall();
                scope.Complete();
            }

            store.EnumeratePackages().Should().BeEmpty();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenAPackageNameWithDifferentCaseItCanInstallThePackage(bool testMockBehaviorIsInSync)
        {
            var nugetConfigPath = WriteNugetConfigFileToPointToTheFeed();

            var (store, installer, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                feeds: GetMockFeedsForConfigFile(nugetConfigPath));

            var package = installer.InstallPackage(
                packageId: new PackageId("GlObAl.TooL.coNsoLe.DemO"),
                targetFramework: _testTargetframework,
                nugetConfig: nugetConfigPath);

            AssertPackageInstall(reporter, fileSystem, package, store);

            package.Uninstall();
        }

        [Fact]
        public void GivenANuGetDiagnosticMessageItShouldNotContainTheTempProject()
        {
            var nugetConfigPath = WriteNugetConfigFileToPointToTheFeed();
            var tempProject = GetUniqueTempProjectPathEachTest();

            var (store, installer, reporter, fileSystem) = Setup(
                useMock: false,
                tempProject: tempProject);

            var package = installer.InstallPackage(
                packageId: TestPackageId,
                versionRange: VersionRange.Parse("1.0.*"),
                targetFramework: _testTargetframework,
                nugetConfig: nugetConfigPath);

            reporter.Lines.Should().NotBeEmpty();
            reporter.Lines.Should().Contain(l => l.Contains("warning"));
            reporter.Lines.Should().NotContain(l => l.Contains(tempProject.Value));
            reporter.Lines.Clear();

            AssertPackageInstall(reporter, fileSystem, package, store);

            package.Uninstall();
        }

        [Fact]
        public void GivenARootWithNonAsciiCharactorInstallSucceeds()
        {
            var nugetConfigPath = WriteNugetConfigFileToPointToTheFeed();

            var surrogate = char.ConvertFromUtf32(int.Parse("2A601", NumberStyles.HexNumber));
            string nonAscii = "ab Ṱ̺̺̕o 田中さん åä," + surrogate;

            var root = new DirectoryPath(Path.Combine(TempRoot.Root, nonAscii, Path.GetRandomFileName()));
            var reporter = new BufferedReporter();
            var fileSystem = new FileSystemWrapper();
            var store = new ToolPackageStore(root);
            var installer = new ToolPackageInstaller(
                store: store,
                projectRestorer: new ProjectRestorer(reporter),
                tempProject: GetUniqueTempProjectPathEachTest(),
                offlineFeed: new DirectoryPath("does not exist"));

            var package = installer.InstallPackage(
                packageId: TestPackageId,
                versionRange: VersionRange.Parse(TestPackageVersion),
                targetFramework: _testTargetframework,
                nugetConfig: nugetConfigPath);

            AssertPackageInstall(reporter, fileSystem, package, store);

            package.Uninstall();
        }

        private static void AssertPackageInstall(
            BufferedReporter reporter,
            IFileSystem fileSystem,
            IToolPackage package,
            IToolPackageStore store)
        {
            reporter.Lines.Should().BeEmpty();

            package.Id.Should().Be(TestPackageId);
            package.Version.ToNormalizedString().Should().Be(TestPackageVersion);
            package.PackageDirectory.Value.Should().Contain(store.Root.Value);

            store.EnumeratePackageVersions(TestPackageId)
                .Select(p => p.Version.ToNormalizedString())
                .Should()
                .Equal(TestPackageVersion);

            package.Commands.Count.Should().Be(1);
            fileSystem.File.Exists(package.Commands[0].Executable.Value).Should().BeTrue($"{package.Commands[0].Executable.Value} should exist");
            package.Commands[0].Executable.Value.Should().Contain(store.Root.Value);
        }

        private static void AssertInstallRollBack(IFileSystem fileSystem, IToolPackageStore store)
        {
            if (!fileSystem.Directory.Exists(store.Root.Value))
            {
                return;
            }

            fileSystem
                .Directory
                .EnumerateFileSystemEntries(store.Root.Value)
                .Should()
                .NotContain(e => Path.GetFileName(e) != ToolPackageStore.StagingDirectory);

            fileSystem
                .Directory
                .EnumerateFileSystemEntries(store.Root.WithSubDirectories(ToolPackageStore.StagingDirectory).Value)
                .Should()
                .BeEmpty();
        }

        private static FilePath GetUniqueTempProjectPathEachTest()
        {
            var tempProjectDirectory =
                new DirectoryPath(Path.GetTempPath()).WithSubDirectories(Path.GetRandomFileName());
            var tempProjectPath =
                tempProjectDirectory.WithFile(Path.GetRandomFileName() + ".csproj");
            return tempProjectPath;
        }

        private static IEnumerable<MockFeed> GetMockFeedsForConfigFile(FilePath nugetConfig)
        {
            return new MockFeed[]
            {
                new MockFeed
                {
                    Type = MockFeedType.ExplicitNugetConfig,
                    Uri = nugetConfig.Value,
                    Packages = new List<MockFeedPackage>
                    {
                        new MockFeedPackage
                        {
                            PackageId = TestPackageId.ToString(),
                            Version = TestPackageVersion
                        }
                    }
                }
            };
        }

        private static IEnumerable<MockFeed> GetMockFeedsForSource(string source)
        {
            return new MockFeed[]
            {
                new MockFeed
                {
                    Type = MockFeedType.ImplicitAdditionalFeed,
                    Uri = source,
                    Packages = new List<MockFeedPackage>
                    {
                        new MockFeedPackage
                        {
                            PackageId = TestPackageId.ToString(),
                            Version = TestPackageVersion
                        }
                    }
                }
            };
        }

        private static IEnumerable<MockFeed> GetOfflineMockFeed()
        {
            return new MockFeed[]
            {
                new MockFeed
                {
                    Type = MockFeedType.ImplicitAdditionalFeed,
                    Uri = GetTestLocalFeedPath(),
                    Packages = new List<MockFeedPackage>
                    {
                        new MockFeedPackage
                        {
                            PackageId = TestPackageId.ToString(),
                            Version = TestPackageVersion
                        }
                    }
                }
            };
        }

        private static (IToolPackageStore, IToolPackageInstaller, BufferedReporter, IFileSystem) Setup(
            bool useMock,
            IEnumerable<MockFeed> feeds = null,
            FilePath? tempProject = null,
            DirectoryPath? offlineFeed = null)
        {
            var root = new DirectoryPath(Path.Combine(TempRoot.Root, Path.GetRandomFileName()));
            var reporter = new BufferedReporter();

            IFileSystem fileSystem;
            IToolPackageStore store;
            IToolPackageInstaller installer;
            if (useMock)
            {
                fileSystem = new FileSystemMockBuilder().Build();
                store = new ToolPackageStoreMock(root, fileSystem);
                installer = new ToolPackageInstallerMock(
                    fileSystem: fileSystem,
                    store: store,
                    projectRestorer: new ProjectRestorerMock(
                        fileSystem: fileSystem,
                        reporter: reporter,
                        feeds: feeds));
            }
            else
            {
                fileSystem = new FileSystemWrapper();
                store = new ToolPackageStore(root);
                installer = new ToolPackageInstaller(
                    store: store,
                    projectRestorer: new ProjectRestorer(reporter),
                    tempProject: tempProject ?? GetUniqueTempProjectPathEachTest(),
                    offlineFeed: offlineFeed ?? new DirectoryPath("does not exist"));
            }

            store.Root.Value.Should().Be(Path.GetFullPath(root.Value));

            return (store, installer, reporter, fileSystem);
        }

        private static FilePath WriteNugetConfigFileToPointToTheFeed()
        {
            var nugetConfigName = "nuget.config";

            var tempPathForNugetConfigWithWhiteSpace =
                Path.Combine(Path.GetTempPath(),
                    Path.GetRandomFileName() + " " + Path.GetRandomFileName());
            Directory.CreateDirectory(tempPathForNugetConfigWithWhiteSpace);

            NuGetConfig.Write(
                directory: tempPathForNugetConfigWithWhiteSpace,
                configname: nugetConfigName,
                localFeedPath: GetTestLocalFeedPath());

            return new FilePath(Path.GetFullPath(Path.Combine(tempPathForNugetConfigWithWhiteSpace, nugetConfigName)));
        }

        private static string GetTestLocalFeedPath() => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "TestAssetLocalNugetFeed");
        private readonly string _testTargetframework = BundledTargetFramework.GetTargetFrameworkMoniker();
        private const string TestPackageVersion = "1.0.4";
        private static readonly PackageId TestPackageId = new PackageId("global.tool.console.demo");
    }
}
