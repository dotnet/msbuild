// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using FluentAssertions;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ToolManifest;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Tools.Tests.ComponentMocks;
using Microsoft.DotNet.Tools.Tool.Restore;
using Microsoft.Extensions.DependencyModel.Tests;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.NET.TestFramework.Utilities;
using NuGet.Frameworks;
using NuGet.Versioning;
using Xunit;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.Commands.Tool
{
    public class ToolRestoreCommandWithMultipleNugetConfigTests
    {
        private readonly IFileSystem _fileSystem;
        private ToolPackageInstallerMock _toolPackageInstallerMock;
        private readonly ParseResult _parseResult;
        private readonly BufferedReporter _reporter;
        private readonly ILocalToolsResolverCache _localToolsResolverCache;

        private readonly PackageId _packageIdA = new PackageId("local.tool.console.a");
        private readonly NuGetVersion _packageVersionA;
        private readonly ToolCommandName _toolCommandNameA = new ToolCommandName("a");
        private readonly PackageId _packageIdB = new PackageId("local.tool.console.B");
        private readonly NuGetVersion _packageVersionB;
        private readonly ToolCommandName _toolCommandNameB = new ToolCommandName("b");

        private string _nugetConfigUnderTestRoot;
        private string _nugetConfigUnderSubDir;

        public ToolRestoreCommandWithMultipleNugetConfigTests()
        {
            _packageVersionA = NuGetVersion.Parse("1.0.4");
            _packageVersionB = NuGetVersion.Parse("1.0.4");

            _reporter = new BufferedReporter();
            _fileSystem = new FileSystemMockBuilder().UseCurrentSystemTemporaryDirectory().Build();
            string temporaryDirectory = _fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;

            string pathToPlacePackages = Path.Combine(temporaryDirectory, "pathToPlacePackage");
            ToolPackageStoreMock toolPackageStoreMock =
                new ToolPackageStoreMock(new DirectoryPath(pathToPlacePackages), _fileSystem);

            SetupFileLayoutAndFeed(temporaryDirectory, toolPackageStoreMock);

            _parseResult = Parser.Instance.Parse("dotnet tool restore");

            _localToolsResolverCache
                = new LocalToolsResolverCache(
                    _fileSystem,
                    new DirectoryPath(Path.Combine(temporaryDirectory, "cache")),
                    1);
        }

        private void SetupFileLayoutAndFeed(string temporaryDirectory, ToolPackageStoreMock toolPackageStoreMock)
        {
            var testRoot = Path.Combine(temporaryDirectory, "testRoot");
            _fileSystem.Directory.CreateDirectory(testRoot);
            _nugetConfigUnderTestRoot = Path.Combine(testRoot, "nuget.config");
            _fileSystem.File.CreateEmptyFile(_nugetConfigUnderTestRoot);
            var subDir = Path.Combine(testRoot, "sub");
            _fileSystem.Directory.CreateDirectory(subDir);
            _nugetConfigUnderSubDir = Path.Combine(subDir, "nuget.config");
            _fileSystem.File.CreateEmptyFile(_nugetConfigUnderSubDir);

            _toolPackageInstallerMock = new ToolPackageInstallerMock(
                _fileSystem,
                toolPackageStoreMock,
                new ProjectRestorerMock(
                    _fileSystem,
                    _reporter,
                    new List<MockFeed>
                    {
                        new MockFeed
                        {
                            Type = MockFeedType.FeedFromLookUpNugetConfig,
                            Uri = _nugetConfigUnderTestRoot,
                            Packages = new List<MockFeedPackage>
                            {
                                new MockFeedPackage
                                {
                                    PackageId = _packageIdA.ToString(),
                                    Version = _packageVersionA.ToNormalizedString(),
                                    ToolCommandName = _toolCommandNameA.ToString()
                                },
                            }
                        },

                        new MockFeed
                        {
                            Type = MockFeedType.FeedFromLookUpNugetConfig,
                            Uri = _nugetConfigUnderSubDir,
                            Packages = new List<MockFeedPackage>
                            {
                                new MockFeedPackage
                                {
                                    PackageId = _packageIdB.ToString(),
                                    Version = _packageVersionB.ToNormalizedString(),
                                    ToolCommandName = _toolCommandNameB.ToString()
                                },
                            }
                        }
                    }));
        }

        [Fact]
        public void WhenManifestPackageAreFromDifferentDirectoryItCanFindTheRightNugetConfigAndSaveToCache()
        {
            IToolManifestFinder manifestFinder =
                new MockManifestFinder(new[]
                {
                    new ToolManifestPackage(_packageIdA, _packageVersionA,
                        new[] {_toolCommandNameA},
                        new DirectoryPath(Path.GetDirectoryName(_nugetConfigUnderTestRoot))),
                    new ToolManifestPackage(_packageIdB, _packageVersionB,
                        new[] {_toolCommandNameB},
                        new DirectoryPath(Path.GetDirectoryName(_nugetConfigUnderSubDir)))
                });

            ToolRestoreCommand toolRestoreCommand = new ToolRestoreCommand(_parseResult,
                _toolPackageInstallerMock,
                manifestFinder,
                _localToolsResolverCache,
                _fileSystem,
                _reporter
            );

            toolRestoreCommand.Execute().Should()
                .Be(0, "if nuget probed from sub dir, it will find only the nuget.config under sub dir. " +
                       "And it does not have the feed to package A. However, since package A is set in " +
                       "manifest file under repository root, nuget should prob from manifest file directory " +
                       "and there is another nuget.config set beside the manifest file under repository root");

            _localToolsResolverCache.TryLoad(
                    new RestoredCommandIdentifier(
                        _packageIdA,
                        _packageVersionA,
                        NuGetFramework.Parse(BundledTargetFramework.GetTargetFrameworkMoniker()),
                        Constants.AnyRid,
                        _toolCommandNameA), out RestoredCommand _)
                .Should().BeTrue();

            _localToolsResolverCache.TryLoad(
                    new RestoredCommandIdentifier(
                        _packageIdB,
                        _packageVersionB,
                        NuGetFramework.Parse(BundledTargetFramework.GetTargetFrameworkMoniker()),
                        Constants.AnyRid,
                        _toolCommandNameB), out RestoredCommand _)
                .Should().BeTrue();
        }

        private class MockManifestFinder : IToolManifestFinder
        {
            private readonly IReadOnlyCollection<ToolManifestPackage> _toReturn;

            public MockManifestFinder(IReadOnlyCollection<ToolManifestPackage> toReturn)
            {
                _toReturn = toReturn;
            }

            public IReadOnlyCollection<ToolManifestPackage> Find(FilePath? filePath = null)
            {
                return _toReturn;
            }

            public FilePath FindFirst()
            {
                throw new NotImplementedException();
            }

            public IReadOnlyList<FilePath> FindByPackageId(PackageId packageId)
            {
                throw new NotImplementedException();
            }
        }
    }
}
