// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
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
using NuGet.Versioning;
using Xunit;
using Microsoft.NET.TestFramework.Utilities;
using Microsoft.NET.TestFramework;
using Xunit.Abstractions;

namespace Microsoft.DotNet.ToolPackage.Tests
{
    public class ToolPackageUninstallerTests : SdkTest
    {
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

            package.PackagedShims.Should().ContainSingle(f => f.Value.Contains("demo.exe") || f.Value.Contains("demo"));

            uninstaller.Uninstall(package.PackageDirectory);

            storeQuery.EnumeratePackages().Should().BeEmpty();
        }

        private static FilePath GetUniqueTempProjectPathEachTest()
        {
            var tempProjectDirectory =
                new DirectoryPath(Path.GetTempPath()).WithSubDirectories(Path.GetRandomFileName());
            var tempProjectPath =
                tempProjectDirectory.WithFile(Path.GetRandomFileName() + ".csproj");
            return tempProjectPath;
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

        private (IToolPackageStore, IToolPackageStoreQuery, IToolPackageInstaller, IToolPackageUninstaller, BufferedReporter, IFileSystem
            ) Setup(
                bool useMock,
                List<MockFeed> feeds = null,
                FilePath? tempProject = null,
                DirectoryPath? offlineFeed = null)
        {
            var root = new DirectoryPath(_testAssetsManager.CreateTestDirectory("root").Path);
            var reporter = new BufferedReporter();

            IFileSystem fileSystem;
            IToolPackageStore store;
            IToolPackageStoreQuery storeQuery;
            IToolPackageInstaller installer;
            IToolPackageUninstaller uninstaller;
            if (useMock)
            {
                var packagedShimsMap = new Dictionary<PackageId, IReadOnlyList<FilePath>>
                {
                    [TestPackageId] = new FilePath[] { new FilePath("path/demo.exe") }
                };

                fileSystem = new FileSystemMockBuilder().Build();
                var toolPackageStoreMock = new ToolPackageStoreMock(root, fileSystem);
                store = toolPackageStoreMock;
                storeQuery = toolPackageStoreMock;
                installer = new ToolPackageInstallerMock(
                    fileSystem: fileSystem,
                    store: toolPackageStoreMock,
                    projectRestorer: new ProjectRestorerMock(
                        fileSystem: fileSystem,
                        reporter: reporter,
                        feeds: feeds),
                     packagedShimsMap: packagedShimsMap);
                uninstaller = new ToolPackageUninstallerMock(fileSystem, toolPackageStoreMock);
            }
            else
            {
                fileSystem = new FileSystemWrapper();
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

        private static string GetTestLocalFeedPath() =>
            Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "TestAssetLocalNugetFeed");

        private readonly string _testTargetframework = BundledTargetFramework.GetTargetFrameworkMoniker();
        private const string TestPackageVersion = "1.0.4";
        private static readonly PackageId TestPackageId = new PackageId("global.tool.console.demo.with.shim");

        public ToolPackageUninstallerTests(ITestOutputHelper log) : base(log)
        {
        }
    }
}
