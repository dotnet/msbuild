// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Transactions;
using System.Threading;
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
using System.Runtime.CompilerServices;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit.Abstractions;
using Microsoft.NET.TestFramework.Utilities;

namespace Microsoft.DotNet.ToolPackage.Tests
{
    public class ToolPackageInstallerTests : SdkTest
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenNoFeedInstallFailsWithException(bool testMockBehaviorIsInSync)
        {
            var (store, storeQuery, installer, uninstaller, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                feeds: new List<MockFeed>());

            Action a = () => installer.InstallPackage(new PackageLocation(), packageId: TestPackageId,
                versionRange: VersionRange.Parse(TestPackageVersion), targetFramework: _testTargetframework);

            a.ShouldThrow<ToolPackageException>().WithMessage(LocalizableStrings.ToolInstallationRestoreFailed);

            reporter.Lines.Count.Should().Be(1);
            reporter.Lines[0].Should().Contain(TestPackageId.ToString());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenOfflineFeedInstallSucceeds(bool testMockBehaviorIsInSync)
        {
            var (store, storeQuery, installer, uninstaller, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                offlineFeed: new DirectoryPath(GetTestLocalFeedPath()),
                feeds: GetOfflineMockFeed());

            var package = installer.InstallPackage(new PackageLocation(), packageId: TestPackageId,
                versionRange: VersionRange.Parse(TestPackageVersion), targetFramework: _testTargetframework);

            AssertPackageInstall(reporter, fileSystem, package, store, storeQuery);

            uninstaller.Uninstall(package.PackageDirectory);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenAEmptySourceAndOfflineFeedInstallSucceeds(bool testMockBehaviorIsInSync)
        {
            var emptySource = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(emptySource);

            var (store, storeQuery, installer, uninstaller, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                offlineFeed: new DirectoryPath(GetTestLocalFeedPath()),
                feeds: GetOfflineMockFeed());

            var package = installer.InstallPackage(new PackageLocation(additionalFeeds: new[] { emptySource }),
                packageId: TestPackageId,
                versionRange: VersionRange.Parse(TestPackageVersion),
                targetFramework: _testTargetframework);

            AssertPackageInstall(reporter, fileSystem, package, store, storeQuery);

            uninstaller.Uninstall(package.PackageDirectory);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenNugetConfigInstallSucceeds(bool testMockBehaviorIsInSync)
        {
            var nugetConfigPath = GenerateRandomNugetConfigFilePath();

            var (store, storeQuery, installer, uninstaller, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                writeLocalFeedToNugetConfig: nugetConfigPath);

            var package = installer.InstallPackage(new PackageLocation(nugetConfig: nugetConfigPath),
                packageId: TestPackageId,
                versionRange: VersionRange.Parse(TestPackageVersion),
                targetFramework: _testTargetframework);

            AssertPackageInstall(reporter, fileSystem, package, store, storeQuery);

            uninstaller.Uninstall(package.PackageDirectory);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenNugetConfigInstallSucceedsInTransaction(bool testMockBehaviorIsInSync)
        {
            var nugetConfigPath = GenerateRandomNugetConfigFilePath();

            var (store, storeQuery, installer, uninstaller, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                writeLocalFeedToNugetConfig: nugetConfigPath);

            IToolPackage package = null;
            using (var transactionScope = new TransactionScope(
                TransactionScopeOption.Required,
                TimeSpan.Zero))
            {
                package = installer.InstallPackage(new PackageLocation(nugetConfig: nugetConfigPath),
                    packageId: TestPackageId,
                    versionRange: VersionRange.Parse(TestPackageVersion),
                    targetFramework: _testTargetframework);

                transactionScope.Complete();
            }

            AssertPackageInstall(reporter, fileSystem, package, store, storeQuery);

            uninstaller.Uninstall(package.PackageDirectory);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenNugetConfigInstallCreatesAnAssetFile(bool testMockBehaviorIsInSync)
        {
            var nugetConfigPath = GenerateRandomNugetConfigFilePath();

            var (store, storeQuery, installer, uninstaller, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                writeLocalFeedToNugetConfig: nugetConfigPath);

            var package = installer.InstallPackage(new PackageLocation(nugetConfig: nugetConfigPath),
                packageId: TestPackageId,
                versionRange: VersionRange.Parse(TestPackageVersion),
                targetFramework: _testTargetframework);

            AssertPackageInstall(reporter, fileSystem, package, store, storeQuery);

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

            uninstaller.Uninstall(package.PackageDirectory);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenAConfigFileRootDirectoryPackageInstallSucceedsViaFindingNugetConfigInParentDir(
            bool testMockBehaviorIsInSync)
        {
            var nugetConfigPath = GenerateRandomNugetConfigFilePath();
            var subDirUnderNugetConfigPath = nugetConfigPath.GetDirectoryPath().WithSubDirectories("sub");

            var onlyNugetConfigInParentDirHasPackagesFeed = new List<MockFeed>
            {
                new MockFeed
                {
                    Type = MockFeedType.FeedFromLookUpNugetConfig,
                    Uri = nugetConfigPath.Value,
                    Packages = new List<MockFeedPackage>
                    {
                        new MockFeedPackage
                        {
                            PackageId = TestPackageId.ToString(),
                            Version = TestPackageVersion,
                            ToolCommandName = "SimulatorCommand"
                        }
                    }
                }
            };

            var (store, storeQuery, installer, uninstaller, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                writeLocalFeedToNugetConfig: nugetConfigPath,
                feeds: onlyNugetConfigInParentDirHasPackagesFeed);

            fileSystem.Directory.CreateDirectory(subDirUnderNugetConfigPath.Value);

            var package = installer.InstallPackage(
                new PackageLocation(rootConfigDirectory: subDirUnderNugetConfigPath),
                packageId: TestPackageId,
                versionRange: VersionRange.Parse(TestPackageVersion),
                targetFramework: _testTargetframework);

            AssertPackageInstall(reporter, fileSystem, package, store, storeQuery);

            uninstaller.Uninstall(package.PackageDirectory);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenAllButNoPackageVersionItCanInstallThePackage(bool testMockBehaviorIsInSync)
        {
            var nugetConfigPath = GenerateRandomNugetConfigFilePath();

            var (store, storeQuery, installer, uninstaller, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                writeLocalFeedToNugetConfig: nugetConfigPath);

            var package = installer.InstallPackage(
                new PackageLocation(nugetConfig: nugetConfigPath),
                packageId: TestPackageId,
                targetFramework: _testTargetframework);

            AssertPackageInstall(reporter, fileSystem, package, store, storeQuery);

            uninstaller.Uninstall(package.PackageDirectory);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenAllButNoTargetFrameworkItCanDownloadThePackage(bool testMockBehaviorIsInSync)
        {
            var nugetConfigPath = GenerateRandomNugetConfigFilePath();

            var (store, storeQuery, installer, uninstaller, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                writeLocalFeedToNugetConfig: nugetConfigPath);

            var package = installer.InstallPackage(new PackageLocation(nugetConfig: nugetConfigPath),
                packageId: TestPackageId,
                versionRange: VersionRange.Parse(TestPackageVersion));

            AssertPackageInstall(reporter, fileSystem, package, store, storeQuery);

            uninstaller.Uninstall(package.PackageDirectory);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenASourceInstallSucceeds(bool testMockBehaviorIsInSync)
        {
            var source = GetTestLocalFeedPath();

            var (store, storeQuery, installer, uninstaller, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                feeds: GetMockFeedsForSource(source));

            var package = installer.InstallPackage(new PackageLocation(additionalFeeds: new[] { source }),
                packageId: TestPackageId,
                versionRange: VersionRange.Parse(TestPackageVersion),
                targetFramework: _testTargetframework);

            AssertPackageInstall(reporter, fileSystem, package, store, storeQuery);

            uninstaller.Uninstall(package.PackageDirectory);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenARelativeSourcePathInstallSucceeds(bool testMockBehaviorIsInSync)
        {
            var source = GetTestLocalFeedPath();

            var (store, storeQuery, installer, uninstaller, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                feeds: GetMockFeedsForSource(source));

            var package = installer.InstallPackage(
                new PackageLocation(additionalFeeds: new[]
                    {Path.GetRelativePath(Directory.GetCurrentDirectory(), source)}), packageId: TestPackageId,
                versionRange: VersionRange.Parse(TestPackageVersion),
                targetFramework: _testTargetframework);

            AssertPackageInstall(reporter, fileSystem, package, store, storeQuery);

            uninstaller.Uninstall(package.PackageDirectory);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenAUriSourceInstallSucceeds(bool testMockBehaviorIsInSync)
        {
            var source = GetTestLocalFeedPath();

            var (store, storeQuery, installer, uninstaller, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                feeds: GetMockFeedsForSource(source));

            var package = installer.InstallPackage(
                new PackageLocation(additionalFeeds: new[] { new Uri(source).AbsoluteUri }), packageId: TestPackageId,
                versionRange: VersionRange.Parse(TestPackageVersion),
                targetFramework: _testTargetframework);

            AssertPackageInstall(reporter, fileSystem, package, store, storeQuery);

            uninstaller.Uninstall(package.PackageDirectory);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenAEmptySourceAndNugetConfigInstallSucceeds(bool testMockBehaviorIsInSync)
        {
            var nugetConfigPath = GenerateRandomNugetConfigFilePath();
            var emptySource = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(emptySource);

            var (store, storeQuery, installer, uninstaller, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                writeLocalFeedToNugetConfig: nugetConfigPath);

            var package = installer.InstallPackage(new PackageLocation(nugetConfig: nugetConfigPath,
                    additionalFeeds: new[] { emptySource }),
                packageId: TestPackageId,
                versionRange: VersionRange.Parse(TestPackageVersion),
                targetFramework: _testTargetframework);

            AssertPackageInstall(reporter, fileSystem, package, store, storeQuery);

            uninstaller.Uninstall(package.PackageDirectory);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenFailedRestoreInstallWillRollback(bool testMockBehaviorIsInSync)
        {
            var (store, storeQuery, installer, uninstaller, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync);

            Action a = () =>
            {
                using (var t = new TransactionScope(
                    TransactionScopeOption.Required,
                    TimeSpan.Zero))
                {
                    installer.InstallPackage(new PackageLocation(), new PackageId("non.existent.package.id"));

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

            var (store, storeQuery, installer, uninstaller, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                feeds: GetMockFeedsForSource(source));

            void FailedStepAfterSuccessRestore() => throw new GracefulException("simulated error");

            Action a = () =>
            {
                using (var t = new TransactionScope(
                    TransactionScopeOption.Required,
                    TimeSpan.Zero))
                {
                    installer.InstallPackage(new PackageLocation(additionalFeeds: new[] { source }),
                        packageId: TestPackageId,
                        versionRange: VersionRange.Parse(TestPackageVersion),
                        targetFramework: _testTargetframework);

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

            var (store, storeQuery, installer, uninstaller, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                feeds: GetMockFeedsForSource(source));

            Action a = () =>
            {
                using (var t = new TransactionScope(
                    TransactionScopeOption.Required,
                    TimeSpan.Zero))
                {
                    Action first = () => installer.InstallPackage(new PackageLocation(additionalFeeds: new[] { source }),
                        packageId: TestPackageId,
                        versionRange: VersionRange.Parse(TestPackageVersion),
                        targetFramework: _testTargetframework);

                    first.ShouldNotThrow();

                    installer.InstallPackage(new PackageLocation(additionalFeeds: new[] { source }),
                        packageId: TestPackageId,
                        versionRange: VersionRange.Parse(TestPackageVersion),
                        targetFramework: _testTargetframework);

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

            var (store, storeQuery, installer, uninstaller, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                feeds: GetMockFeedsForSource(source));

            var package = installer.InstallPackage(new PackageLocation(additionalFeeds: new[] { source }),
                packageId: TestPackageId,
                versionRange: VersionRange.Parse(TestPackageVersion),
                targetFramework: _testTargetframework);

            AssertPackageInstall(reporter, fileSystem, package, store, storeQuery);

            Action secondCall = () => installer.InstallPackage(new PackageLocation(additionalFeeds: new[] { source }),
                packageId: TestPackageId,
                versionRange: VersionRange.Parse(TestPackageVersion),
                targetFramework: _testTargetframework);

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

            uninstaller.Uninstall(package.PackageDirectory);

            fileSystem
                .Directory
                .EnumerateFileSystemEntries(store.Root.WithSubDirectories(ToolPackageStoreAndQuery.StagingDirectory).Value)
                .Should()
                .BeEmpty();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenAnInstalledPackageUninstallRemovesThePackage(bool testMockBehaviorIsInSync)
        {
            var source = GetTestLocalFeedPath();

            var (store, storeQuery, installer, uninstaller, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                feeds: GetMockFeedsForSource(source));

            var package = installer.InstallPackage(new PackageLocation(additionalFeeds: new[] { source }),
                packageId: TestPackageId,
                versionRange: VersionRange.Parse(TestPackageVersion),
                targetFramework: _testTargetframework);

            AssertPackageInstall(reporter, fileSystem, package, store, storeQuery);

            uninstaller.Uninstall(package.PackageDirectory);

            storeQuery.EnumeratePackages().Should().BeEmpty();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenAnInstalledPackageUninstallRollsbackWhenTransactionFails(bool testMockBehaviorIsInSync)
        {
            var source = GetTestLocalFeedPath();

            var (store, storeQuery, installer, uninstaller, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                feeds: GetMockFeedsForSource(source));

            var package = installer.InstallPackage(
                new PackageLocation(additionalFeeds: new[] { source }),
                packageId: TestPackageId,
                versionRange: VersionRange.Parse(TestPackageVersion),
                targetFramework: _testTargetframework);

            AssertPackageInstall(reporter, fileSystem, package, store, storeQuery);

            using (var scope = new TransactionScope(
                TransactionScopeOption.Required,
                TimeSpan.Zero))
            {
                uninstaller.Uninstall(package.PackageDirectory);

                storeQuery.EnumeratePackages().Should().BeEmpty();
            }

            package = storeQuery.EnumeratePackageVersions(TestPackageId).First();

            AssertPackageInstall(reporter, fileSystem, package, store, storeQuery);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenAnInstalledPackageUninstallRemovesThePackageWhenTransactionCommits(
            bool testMockBehaviorIsInSync)
        {
            var source = GetTestLocalFeedPath();

            var (store, storeQuery, installer, uninstaller, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                feeds: GetMockFeedsForSource(source));

            var package = installer.InstallPackage(new PackageLocation(additionalFeeds: new[] { source }),
                packageId: TestPackageId,
                versionRange: VersionRange.Parse(TestPackageVersion),
                targetFramework: _testTargetframework);

            AssertPackageInstall(reporter, fileSystem, package, store, storeQuery);

            using (var scope = new TransactionScope(
                TransactionScopeOption.Required,
                TimeSpan.Zero))
            {
                uninstaller.Uninstall(package.PackageDirectory);
                scope.Complete();
            }

            storeQuery.EnumeratePackages().Should().BeEmpty();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenAPackageNameWithDifferentCaseItCanInstallThePackage(bool testMockBehaviorIsInSync)
        {
            var nugetConfigPath = GenerateRandomNugetConfigFilePath();

            var (store, storeQuery, installer, uninstaller, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                writeLocalFeedToNugetConfig: nugetConfigPath);

            var package = installer.InstallPackage(new PackageLocation(nugetConfig: nugetConfigPath),
                packageId: new PackageId("GlObAl.TooL.coNsoLe.DemO"),
                targetFramework: _testTargetframework);

            AssertPackageInstall(reporter, fileSystem, package, store, storeQuery);

            uninstaller.Uninstall(package.PackageDirectory);
        }

        [Fact]
        public void GivenANuGetDiagnosticMessageItShouldNotContainTheTempProject()
        {
            var nugetConfigPath = GenerateRandomNugetConfigFilePath();
            var tempProject = GetUniqueTempProjectPathEachTest();

            var (store, storeQuery, installer, uninstaller, reporter, fileSystem) = Setup(
                useMock: false,
                tempProject: tempProject,
                writeLocalFeedToNugetConfig: nugetConfigPath);

            var package = installer.InstallPackage(new PackageLocation(nugetConfig: nugetConfigPath),
                packageId: TestPackageId,
                versionRange: VersionRange.Parse("1.0.0"),
                targetFramework: _testTargetframework);

            reporter.Lines.Should().NotBeEmpty();
            reporter.Lines.Should().Contain(l => l.Contains("warning"));
            reporter.Lines.Should().NotContain(l => l.Contains(tempProject.Value));
            reporter.Lines.Clear();

            AssertPackageInstall(reporter, fileSystem, package, store, storeQuery);

            uninstaller.Uninstall(package.PackageDirectory);
        }

        [Fact]
        public void GivenARootWithNonAsciiCharacterInstallSucceeds()
        {
            var nugetConfigPath = GenerateRandomNugetConfigFilePath();

            var surrogate = char.ConvertFromUtf32(int.Parse("2A601", NumberStyles.HexNumber));
            string nonAscii = "ab Ṱ̺̺̕o 田中さん åä," + surrogate;

            var root = _testAssetsManager.CreateTestDirectory(testName: nonAscii, identifier: "root");
            var reporter = new BufferedReporter();
            var fileSystem = new FileSystemWrapper();
            var store = new ToolPackageStoreAndQuery(new DirectoryPath(root.Path));
            WriteNugetConfigFileToPointToTheFeed(fileSystem, nugetConfigPath);
            var installer = new ToolPackageInstaller(
                store: store,
                projectRestorer: new Stage2ProjectRestorer(Log, reporter),
                tempProject: GetUniqueTempProjectPathEachTest(),
                offlineFeed: new DirectoryPath("does not exist"));

            var package = installer.InstallPackage(new PackageLocation(nugetConfig: nugetConfigPath),
                packageId: TestPackageId,
                versionRange: VersionRange.Parse(TestPackageVersion),
                targetFramework: _testTargetframework);

            AssertPackageInstall(reporter, fileSystem, package, store, store);

            new ToolPackageUninstaller(store).Uninstall(package.PackageDirectory);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        // repro https://github.com/dotnet/cli/issues/9409
        public void GivenAComplexVersionRangeInstallSucceeds(bool testMockBehaviorIsInSync)
        {
            var nugetConfigPath = GenerateRandomNugetConfigFilePath();
            var emptySource = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(emptySource);

            var (store, storeQuery, installer, uninstaller, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                writeLocalFeedToNugetConfig: nugetConfigPath);

            var package = installer.InstallPackage(new PackageLocation(nugetConfig: nugetConfigPath,
                    additionalFeeds: new[] { emptySource }),
                packageId: TestPackageId,
                versionRange: VersionRange.Parse("1.0.0-rc*"),
                targetFramework: _testTargetframework);

            AssertPackageInstall(reporter, fileSystem, package, store, storeQuery);

            uninstaller.Uninstall(package.PackageDirectory);
        }

        [UnixOnlyTheory]
        [InlineData(false)]
        [InlineData(true)]
        // repro https://github.com/dotnet/cli/issues/10101
        public void GivenAPackageWithCasingAndenUSPOSIXInstallSucceeds(bool testMockBehaviorIsInSync)
        {
            var nugetConfigPath = GenerateRandomNugetConfigFilePath();
            var emptySource = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(emptySource);

            var packageId = new PackageId("Global.Tool.Console.Demo.With.Casing");
            var packageVersion = "2.0.4";
            var feed = new MockFeed
            {
                Type = MockFeedType.ImplicitAdditionalFeed,
                Uri = nugetConfigPath.Value,
                Packages = new List<MockFeedPackage>
                    {
                        new MockFeedPackage
                        {
                            PackageId = packageId.ToString(),
                            Version = packageVersion,
                            ToolCommandName = "DemoWithCasing",
                        }
                    }
            };

            var (store, storeQuery, installer, uninstaller, reporter, fileSystem) = Setup(
                useMock: testMockBehaviorIsInSync,
                feeds: new List<MockFeed> { feed },
                writeLocalFeedToNugetConfig: nugetConfigPath);

            CultureInfo currentCultureBefore = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("en-US-POSIX");
                IToolPackage package = null;
                Action action = () => package = installer.InstallPackage(
                    new PackageLocation(
                        nugetConfig: nugetConfigPath,
                        additionalFeeds: new[] { emptySource }),
                    packageId: packageId,
                    versionRange: VersionRange.Parse(packageVersion),
                    targetFramework: _testTargetframework);

                action.ShouldNotThrow<ToolConfigurationException>();

                fileSystem.File.Exists(package.Commands[0].Executable.Value).Should().BeTrue($"{package.Commands[0].Executable.Value} should exist");

                uninstaller.Uninstall(package.PackageDirectory);
            }
            finally
            {
                CultureInfo.CurrentCulture = currentCultureBefore;
            }
        }

        private static void AssertPackageInstall(
            BufferedReporter reporter,
            IFileSystem fileSystem,
            IToolPackage package,
            IToolPackageStore store,
            IToolPackageStoreQuery storeQuery)
        {
            reporter.Lines.Should().BeEmpty();

            package.Id.Should().Be(TestPackageId);
            package.Version.ToNormalizedString().Should().Be(TestPackageVersion);
            package.PackageDirectory.Value.Should().Contain(store.Root.Value);

            storeQuery.EnumeratePackageVersions(TestPackageId)
                .Select(p => p.Version.ToNormalizedString())
                .Should()
                .Equal(TestPackageVersion);

            package.Commands.Count.Should().Be(1);
            fileSystem.File.Exists(package.Commands[0].Executable.Value).Should()
                .BeTrue($"{package.Commands[0].Executable.Value} should exist");
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
                .NotContain(e => Path.GetFileName(e) != ToolPackageStoreAndQuery.StagingDirectory);

            fileSystem
                .Directory
                .EnumerateFileSystemEntries(store.Root.WithSubDirectories(ToolPackageStoreAndQuery.StagingDirectory).Value)
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

        private static List<MockFeed> GetMockFeedsForConfigFile(FilePath? nugetConfig)
        {
            if (nugetConfig.HasValue)
            {
                return new List<MockFeed>
                {
                    new MockFeed
                    {
                        Type = MockFeedType.ExplicitNugetConfig,
                        Uri = nugetConfig.Value.Value,
                        Packages = new List<MockFeedPackage>
                        {
                            new MockFeedPackage
                            {
                                PackageId = TestPackageId.ToString(),
                                Version = TestPackageVersion,
                                ToolCommandName = "SimulatorCommand"
                            }
                        }
                    }
                };
            }
            else
            {
                return new List<MockFeed>();
            }
        }

        private static List<MockFeed> GetMockFeedsForSource(string source)
        {
            return new List<MockFeed>
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
                            Version = TestPackageVersion,
                            ToolCommandName = "SimulatorCommand"
                        }
                    }
                }
            };
        }

        private static List<MockFeed> GetOfflineMockFeed()
        {
            return new List<MockFeed>
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
                            Version = TestPackageVersion,
                            ToolCommandName = "SimulatorCommand"
                        }
                    }
                }
            };
        }

        private (IToolPackageStore, IToolPackageStoreQuery, IToolPackageInstaller, IToolPackageUninstaller, BufferedReporter, IFileSystem
            ) Setup(
                bool useMock,
                List<MockFeed> feeds = null,
                FilePath? tempProject = null,
                DirectoryPath? offlineFeed = null,
                FilePath? writeLocalFeedToNugetConfig = null,
                [CallerMemberName] string callingMethod = "")
        {
            var root = new DirectoryPath(_testAssetsManager.CreateTestDirectory(callingMethod, identifier: useMock.ToString()).Path);
            var reporter = new BufferedReporter();

            IFileSystem fileSystem;
            IToolPackageStore store;
            IToolPackageStoreQuery storeQuery;
            IToolPackageInstaller installer;
            IToolPackageUninstaller uninstaller;
            if (useMock)
            {
                fileSystem = new FileSystemMockBuilder().Build();
                WriteNugetConfigFileToPointToTheFeed(fileSystem, writeLocalFeedToNugetConfig);
                var toolPackageStoreMock = new ToolPackageStoreMock(root, fileSystem);
                store = toolPackageStoreMock;
                storeQuery = toolPackageStoreMock;
                installer = new ToolPackageInstallerMock(
                    fileSystem: fileSystem,
                    store: toolPackageStoreMock,
                    projectRestorer: new ProjectRestorerMock(
                        fileSystem: fileSystem,
                        reporter: reporter,
                        feeds: feeds == null
                            ? GetMockFeedsForConfigFile(writeLocalFeedToNugetConfig)
                            : feeds.Concat(GetMockFeedsForConfigFile(writeLocalFeedToNugetConfig)).ToList()));
                uninstaller = new ToolPackageUninstallerMock(fileSystem, toolPackageStoreMock);
            }
            else
            {
                fileSystem = new FileSystemWrapper();
                WriteNugetConfigFileToPointToTheFeed(fileSystem, writeLocalFeedToNugetConfig);
                var toolPackageStore = new ToolPackageStoreAndQuery(root);
                store = toolPackageStore;
                storeQuery = toolPackageStore;
                installer = new ToolPackageInstaller(
                    store: store,
                    projectRestorer: new Stage2ProjectRestorer(Log, reporter),
                    tempProject: tempProject ?? GetUniqueTempProjectPathEachTest(),
                    offlineFeed: offlineFeed ?? new DirectoryPath("does not exist"));
                uninstaller = new ToolPackageUninstaller(store);
            }

            store.Root.Value.Should().Be(Path.GetFullPath(root.Value));

            return (store, storeQuery, installer, uninstaller, reporter, fileSystem);
        }

        private static void WriteNugetConfigFileToPointToTheFeed(IFileSystem fileSystem, FilePath? filePath)
        {
            if (!filePath.HasValue) return;

            fileSystem.Directory.CreateDirectory(filePath.Value.GetDirectoryPath().Value);

            fileSystem.File.WriteAllText(filePath.Value.Value, FormatNuGetConfig(
                localFeedPath: GetTestLocalFeedPath()));
        }

        public static string FormatNuGetConfig(string localFeedPath)
        {
            const string template = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
<packageSources>
<!--To inherit the global NuGet package sources remove the <clear/> line below -->
<clear />
<add key=""Test Source"" value=""{0}"" />
<add key=""api.nuget.org"" value=""https://api.nuget.org/v3/index.json"" />
<add key=""dotnet-core"" value=""https://dotnet.myget.org/F/dotnet-core/api/v3/index.json"" />
</packageSources>
</configuration>";
            return string.Format(template, localFeedPath);
        }

        private static FilePath GenerateRandomNugetConfigFilePath()
        {
            const string nugetConfigName = "nuget.config";
            var tempPathForNugetConfigWithWhiteSpace =
                Path.Combine(Path.GetTempPath(),
                    Path.GetRandomFileName() + " " + Path.GetRandomFileName());

            FilePath nugetConfigFullPath =
                new FilePath(Path.GetFullPath(Path.Combine(tempPathForNugetConfigWithWhiteSpace, nugetConfigName)));
            return nugetConfigFullPath;
        }

        private static string GetTestLocalFeedPath() =>
            Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "TestAssetLocalNugetFeed");

        private readonly string _testTargetframework = BundledTargetFramework.GetTargetFrameworkMoniker();
        private const string TestPackageVersion = "1.0.4";
        private static readonly PackageId TestPackageId = new PackageId("global.tool.console.demo");

        public ToolPackageInstallerTests(ITestOutputHelper log) : base(log)
        {
        }
    }
}
