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

namespace Microsoft.DotNet.ToolPackage.Tests
{
    public class ToolPackageInstanceTests : TestBase
    {
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

            package.PackagedShims.Should().ContainSingle(f => f.Value.Contains("demo.exe") || f.Value.Contains("demo"));

            package.Uninstall();
        }

        private static FilePath GetUniqueTempProjectPathEachTest()
        {
            var tempProjectDirectory =
                new DirectoryPath(Path.GetTempPath()).WithSubDirectories(Path.GetRandomFileName());
            var tempProjectPath =
                tempProjectDirectory.WithFile(Path.GetRandomFileName() + ".csproj");
            return tempProjectPath;
        }

        private static IEnumerable<MockFeed> GetMockFeedsForSource(string source)
        {
            return new[]
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
                var packagedShimsMap = new Dictionary<PackageId, IReadOnlyList<FilePath>>
                {
                    [TestPackageId] = new FilePath[] {new FilePath("path/demo.exe")}
                };

                fileSystem = new FileSystemMockBuilder().Build();
                store = new ToolPackageStoreMock(root, fileSystem);
                installer = new ToolPackageInstallerMock(
                    fileSystem: fileSystem,
                    store: store,
                    projectRestorer: new ProjectRestorerMock(
                        fileSystem: fileSystem,
                        reporter: reporter,
                        feeds: feeds),
                    packagedShimsMap: packagedShimsMap);
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

        private static string GetTestLocalFeedPath() =>
            Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "TestAssetLocalNugetFeed");

        private readonly string _testTargetframework = BundledTargetFramework.GetTargetFrameworkMoniker();
        private const string TestPackageVersion = "1.0.4";
        private static readonly PackageId TestPackageId = new PackageId("global.tool.console.demo.with.shim");
    }
}
