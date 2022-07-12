// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Tools.Tool.Install;
using Microsoft.DotNet.Tools.Tests.ComponentMocks;
using Microsoft.Extensions.DependencyModel.Tests;
using Microsoft.Extensions.EnvironmentAbstractions;
using Xunit;
using NuGet.Versioning;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.Install.LocalizableStrings;
using Microsoft.DotNet.ToolManifest;
using NuGet.Frameworks;
using Microsoft.NET.TestFramework.Utilities;
using System.CommandLine;
using System.CommandLine.Parsing;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.Commands.Tool
{
    public class ToolInstallLocalCommandTests
    {
        private readonly IFileSystem _fileSystem;
        private readonly IToolPackageStore _toolPackageStore;
        private readonly ToolPackageInstallerMock _toolPackageInstallerMock;
        private readonly ParseResult _parseResult;
        private readonly BufferedReporter _reporter;
        private readonly string _temporaryDirectory;
        private readonly string _pathToPlacePackages;
        private readonly ILocalToolsResolverCache _localToolsResolverCache;
        private readonly string _manifestFilePath;
        private readonly PackageId _packageIdA = new PackageId("local.tool.console.a");
        private readonly NuGetVersion _packageVersionA;
        private readonly ToolCommandName _toolCommandNameA = new ToolCommandName("a");
        private readonly ToolManifestFinder _toolManifestFinder;
        private readonly ToolManifestEditor _toolManifestEditor;

        public ToolInstallLocalCommandTests()
        {
            _packageVersionA = NuGetVersion.Parse("1.0.4");

            _reporter = new BufferedReporter();
            _fileSystem = new FileSystemMockBuilder().UseCurrentSystemTemporaryDirectory().Build();
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
                                }
                            }
                        }
                    }));

            _localToolsResolverCache
                = new LocalToolsResolverCache(
                    _fileSystem,
                    new DirectoryPath(Path.Combine(_temporaryDirectory, "cache")),
                    1);

            _manifestFilePath = Path.Combine(_temporaryDirectory, "dotnet-tools.json");
            _fileSystem.File.WriteAllText(Path.Combine(_temporaryDirectory, _manifestFilePath), _jsonContent);
            _toolManifestFinder = new ToolManifestFinder(new DirectoryPath(_temporaryDirectory), _fileSystem, new FakeDangerousFileDetector());
            _toolManifestEditor = new ToolManifestEditor(_fileSystem);

            _parseResult = Parser.Instance.Parse($"dotnet tool install {_packageIdA.ToString()}");

            _localToolsResolverCache
                = new LocalToolsResolverCache(
                    _fileSystem,
                    new DirectoryPath(Path.Combine(_temporaryDirectory, "cache")),
                    1);
        }

        [Fact]
        public void WhenRunWithPackageIdItShouldSaveToCacheAndAddToManifestFile()
        {
            var toolInstallLocalCommand = GetDefaultTestToolInstallLocalCommand();

            toolInstallLocalCommand.Execute().Should().Be(0);

            AssertDefaultInstallSuccess();
        }

        [Fact]
        public void GivenNoManifestFileItShouldThrow()
        {
            _fileSystem.File.Delete(_manifestFilePath);
            var toolInstallLocalCommand = GetDefaultTestToolInstallLocalCommand();

            Action a = () => toolInstallLocalCommand.Execute();
            a.ShouldThrow<GracefulException>()
                .And.Message.Should()
                .Contain(ToolManifest.LocalizableStrings.CannotFindAManifestFile);
        }

        [Fact]
        public void GivenNoManifestFileItShouldThrowAndContainNoManifestGuide()
        {
            _fileSystem.File.Delete(_manifestFilePath);
            var toolInstallLocalCommand = GetDefaultTestToolInstallLocalCommand();

            Action a = () => toolInstallLocalCommand.Execute();
            a.ShouldThrow<GracefulException>()
                .And.Message.Should()
                .Contain(LocalizableStrings.NoManifestGuide);

            a.ShouldThrow<GracefulException>()
                .And.Message.Should()
                .Contain(ToolManifest.LocalizableStrings.CannotFindAManifestFile);

            a.ShouldThrow<GracefulException>()
                .And.VerboseMessage.Should().Contain(string.Format(ToolManifest.LocalizableStrings.ListOfSearched, ""));
        }

        [Fact]
        public void WhenRunWithExplicitManifestFileItShouldAddEntryToExplicitManifestFile()
        {
            var explicitManifestFilePath = Path.Combine(_temporaryDirectory, "subdirectory", "dotnet-tools.json");
            _fileSystem.File.Delete(_manifestFilePath);
            _fileSystem.Directory.CreateDirectory(Path.Combine(_temporaryDirectory, "subdirectory"));
            _fileSystem.File.WriteAllText(explicitManifestFilePath, _jsonContent);

            ParseResult parseResult =
                Parser.Instance.Parse(
                    $"dotnet tool install {_packageIdA.ToString()} --tool-manifest {explicitManifestFilePath}");

            var installLocalCommand = new ToolInstallLocalCommand(
                parseResult,
                _toolPackageInstallerMock,
                _toolManifestFinder,
                _toolManifestEditor,
                _localToolsResolverCache,
                _reporter);

            installLocalCommand.Execute().Should().Be(0);
            _toolManifestFinder.Find(new FilePath(explicitManifestFilePath)).Should().HaveCount(1);
        }

        [Fact]
        public void WhenRunFromToolInstallRedirectCommandWithPackageIdItShouldSaveToCacheAndAddToManifestFile()
        {
            var toolInstallLocalCommand = GetDefaultTestToolInstallLocalCommand();

            var toolInstallCommand = new ToolInstallCommand(
                _parseResult,
                toolInstallLocalCommand: toolInstallLocalCommand);

            toolInstallCommand.Execute().Should().Be(0);
            AssertDefaultInstallSuccess();
        }

        [Fact]
        public void WhenRunWithPackageIdItShouldShowSuccessMessage()
        {
            var toolInstallLocalCommand = GetDefaultTestToolInstallLocalCommand();

            toolInstallLocalCommand.Execute().Should().Be(0);

            _reporter.Lines[0].Should()
                .Contain(
                    string.Format(LocalizableStrings.LocalToolInstallationSucceeded,
                        _toolCommandNameA.ToString(),
                        _packageIdA,
                        _packageVersionA.ToNormalizedString(),
                        _manifestFilePath).Green());
        }

        [Fact]
        public void GivenFailedPackageInstallWhenRunWithPackageIdItShouldNotChangeManifestFile()
        {
            ParseResult result = Parser.Instance.Parse($"dotnet tool install non-exist");

            var installLocalCommand = new ToolInstallLocalCommand(
                result,
                _toolPackageInstallerMock,
                _toolManifestFinder,
                _toolManifestEditor,
                _localToolsResolverCache,
                _reporter);

            Action a = () => installLocalCommand.Execute();
            a.ShouldThrow<GracefulException>()
                .And.Message.Should()
                .Contain(LocalizableStrings.ToolInstallationRestoreFailed);

            _fileSystem.File.ReadAllText(_manifestFilePath).Should()
                .Be(_jsonContent, "Manifest file should not be changed");
        }

        [Fact]
        public void GivenManifestFileConflictItShouldNotAddToCache()
        {
            _toolManifestEditor.Add(
                new FilePath(_manifestFilePath),
                _packageIdA,
                new NuGetVersion(1, 1, 1),
                new[] {_toolCommandNameA});

            var toolInstallLocalCommand = GetDefaultTestToolInstallLocalCommand();

            Action a = () => toolInstallLocalCommand.Execute();
            a.ShouldThrow<GracefulException>();

            _localToolsResolverCache.TryLoad(new RestoredCommandIdentifier(
                    _packageIdA,
                    _packageVersionA,
                    NuGetFramework.Parse(BundledTargetFramework.GetTargetFrameworkMoniker()),
                    Constants.AnyRid,
                    _toolCommandNameA),
                out RestoredCommand restoredCommand
            ).Should().BeFalse("it should not add to cache if add to manifest failed. " +
                               "But restore do not need to 'revert' since it just set in nuget global directory");
        }

        private ToolInstallLocalCommand GetDefaultTestToolInstallLocalCommand()
        {
            return new ToolInstallLocalCommand(
                _parseResult,
                _toolPackageInstallerMock,
                _toolManifestFinder,
                _toolManifestEditor,
                _localToolsResolverCache,
                _reporter);
        }

        [Fact]
        public void WhenRunWithExactVersionItShouldSucceed()
        {
            ParseResult result = Parser.Instance.Parse(
                $"dotnet tool install {_packageIdA.ToString()} --version {_packageVersionA.ToNormalizedString()}");

            var installLocalCommand = new ToolInstallLocalCommand(
                result,
                _toolPackageInstallerMock,
                _toolManifestFinder,
                _toolManifestEditor,
                _localToolsResolverCache,
                _reporter);

            installLocalCommand.Execute().Should().Be(0);
            AssertDefaultInstallSuccess();
        }

        [Fact]
        public void WhenRunWithValidVersionRangeItShouldSucceed()
        {
            ParseResult result = Parser.Instance.Parse(
                $"dotnet tool install {_packageIdA.ToString()} --version 1.*");

            var installLocalCommand = new ToolInstallLocalCommand(
                result,
                _toolPackageInstallerMock,
                _toolManifestFinder,
                _toolManifestEditor,
                _localToolsResolverCache,
                _reporter);

            installLocalCommand.Execute().Should().Be(0);
            AssertDefaultInstallSuccess();
        }

        [Fact]
        public void WhenRunWithPrereleaseAndPackageVersionItShouldSucceed()
        {
            ParseResult result =
                Parser.Instance.Parse($"dotnet tool install {_packageIdA.ToString()} --prerelease");

            var installLocalCommand = new ToolInstallLocalCommand(
                result,
                GetToolToolPackageInstallerWithPreviewInFeed(),
                _toolManifestFinder,
                _toolManifestEditor,
                _localToolsResolverCache,
                _reporter);

            installLocalCommand.Execute().Should().Be(0);
            var manifestPackages = _toolManifestFinder.Find();
            manifestPackages.Should().HaveCount(1);
            var addedPackage = manifestPackages.Single();
            _localToolsResolverCache.TryLoad(new RestoredCommandIdentifier(
                    addedPackage.PackageId,
                    new NuGetVersion("2.0.1-preview1"),
                    NuGetFramework.Parse(BundledTargetFramework.GetTargetFrameworkMoniker()),
                    Constants.AnyRid,
                    addedPackage.CommandNames.Single()),
                out RestoredCommand restoredCommand
            ).Should().BeTrue();

            _fileSystem.File.Exists(restoredCommand.Executable.Value);
        }

        private IToolPackageInstaller GetToolToolPackageInstallerWithPreviewInFeed()
        {
            List<MockFeed> feeds = new List<MockFeed>
            {
                new MockFeed
                {
                    Type = MockFeedType.ImplicitAdditionalFeed,
                    Packages = new List<MockFeedPackage>
                    {
                        new MockFeedPackage
                        {
                            PackageId = _packageIdA.ToString(),
                            Version = "1.0.4",
                            ToolCommandName = "SimulatorCommand"
                        },
                        new MockFeedPackage
                        {
                            PackageId = _packageIdA.ToString(),
                            Version = "2.0.1-preview1",
                            ToolCommandName = "SimulatorCommand"
                        }
                    }
                }
            };
            var toolToolPackageInstaller = (IToolPackageInstaller)new ToolPackageInstallerMock(
                fileSystem: _fileSystem,
                store: _toolPackageStore,
                projectRestorer: new ProjectRestorerMock(
                    fileSystem: _fileSystem,
                    reporter: _reporter,
                    feeds: feeds),
                installCallback: null);
            return toolToolPackageInstaller;
        }

        private void AssertDefaultInstallSuccess()
        {
            var manifestPackages = _toolManifestFinder.Find();
            manifestPackages.Should().HaveCount(1);
            var addedPackage = manifestPackages.Single();
            _localToolsResolverCache.TryLoad(new RestoredCommandIdentifier(
                    addedPackage.PackageId,
                    addedPackage.Version,
                    NuGetFramework.Parse(BundledTargetFramework.GetTargetFrameworkMoniker()),
                    Constants.AnyRid,
                    addedPackage.CommandNames.Single()),
                out RestoredCommand restoredCommand
            ).Should().BeTrue();

            _fileSystem.File.Exists(restoredCommand.Executable.Value);
        }

        private string _jsonContent =
            @"{
   ""version"":1,
   ""isRoot"":true,
   ""tools"":{
   }
}";
    }
}
