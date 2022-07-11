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
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.Restore.LocalizableStrings;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.Commands.Tool
{
    public class ToolRestoreCommandTests
    {
        private readonly IFileSystem _fileSystem;
        private readonly IToolPackageStore _toolPackageStore;
        private readonly ToolPackageInstallerMock _toolPackageInstallerMock;
        private readonly ParseResult _parseResult;
        private readonly BufferedReporter _reporter;
        private readonly string _temporaryDirectory;
        private readonly string _pathToPlacePackages;
        private readonly ILocalToolsResolverCache _localToolsResolverCache;
        private readonly PackageId _packageIdA = new PackageId("local.tool.console.a");

        private readonly PackageId _packageIdWithCommandNameCollisionWithA =
            new PackageId("command.name.collision.with.package.a");

        private readonly NuGetVersion _packageVersionWithCommandNameCollisionWithA;
        private readonly NuGetVersion _packageVersionA;
        private readonly ToolCommandName _toolCommandNameA = new ToolCommandName("a");

        private readonly PackageId _packageIdB = new PackageId("local.tool.console.B");
        private readonly NuGetVersion _packageVersionB;
        private readonly ToolCommandName _toolCommandNameB = new ToolCommandName("b");
        private readonly DirectoryPath _nugetGlobalPackagesFolder;

        private int _installCalledCount = 0;

        public ToolRestoreCommandTests()
        {
            _packageVersionA = NuGetVersion.Parse("1.0.4");
            _packageVersionWithCommandNameCollisionWithA = NuGetVersion.Parse("1.0.9");
            _packageVersionB = NuGetVersion.Parse("1.0.4");

            _reporter = new BufferedReporter();
            _fileSystem = new FileSystemMockBuilder().UseCurrentSystemTemporaryDirectory().Build();
            _nugetGlobalPackagesFolder = new DirectoryPath(NuGetGlobalPackagesFolder.GetLocation());
            _temporaryDirectory = _fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;
            _pathToPlacePackages = Path.Combine(_temporaryDirectory, "pathToPlacePackage");
            ToolPackageStoreMock toolPackageStoreMock =
                new ToolPackageStoreMock(new DirectoryPath(_pathToPlacePackages), _fileSystem);
            _toolPackageStore = toolPackageStoreMock;
            _toolPackageInstallerMock = new ToolPackageInstallerMock(
                _fileSystem,
                _toolPackageStore,
                new ProjectRestorerMock(
                    _fileSystem,
                    _reporter,
                    new List<MockFeed>
                    {
                        new MockFeed
                        {
                            Type = MockFeedType.ImplicitAdditionalFeed,
                            Packages = new List<MockFeedPackage>
                            {
                                new MockFeedPackage
                                {
                                    PackageId = _packageIdA.ToString(),
                                    Version = _packageVersionA.ToNormalizedString(),
                                    ToolCommandName = _toolCommandNameA.ToString()
                                },
                                new MockFeedPackage
                                {
                                    PackageId = _packageIdB.ToString(),
                                    Version = _packageVersionB.ToNormalizedString(),
                                    ToolCommandName = _toolCommandNameB.ToString()
                                },
                                new MockFeedPackage
                                {
                                    PackageId = _packageIdWithCommandNameCollisionWithA.ToString(),
                                    Version = _packageVersionWithCommandNameCollisionWithA.ToNormalizedString(),
                                    ToolCommandName = "A"
                                }
                            }
                        }
                    }),
                installCallback: () => _installCalledCount++);

            _parseResult = Parser.Instance.Parse("dotnet tool restore");

            _localToolsResolverCache
                = new LocalToolsResolverCache(
                    _fileSystem,
                    new DirectoryPath(Path.Combine(_temporaryDirectory, "cache")),
                    1);
        }

        [Fact]
        public void WhenRunItCanSaveCommandsToCache()
        {
            IToolManifestFinder manifestFinder =
                new MockManifestFinder(new[]
                {
                    new ToolManifestPackage(_packageIdA, _packageVersionA,
                        new[] {_toolCommandNameA},
                        new DirectoryPath(_temporaryDirectory)),
                    new ToolManifestPackage(_packageIdB, _packageVersionB,
                        new[] {_toolCommandNameB},
                        new DirectoryPath(_temporaryDirectory))
                });

            ToolRestoreCommand toolRestoreCommand = new ToolRestoreCommand(_parseResult,
                _toolPackageInstallerMock,
                manifestFinder,
                _localToolsResolverCache,
                _fileSystem,
                _reporter
            );

            toolRestoreCommand.Execute().Should().Be(0);

            _localToolsResolverCache.TryLoad(
                    new RestoredCommandIdentifier(
                        _packageIdA,
                        _packageVersionA,
                        NuGetFramework.Parse(BundledTargetFramework.GetTargetFrameworkMoniker()),
                        Constants.AnyRid,
                        _toolCommandNameA), out RestoredCommand restoredCommand)
                .Should().BeTrue();

            _fileSystem.File.Exists(restoredCommand.Executable.Value)
                .Should().BeTrue($"Cached command should be found at {restoredCommand.Executable.Value}");
        }

        [Fact]
        public void WhenRunItCanSaveCommandsToCacheAndShowSuccessMessage()
        {
            IToolManifestFinder manifestFinder =
                new MockManifestFinder(new[]
                {
                    new ToolManifestPackage(_packageIdA, _packageVersionA,
                        new[] {_toolCommandNameA},
                        new DirectoryPath(_temporaryDirectory)),
                    new ToolManifestPackage(_packageIdB, _packageVersionB,
                        new[] {_toolCommandNameB},
                        new DirectoryPath(_temporaryDirectory))
                });

            ToolRestoreCommand toolRestoreCommand = new ToolRestoreCommand(_parseResult,
                _toolPackageInstallerMock,
                manifestFinder,
                _localToolsResolverCache,
                _fileSystem,
                _reporter
            );

            toolRestoreCommand.Execute().Should().Be(0);

            _reporter.Lines.Should().Contain(l => l.Contains(string.Format(
                LocalizableStrings.RestoreSuccessful, _packageIdA,
                _packageVersionA.ToNormalizedString(), _toolCommandNameA)));
            _reporter.Lines.Should().Contain(l => l.Contains(string.Format(
                LocalizableStrings.RestoreSuccessful, _packageIdB,
                _packageVersionB.ToNormalizedString(), _toolCommandNameB)));

            _reporter.Lines.Should().Contain(l => l.Contains("\x1B[32m"),
                "ansicolor code for green, message should be green");
        }

        [Fact]
        public void WhenRestoredCommandHasTheSameCommandNameItThrows()
        {
            IToolManifestFinder manifestFinder =
                new MockManifestFinder(new[]
                {
                    new ToolManifestPackage(_packageIdA, _packageVersionA,
                        new[] {_toolCommandNameA},
                        new DirectoryPath(_temporaryDirectory)),
                    new ToolManifestPackage(_packageIdWithCommandNameCollisionWithA,
                        _packageVersionWithCommandNameCollisionWithA, new[] {_toolCommandNameA},
                        new DirectoryPath(_temporaryDirectory))
                });

            ToolRestoreCommand toolRestoreCommand = new ToolRestoreCommand(_parseResult,
                _toolPackageInstallerMock,
                manifestFinder,
                _localToolsResolverCache,
                _fileSystem,
                _reporter
            );

            var allPossibleErrorMessage = new[]
            {
                string.Format(LocalizableStrings.PackagesCommandNameCollisionConclusion,
                    string.Join(Environment.NewLine,
                        new[]
                        {
                            "\t" + string.Format(LocalizableStrings.PackagesCommandNameCollisionForOnePackage,
                                _toolCommandNameA.Value,
                                _packageIdA.ToString()),
                            "\t" + string.Format(LocalizableStrings.PackagesCommandNameCollisionForOnePackage,
                                "A",
                                _packageIdWithCommandNameCollisionWithA.ToString())
                        })),

                string.Format(LocalizableStrings.PackagesCommandNameCollisionConclusion,
                    string.Join(Environment.NewLine,
                        new[]
                        {
                            "\t" + string.Format(LocalizableStrings.PackagesCommandNameCollisionForOnePackage,
                                "A",
                                _packageIdWithCommandNameCollisionWithA.ToString()),
                            "\t" + string.Format(LocalizableStrings.PackagesCommandNameCollisionForOnePackage,
                                _toolCommandNameA.Value,
                                _packageIdA.ToString()),
                        })),
            };

            Action a = () => toolRestoreCommand.Execute();
            a.ShouldThrow<ToolPackageException>()
                .And.Message
                .Should().BeOneOf(allPossibleErrorMessage, "Run in parallel, no order guarantee");
        }

        [Fact]
        public void WhenSomePackageFailedToRestoreItCanRestorePartiallySuccessful()
        {
            IToolManifestFinder manifestFinder =
                new MockManifestFinder(new[]
                {
                    new ToolManifestPackage(_packageIdA, _packageVersionA,
                        new[] {_toolCommandNameA},
                        new DirectoryPath(_temporaryDirectory)),
                    new ToolManifestPackage(new PackageId("non-exists"), NuGetVersion.Parse("1.0.0"),
                        new[] {new ToolCommandName("non-exists")},
                        new DirectoryPath(_temporaryDirectory))
                });

            ToolRestoreCommand toolRestoreCommand = new ToolRestoreCommand(_parseResult,
                _toolPackageInstallerMock,
                manifestFinder,
                _localToolsResolverCache,
                _fileSystem,
                _reporter
            );

            int executeResult = toolRestoreCommand.Execute();
            _reporter.Lines.Should()
                .Contain(l => l.Contains(string.Format(LocalizableStrings.PackageFailedToRestore,
                    "non-exists", "")));

            _reporter.Lines.Should().Contain(l => l.Contains(LocalizableStrings.RestorePartiallyFailed));

            executeResult.Should().Be(1);

            _localToolsResolverCache.TryLoad(
                    new RestoredCommandIdentifier(
                        _packageIdA,
                        _packageVersionA,
                        NuGetFramework.Parse(BundledTargetFramework.GetTargetFrameworkMoniker()),
                        Constants.AnyRid,
                        _toolCommandNameA), out _)
                .Should().BeTrue("Existing package will succeed despite other package failed");
        }

        [Fact]
        public void ItShouldFailWhenPackageCommandNameDoesNotMatchManifestCommands()
        {
            ToolCommandName differentCommandNameA = new ToolCommandName("different-command-nameA");
            ToolCommandName differentCommandNameB = new ToolCommandName("different-command-nameB");
            IToolManifestFinder manifestFinder =
                new MockManifestFinder(new[]
                {
                    new ToolManifestPackage(_packageIdA, _packageVersionA,
                        new[] {differentCommandNameA, differentCommandNameB},
                        new DirectoryPath(_temporaryDirectory)),
                });

            ToolRestoreCommand toolRestoreCommand = new ToolRestoreCommand(_parseResult,
                _toolPackageInstallerMock,
                manifestFinder,
                _localToolsResolverCache,
                _fileSystem,
                _reporter
            );

            toolRestoreCommand.Execute().Should().Be(1);
            _reporter.Lines.Should()
                .Contain(l =>
                    l.Contains(
                        string.Format(LocalizableStrings.CommandsMismatch,
                            "\"different-command-nameA\" \"different-command-nameB\"", _packageIdA, "\"a\"")));
        }

        [Fact]
        public void WhenCannotFindManifestFileItPrintsWarning()
        {
            IToolManifestFinder realManifestFinderImplementationWithMockFinderSystem =
                new ToolManifestFinder(new DirectoryPath(Path.GetTempPath()), _fileSystem, new FakeDangerousFileDetector());

            ToolRestoreCommand toolRestoreCommand = new ToolRestoreCommand(_parseResult,
                _toolPackageInstallerMock,
                realManifestFinderImplementationWithMockFinderSystem,
                _localToolsResolverCache,
                _fileSystem,
                _reporter
            );

            toolRestoreCommand.Execute().Should().Be(0);

            _reporter.Lines.Should()
                .Contain(l =>
                    l.Contains(ToolManifest.LocalizableStrings.CannotFindAManifestFile));
        }

        [Fact]
        public void WhenPackageIsRestoredAlreadyItWillNotRestoreItAgain()
        {
            IToolManifestFinder manifestFinder =
                new MockManifestFinder(new[]
                {
                    new ToolManifestPackage(_packageIdA, _packageVersionA,
                        new[] {_toolCommandNameA},
                        new DirectoryPath(_temporaryDirectory))
                });

            ToolRestoreCommand toolRestoreCommand = new ToolRestoreCommand(_parseResult,
                _toolPackageInstallerMock,
                manifestFinder,
                _localToolsResolverCache,
                _fileSystem,
                _reporter
            );

            toolRestoreCommand.Execute();
            var installCallCountBeforeTheSecondRestore = _installCalledCount;
            toolRestoreCommand.Execute();

            installCallCountBeforeTheSecondRestore.Should().BeGreaterThan(0);
            _installCalledCount.Should().Be(installCallCountBeforeTheSecondRestore);
        }

        [Fact]
        public void WhenPackageIsRestoredAlreadyButDllIsRemovedItRestoresAgain()
        {
            IToolManifestFinder manifestFinder =
                new MockManifestFinder(new[]
                {
                    new ToolManifestPackage(_packageIdA, _packageVersionA,
                        new[] {_toolCommandNameA},
                        new DirectoryPath(_temporaryDirectory))
                });

            ToolRestoreCommand toolRestoreCommand = new ToolRestoreCommand(_parseResult,
                _toolPackageInstallerMock,
                manifestFinder,
                _localToolsResolverCache,
                _fileSystem,
                _reporter
            );

            toolRestoreCommand.Execute();
            _fileSystem.Directory.Delete(_nugetGlobalPackagesFolder.Value, true);
            var installCallCountBeforeTheSecondRestore = _installCalledCount;
            toolRestoreCommand.Execute();

            installCallCountBeforeTheSecondRestore.Should().BeGreaterThan(0);
            _installCalledCount.Should().Be(installCallCountBeforeTheSecondRestore + 1);
        }

        [Fact]
        public void WhenRunWithoutManifestFileItShouldPrintSpecificRestoreErrorMessage()
        {
            IToolManifestFinder manifestFinder =
                new CannotFindManifestFinder();

            ToolRestoreCommand toolRestoreCommand = new ToolRestoreCommand(_parseResult,
                _toolPackageInstallerMock,
                manifestFinder,
                _localToolsResolverCache,
                _fileSystem,
                _reporter
            );

            toolRestoreCommand.Execute().Should().Be(0);

            _reporter.Lines.Should().Contain(l =>
                l.Contains(Cli.Utils.AnsiColorExtensions.Yellow(LocalizableStrings.NoToolsWereRestored)));
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

        private class CannotFindManifestFinder : IToolManifestFinder
        {
            public IReadOnlyCollection<ToolManifestPackage> Find(FilePath? filePath = null)
            {
                throw new ToolManifestCannotBeFoundException("In test cannot find manifest");
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
