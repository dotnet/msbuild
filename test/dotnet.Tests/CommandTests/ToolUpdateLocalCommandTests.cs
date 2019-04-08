// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ToolManifest;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.DotNet.Tools.Tests.ComponentMocks;
using Microsoft.DotNet.Tools.Tool.Restore;
using Microsoft.DotNet.Tools.Tool.Update;
using Microsoft.Extensions.DependencyModel.Tests;
using Microsoft.Extensions.EnvironmentAbstractions;
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
        private readonly AppliedOption _appliedCommand;
        private readonly ParseResult _parseResult;
        private readonly BufferedReporter _reporter;
        private readonly string _temporaryDirectory;
        private readonly string _manifestFilePath;
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

        private readonly MockFeed _mockFeed;


        public ToolUpdateLocalCommandTests()
        {
            _reporter = new BufferedReporter();
            _fileSystem = new FileSystemMockBuilder().UseCurrentSystemTemporaryDirectory().Build();
            _temporaryDirectory = _fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;

            _packageOriginalVersionA = NuGetVersion.Parse("1.0.0");
            _packageNewVersionA = NuGetVersion.Parse("2.0.0");

            _reporter = new BufferedReporter();
            _fileSystem = new FileSystemMockBuilder().UseCurrentSystemTemporaryDirectory().Build();
            _temporaryDirectory = _fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;
            _pathToPlacePackages = Path.Combine(_temporaryDirectory, "pathToPlacePackage");
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
                    new[]
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
            _appliedCommand = _parseResult["dotnet"]["tool"]["update"];

            _toolRestoreCommand = new ToolRestoreCommand(_appliedCommand,
                _parseResult,
                _toolPackageInstallerMock,
                _toolManifestFinder,
                _localToolsResolverCache,
                _fileSystem,
                _reporter
            );

            _defaultToolUpdateLocalCommand = new ToolUpdateLocalCommand(
                _appliedCommand,
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
            _mockFeed.Packages.Single().Version = _packageOriginalVersionA.ToNormalizedString();

            _defaultToolUpdateLocalCommand.Execute().Should().Be(0);

            AssertDefaultUpdateSuccess();
        }

        [Fact]
        public void GivenNoManifestFileItShouldThrow()
        {
            _fileSystem.File.Delete(_manifestFilePath);
            Action a = () => _defaultToolUpdateLocalCommand.Execute().Should().Be(0);

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
        public void WhenRunWithExplicitManifestFileItShouldUpdateFromExplicitManifestFile()
        {
            string explicitManifestFilePath = Path.Combine(_temporaryDirectory, "subdirectory", "dotnet-tools.json");
            _fileSystem.File.Delete(_manifestFilePath);
            _fileSystem.Directory.CreateDirectory(Path.Combine(_temporaryDirectory, "subdirectory"));
            _fileSystem.File.WriteAllText(explicitManifestFilePath, _jsonContent);

            ParseResult parseResult
                = Parser.Instance.Parse(
                    $"dotnet tool update {_packageIdA.ToString()} --tool-manifest {explicitManifestFilePath}");
            AppliedOption appliedCommand = parseResult["dotnet"]["tool"]["update"];

            _toolRestoreCommand.Execute();
            _mockFeed.Packages.Single().Version = _packageOriginalVersionA.ToNormalizedString();

            ToolUpdateLocalCommand toolUpdateLocalCommand = new ToolUpdateLocalCommand(
                appliedCommand,
                parseResult,
                _toolPackageInstallerMock,
                _toolManifestFinder,
                _toolManifestEditor,
                _localToolsResolverCache,
                _reporter);

            toolUpdateLocalCommand.Execute().Should().Be(0);
            AssertDefaultUpdateSuccess();
        }

        [Fact]
        public void WhenRunFromToolUpdateRedirectCommandWithPackageIdItShouldUpdateFromManifestFile()
        {
            ParseResult parseResult = Parser.Instance.Parse($"dotnet tool update {_packageIdA.ToString()}");
            AppliedOption appliedCommand = parseResult["dotnet"]["tool"]["update"];

            _toolRestoreCommand.Execute();
            _mockFeed.Packages.Single().Version = _packageOriginalVersionA.ToNormalizedString();

            ToolUpdateLocalCommand toolUpdateLocalCommand = new ToolUpdateLocalCommand(
                appliedCommand,
                parseResult,
                _toolPackageInstallerMock,
                _toolManifestFinder,
                _toolManifestEditor,
                _localToolsResolverCache,
                _reporter);
            ToolUpdateCommand toolUpdateCommand = new ToolUpdateCommand(
                appliedCommand,
                parseResult,
                toolUpdateLocalCommand: toolUpdateLocalCommand);

            toolUpdateCommand.Execute().Should().Be(0);

            AssertDefaultUpdateSuccess();
        }

        [Fact]
        public void WhenRunWithPackageIdItShouldShowSuccessMessage()
        {
            _toolRestoreCommand.Execute();
            _mockFeed.Packages.Single().Version = _packageOriginalVersionA.ToNormalizedString();

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

        private void AssertDefaultUpdateSuccess()
        {
            IReadOnlyCollection<ToolManifestPackage> manifestPackages = _toolManifestFinder.Find();
            manifestPackages.Should().HaveCount(1);
            ToolManifestPackage addedPackage = manifestPackages.Single();
            _localToolsResolverCache.TryLoad(new RestoredCommandIdentifier(
                    addedPackage.PackageId,
                    addedPackage.Version,
                    NuGetFramework.Parse(BundledTargetFramework.GetTargetFrameworkMoniker()),
                    Constants.AnyRid,
                    addedPackage.CommandNames.Single()),
                out RestoredCommand restoredCommand
            ).Should().BeTrue();

            _fileSystem.File.Exists(restoredCommand.Executable.Value);
            _fileSystem.File.ReadAllText(_manifestFilePath).Should().Be(_entryUpdatedJsonContent);
        }

        // TODO throw on version lower

        // TODO only deal with the closest manifest

        // TODO throw on cannot find manifest file

        // TODO message on version lower

        // TODO Support version range

        // TODO If not restore install it

        // TODO if no install, install it

        // TODO framework cannot combine with local

        private readonly string _jsonContent =
            @"{
   ""version"":1,
   ""isRoot"":true,
   ""tools"":{
      ""local.tool.console.a"":{
         ""version"":""1.0.0"",
         ""commands"":[
            ""a""
         ]
      }
   }
}";

        private readonly string _entryUpdatedJsonContent =
            @"{
   ""version"":1,
   ""isRoot"":true,
   ""tools"":{
      ""local.tool.console.a"":{
         ""version"":""2.0.0"",
         ""commands"":[
            ""a""
         ]
      }
   }
}";
    }
}
