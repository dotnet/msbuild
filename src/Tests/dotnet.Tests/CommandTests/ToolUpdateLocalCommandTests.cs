// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ToolManifest;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Tools.Tests.ComponentMocks;
using Microsoft.DotNet.Tools.Tool.Restore;
using Microsoft.DotNet.Tools.Tool.Update;
using Microsoft.Extensions.DependencyModel.Tests;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.NET.TestFramework.Utilities;
using NuGet.Frameworks;
using NuGet.Versioning;
using Xunit;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.Update.LocalizableStrings;
using Parser = Microsoft.DotNet.Cli.Parser;


namespace Microsoft.DotNet.Tests.Commands.Tool
{
    public class ToolUpdateLocalCommandTests
    {
        private readonly IFileSystem _fileSystem;
        private readonly string _temporaryDirectoryParent;
        private readonly ParseResult _parseResult;
        private readonly BufferedReporter _reporter;
        private readonly string _temporaryDirectory;
        private readonly string _manifestFilePath;
        private readonly MockFeed _mockFeed;
        private readonly ToolManifestFinder _toolManifestFinder;
        private readonly ToolManifestEditor _toolManifestEditor;
        private readonly ToolUpdateLocalCommand _defaultToolUpdateLocalCommand;
        private readonly string _pathToPlacePackages;
        private readonly IToolPackageStore _toolPackageStore;
        private readonly ToolPackageInstallerMock _toolPackageInstallerMock;
        private readonly NuGetVersion _packageOriginalVersionA;
        private readonly NuGetVersion _packageNewVersionA;
        private readonly PackageId _packageIdA = new PackageId("local.tool.console.a");
        private readonly ToolCommandName _toolCommandNameA = new ToolCommandName("a");
        private readonly ILocalToolsResolverCache _localToolsResolverCache;
        private readonly ToolRestoreCommand _toolRestoreCommand;

        public ToolUpdateLocalCommandTests()
        {
            _reporter = new BufferedReporter();
            _fileSystem = new FileSystemMockBuilder().UseCurrentSystemTemporaryDirectory().Build();

            _temporaryDirectoryParent = _fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;
            _temporaryDirectory = Path.Combine(_temporaryDirectoryParent, "sub");
            _fileSystem.Directory.CreateDirectory(_temporaryDirectory);
            _pathToPlacePackages = Path.Combine(_temporaryDirectory, "pathToPlacePackage");

            _packageOriginalVersionA = NuGetVersion.Parse("1.0.0");
            _packageNewVersionA = NuGetVersion.Parse("2.0.0");

            ToolPackageStoreMock toolPackageStoreMock =
                new ToolPackageStoreMock(new DirectoryPath(_pathToPlacePackages), _fileSystem);
            _toolPackageStore = toolPackageStoreMock;
            _mockFeed = new MockFeed
            {
                Type = MockFeedType.ImplicitAdditionalFeed,
                Packages = new List<MockFeedPackage>
                {
                    new MockFeedPackage
                    {
                        PackageId = _packageIdA.ToString(),
                        Version = _packageOriginalVersionA.ToNormalizedString(),
                        ToolCommandName = _toolCommandNameA.ToString()
                    }
                }
            };
            _toolPackageInstallerMock = new ToolPackageInstallerMock(
                _fileSystem,
                _toolPackageStore,
                new ProjectRestorerMock(
                    _fileSystem,
                    _reporter,
                    new List<MockFeed>
                    {
                        _mockFeed
                    }));

            _localToolsResolverCache
                = new LocalToolsResolverCache(
                    _fileSystem,
                    new DirectoryPath(Path.Combine(_temporaryDirectory, "cache")),
                    1);

            _manifestFilePath = Path.Combine(_temporaryDirectory, "dotnet-tools.json");
            _fileSystem.File.WriteAllText(Path.Combine(_temporaryDirectory, _manifestFilePath), _jsonContent);
            _toolManifestFinder = new ToolManifestFinder(new DirectoryPath(_temporaryDirectory), _fileSystem,
                new FakeDangerousFileDetector());
            _toolManifestEditor = new ToolManifestEditor(_fileSystem, new FakeDangerousFileDetector());

            _parseResult = Parser.Instance.Parse($"dotnet tool update {_packageIdA.ToString()}");

            _toolRestoreCommand = new ToolRestoreCommand(
                _parseResult,
                _toolPackageInstallerMock,
                _toolManifestFinder,
                _localToolsResolverCache,
                _fileSystem,
                _reporter
            );

            _defaultToolUpdateLocalCommand = new ToolUpdateLocalCommand(
                _parseResult,
                _toolPackageInstallerMock,
                _toolManifestFinder,
                _toolManifestEditor,
                _localToolsResolverCache,
                _reporter);
        }

        [Fact]
        public void WhenRunWithPackageIdItShouldUpdateFromManifestFile()
        {
            _toolRestoreCommand.Execute();
            _mockFeed.Packages[0].Version = _packageNewVersionA.ToNormalizedString();

            _defaultToolUpdateLocalCommand.Execute().Should().Be(0);

            AssertUpdateSuccess();
        }

        [Fact]
        public void WhenRunFromDirectorWithPackageIdItShouldUpdateFromManifestFile()
        {
            _toolRestoreCommand.Execute();
            _mockFeed.Packages[0].Version = _packageNewVersionA.ToNormalizedString();

            var toolUpdateCommand = new ToolUpdateCommand(
                _parseResult,
                _reporter,
                new ToolUpdateGlobalOrToolPathCommand(_parseResult),
                _defaultToolUpdateLocalCommand);

            toolUpdateCommand.Execute().Should().Be(0);

            AssertUpdateSuccess();
        }

        [Fact]
        public void GivenNoRestoredManifestWhenRunWithPackageIdItShouldUpdateFromManifestFile()
        {
            _mockFeed.Packages[0].Version = _packageNewVersionA.ToNormalizedString();

            _defaultToolUpdateLocalCommand.Execute().Should().Be(0);

            AssertUpdateSuccess();
        }

        [Fact]
        public void GivenManifestDoesNotHavePackageWhenRunWithPackageIdItShouldUpdate()
        {
            _mockFeed.Packages[0].Version = _packageNewVersionA.ToNormalizedString();
            _fileSystem.File.Delete(_manifestFilePath);
            _fileSystem.File.WriteAllText(Path.Combine(_temporaryDirectory, _manifestFilePath), _jsonEmptyContent);

            _defaultToolUpdateLocalCommand.Execute().Should().Be(0);

            AssertUpdateSuccess();
        }

        [Fact]
        public void GivenNoManifestFileItShouldThrow()
        {
            _fileSystem.File.Delete(_manifestFilePath);
            Action a = () => _defaultToolUpdateLocalCommand.Execute().Should().Be(0);

            a.ShouldThrow<GracefulException>()
                .And.Message.Should()
                .Contain(ToolManifest.LocalizableStrings.CannotFindAManifestFile);

            a.ShouldThrow<GracefulException>()
                .And.VerboseMessage.Should().Contain(string.Format(ToolManifest.LocalizableStrings.ListOfSearched, ""));
        }

        [Fact]
        public void WhenRunWithExplicitManifestFileItShouldUpdateFromExplicitManifestFile()
        {
            string explicitManifestFilePath = Path.Combine(_temporaryDirectory, "subdirectory", "dotnet-tools.json");
            _fileSystem.File.Delete(_manifestFilePath);
            _fileSystem.Directory.CreateDirectory(Path.Combine(_temporaryDirectory, "subdirectory"));
            _fileSystem.File.WriteAllText(explicitManifestFilePath, _jsonContent);

            ParseResult parseResult
                = Parser.Instance.Parse(
                    $"dotnet tool update {_packageIdA.ToString()} --tool-manifest {explicitManifestFilePath}");

            _toolRestoreCommand.Execute();
            _mockFeed.Packages[0].Version = _packageNewVersionA.ToNormalizedString();

            ToolUpdateLocalCommand toolUpdateLocalCommand = new ToolUpdateLocalCommand(
                parseResult,
                _toolPackageInstallerMock,
                _toolManifestFinder,
                _toolManifestEditor,
                _localToolsResolverCache,
                _reporter);

            toolUpdateLocalCommand.Execute().Should().Be(0);
            AssertUpdateSuccess(new FilePath(explicitManifestFilePath));
        }

        [Fact]
        public void WhenRunFromToolUpdateRedirectCommandWithPackageIdItShouldUpdateFromManifestFile()
        {
            ParseResult parseResult = Parser.Instance.Parse($"dotnet tool update {_packageIdA.ToString()}");

            _toolRestoreCommand.Execute();
            _mockFeed.Packages[0].Version = _packageNewVersionA.ToNormalizedString();

            ToolUpdateLocalCommand toolUpdateLocalCommand = new ToolUpdateLocalCommand(
                parseResult,
                _toolPackageInstallerMock,
                _toolManifestFinder,
                _toolManifestEditor,
                _localToolsResolverCache,
                _reporter);
            ToolUpdateCommand toolUpdateCommand = new ToolUpdateCommand(
                parseResult,
                toolUpdateLocalCommand: toolUpdateLocalCommand);

            toolUpdateCommand.Execute().Should().Be(0);
        }

        [Fact]
        public void WhenRunWithPackageIdItShouldShowSuccessMessage()
        {
            _toolRestoreCommand.Execute();
            _mockFeed.Packages.Single().Version = _packageNewVersionA.ToNormalizedString();

            _reporter.Clear();
            _defaultToolUpdateLocalCommand.Execute();

            _reporter.Lines.Single()
                .Should().Contain(
                    string.Format(
                        LocalizableStrings.UpdateLocalToolSucceeded,
                        _packageIdA,
                        _packageOriginalVersionA.ToNormalizedString(),
                        _packageNewVersionA.ToNormalizedString(),
                        _manifestFilePath).Green());
        }

        [Fact]
        public void GivenParentDirHasManifestWithSamePackageIdWhenRunWithPackageIdItShouldOnlyChangTheClosestOne()
        {
            var parentManifestFilePath = Path.Combine(_temporaryDirectoryParent, "dotnet-tools.json");
            _fileSystem.File.WriteAllText(parentManifestFilePath, _jsonContent);

            _toolRestoreCommand.Execute();
            _mockFeed.Packages.Single().Version = _packageNewVersionA.ToNormalizedString();

            _reporter.Clear();
            _defaultToolUpdateLocalCommand.Execute();

            AssertUpdateSuccess();

            _fileSystem.File.ReadAllText(parentManifestFilePath).Should().Be(_jsonContent, "no change");
        }

        [Fact]
        public void GivenParentDirHasManifestWithSamePackageIdWhenRunWithPackageIdItShouldWarningTheOtherManifests()
        {
            var parentManifestFilePath = Path.Combine(_temporaryDirectoryParent, "dotnet-tools.json");
            _fileSystem.File.WriteAllText(parentManifestFilePath, _jsonContent);

            _toolRestoreCommand.Execute();

            _reporter.Clear();
            _mockFeed.Packages.Single().Version = _packageNewVersionA.ToNormalizedString();
            _defaultToolUpdateLocalCommand.Execute();

            _reporter.Lines[0].Should().Contain(parentManifestFilePath);
            _reporter.Lines[0].Should().NotContain(_manifestFilePath);
        }

        [Fact]
        public void GivenFeedVersionIsTheSameWhenRunWithPackageIdItShouldShowDifferentSuccessMessage()
        {
            _toolRestoreCommand.Execute();

            _reporter.Clear();
            _defaultToolUpdateLocalCommand.Execute();

            AssertUpdateSuccess(packageVersion: _packageOriginalVersionA);

            _reporter.Lines.Single()
                .Should().Contain(
                    string.Format(
                        LocalizableStrings.UpdateLocaToolSucceededVersionNoChange,
                        _packageIdA,
                        _packageOriginalVersionA.ToNormalizedString(),
                        _manifestFilePath));
        }

        [Fact]
        public void GivenFeedVersionIsLowerRunPackageIdItShouldThrow()
        {
            _toolRestoreCommand.Execute();
            _mockFeed.Packages.Single().Version = "0.9.0";

            _reporter.Clear();
            Action a = () => _defaultToolUpdateLocalCommand.Execute();
            a.ShouldThrow<GracefulException>().And.Message.Should().Contain(string.Format(
                LocalizableStrings.UpdateLocaToolToLowerVersion,
                "0.9.0",
                _packageOriginalVersionA.ToNormalizedString(),
                _manifestFilePath));
        }

        private void AssertUpdateSuccess(FilePath? manifestFile = null, NuGetVersion packageVersion = null)
        {
            packageVersion ??= _packageNewVersionA;
            IReadOnlyCollection<ToolManifestPackage> manifestPackages = _toolManifestFinder.Find(manifestFile);
            manifestPackages.Should().HaveCount(1);
            ToolManifestPackage addedPackage = manifestPackages.Single();
            addedPackage.Version.Should().Be(packageVersion);
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

        private readonly string _jsonContent =
            @"{
  ""version"": 1,
  ""isRoot"": false,
  ""tools"": {
    ""local.tool.console.a"": {
      ""version"": ""1.0.0"",
      ""commands"": [
        ""a""
      ]
    }
  }
}";

        private readonly string _jsonEmptyContent =
            @"{
  ""version"": 1,
  ""isRoot"": false,
  ""tools"": {}
}";
    }
}
