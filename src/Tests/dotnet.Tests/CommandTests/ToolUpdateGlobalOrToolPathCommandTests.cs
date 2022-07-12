// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Tools.Tool.Install;
using Microsoft.DotNet.Tools.Tests.ComponentMocks;
using Microsoft.DotNet.Tools.Tool.Update;
using Microsoft.Extensions.DependencyModel.Tests;
using Microsoft.Extensions.EnvironmentAbstractions;
using Xunit;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.Update.LocalizableStrings;
using Microsoft.DotNet.ShellShim;
using System.IO;
using Microsoft.NET.TestFramework.Utilities;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Runtime.InteropServices;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.Commands.Tool
{
    public class ToolUpdateGlobalOrToolPathCommandTests
    {
        private readonly BufferedReporter _reporter;
        private readonly IFileSystem _fileSystem;
        private readonly EnvironmentPathInstructionMock _environmentPathInstructionMock;
        private readonly ToolPackageStoreMock _store;
        private readonly PackageId _packageId = new PackageId("global.tool.console.demo");
        private readonly List<MockFeed> _mockFeeds;
        private const string LowerPackageVersion = "1.0.4";
        private const string HigherPackageVersion = "1.0.5";
        private const string HigherPreviewPackageVersion = "1.0.5-preview3";
        private readonly string _shimsDirectory;
        private readonly string _toolsDirectory;

        public ToolUpdateGlobalOrToolPathCommandTests()
        {
            _reporter = new BufferedReporter();
            _fileSystem = new FileSystemMockBuilder().UseCurrentSystemTemporaryDirectory().Build();
            var tempDirectory = _fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;
            _shimsDirectory = Path.Combine(tempDirectory, "shims");
            _toolsDirectory = Path.Combine(tempDirectory, "tools");
            _environmentPathInstructionMock = new EnvironmentPathInstructionMock(_reporter, _shimsDirectory);
            _store = new ToolPackageStoreMock(new DirectoryPath(_toolsDirectory), _fileSystem);
            _mockFeeds = new List<MockFeed>
            {
                new MockFeed
                {
                    Type = MockFeedType.FeedFromGlobalNugetConfig,
                    Packages = new List<MockFeedPackage>
                    {
                        new MockFeedPackage
                        {
                            PackageId = _packageId.ToString(),
                            Version = LowerPackageVersion,
                            ToolCommandName = "SimulatorCommand"
                        },
                        new MockFeedPackage
                        {
                            PackageId = _packageId.ToString(),
                            Version = HigherPackageVersion,
                            ToolCommandName = "SimulatorCommand"
                        },
                        new MockFeedPackage
                        {
                            PackageId = _packageId.ToString(),
                            Version = HigherPreviewPackageVersion,
                            ToolCommandName = "SimulatorCommand"
                        }
                    }
                }
            };
        }

        [Fact]
        public void GivenANonFeedExistentPackageItErrors()
        {
            var packageId = "does.not.exist";
            var command = CreateUpdateCommand($"-g {packageId}");

            Action a = () => command.Execute();

            a.ShouldThrow<GracefulException>().And.Message
                .Should().Contain(
                   Tools.Tool.Install.LocalizableStrings.ToolInstallationRestoreFailed);
        }

        [Fact]
        public void GivenANonExistentPackageItInstallTheLatest()
        {
            var command = CreateUpdateCommand($"-g {_packageId}");

            command.Execute();

            _store.EnumeratePackageVersions(_packageId).Single().Version.ToFullString().Should()
                .Be(HigherPackageVersion);
        }


        [Fact]
        public void GivenAnExistedLowerversionInstallationWhenCallItCanUpdateThePackageVersion()
        {
            CreateInstallCommand($"-g {_packageId} --version {LowerPackageVersion}").Execute();

            var command = CreateUpdateCommand($"-g {_packageId}");

            command.Execute();

            _store.EnumeratePackageVersions(_packageId).Single().Version.ToFullString().Should()
                .Be(HigherPackageVersion);
        }

        [Fact]
        public void GivenAnExistedLowerversionInstallationWhenCallFromRedirectorItCanUpdateThePackageVersion()
        {
            CreateInstallCommand($"-g {_packageId} --version {LowerPackageVersion}").Execute();

            ParseResult result = Parser.Instance.Parse("dotnet tool update " + $"-g {_packageId}");

            var toolUpdateGlobalOrToolPathCommand = new ToolUpdateGlobalOrToolPathCommand(
                result,
                (location, forwardArguments) => (_store, _store, new ToolPackageInstallerMock(
                    _fileSystem,
                    _store,
                    new ProjectRestorerMock(
                        _fileSystem,
                        _reporter,
                        _mockFeeds
                    )),
                    new ToolPackageUninstallerMock(_fileSystem, _store)),
                (_, _) => GetMockedShellShimRepository(),
                _reporter);

            var toolUpdateCommand = new ToolUpdateCommand(
                 result,
                 _reporter,
                 toolUpdateGlobalOrToolPathCommand,
                 new ToolUpdateLocalCommand(result));

            toolUpdateCommand.Execute();

            _store.EnumeratePackageVersions(_packageId).Single().Version.ToFullString().Should()
                .Be(HigherPackageVersion);
        }

        [Fact]
        public void GivenAnExistedLowerversionInstallationWhenCallItCanPrintSuccessMessage()
        {
            CreateInstallCommand($"-g {_packageId} --version {LowerPackageVersion}").Execute();
            _reporter.Lines.Clear();

            var command = CreateUpdateCommand($"-g {_packageId}");

            command.Execute();

            _reporter.Lines.First().Should().Contain(string.Format(
                LocalizableStrings.UpdateSucceeded,
                _packageId, LowerPackageVersion, HigherPackageVersion));
        }

        [Fact]
        public void GivenAnExistedLowerversionInstallationWhenCallWithWildCardVersionItCanPrintSuccessMessage()
        {
            CreateInstallCommand($"-g {_packageId} --version {LowerPackageVersion}").Execute();
            _reporter.Lines.Clear();

            var command = CreateUpdateCommand($"-g {_packageId} --version 1.0.5-*");

            command.Execute();

            _reporter.Lines.First().Should().Contain(string.Format(
                LocalizableStrings.UpdateSucceeded,
                _packageId, LowerPackageVersion, HigherPackageVersion));
        }

        [Fact]
        public void GivenAnExistedLowerversionInstallationWhenCallWithPrereleaseVersionItCanPrintSuccessMessage()
        {
            CreateInstallCommand($"-g {_packageId} --version {LowerPackageVersion}").Execute();
            _reporter.Lines.Clear();

            var command = CreateUpdateCommand($"-g {_packageId} --prerelease");

            command.Execute();

            _reporter.Lines.First().Should().Contain(string.Format(
                LocalizableStrings.UpdateSucceeded,
                _packageId, LowerPackageVersion, HigherPackageVersion));
        }

        [Fact]
        public void GivenAnExistedHigherVersionInstallationWhenCallWithLowerVersionItThrowsAndRollsBack()
        {
            CreateInstallCommand($"-g {_packageId} --version {HigherPackageVersion}").Execute();
            _reporter.Lines.Clear();

            var command = CreateUpdateCommand($"-g {_packageId} --version {LowerPackageVersion}");

            Action a = () => command.Execute();

            a.ShouldThrow<GracefulException>().And.Message
                .Should().Contain(
                    string.Format(LocalizableStrings.UpdateToLowerVersion,
                        LowerPackageVersion,
                        HigherPackageVersion));

            _store.EnumeratePackageVersions(_packageId).Single().Version.ToFullString().Should()
                .Be(HigherPackageVersion);
        }

        [Fact]
        public void GivenAnExistedSameVersionInstallationWhenCallItCanPrintSuccessMessage()
        {
            CreateInstallCommand($"-g {_packageId} --version {HigherPackageVersion}").Execute();
            _reporter.Lines.Clear();

            var command = CreateUpdateCommand($"-g {_packageId}");

            command.Execute();
            
            _reporter.Lines.First().Should().Contain(string.Format(
                LocalizableStrings.UpdateSucceededStableVersionNoChange,
                _packageId, HigherPackageVersion));
        }

        [Fact]
        public void GivenAnExistedSameVersionInstallationWhenCallWithPrereleaseItUsesAPrereleaseSuccessMessage()
        {
            CreateInstallCommand($"-g {_packageId} --version {HigherPreviewPackageVersion}").Execute();
            _reporter.Lines.Clear();

            var command = CreateUpdateCommand($"-g {_packageId} --version {HigherPreviewPackageVersion}");

            command.Execute();

            _reporter.Lines.First().Should().Contain(string.Format(
                LocalizableStrings.UpdateSucceededPreVersionNoChange,
                _packageId, HigherPreviewPackageVersion));
        }

        [Fact]
        public void GivenAnExistedLowerversionWhenReinstallThrowsIthasTheFirstLineIndicateUpdateFailure()
        {
            CreateInstallCommand($"-g {_packageId} --version {LowerPackageVersion}").Execute();
            _reporter.Lines.Clear();

            ParseResult result = Parser.Instance.Parse("dotnet tool update " + $"-g {_packageId}");
            var command = new ToolUpdateGlobalOrToolPathCommand(
                result,
                (location, forwardArguments) => (_store, _store,
                    new ToolPackageInstallerMock(
                        _fileSystem,
                        _store,
                        new ProjectRestorerMock(
                            _fileSystem,
                            _reporter,
                            _mockFeeds
                        ),
                        installCallback: () => throw new ToolConfigurationException("Simulated error")),
                    new ToolPackageUninstallerMock(_fileSystem, _store)),
                (_, _) => GetMockedShellShimRepository(),
                _reporter);

            Action a = () => command.Execute();
            a.ShouldThrow<GracefulException>().And.Message.Should().Contain(
                string.Format(LocalizableStrings.UpdateToolFailed, _packageId) + Environment.NewLine +
                string.Format(Tools.Tool.Install.LocalizableStrings.InvalidToolConfiguration, "Simulated error"));
        }

        [Fact]
        public void GivenAnExistedLowerversionWhenReinstallThrowsItRollsBack()
        {
            CreateInstallCommand($"-g {_packageId} --version {LowerPackageVersion}").Execute();
            _reporter.Lines.Clear();

            ParseResult result = Parser.Instance.Parse("dotnet tool update " + $"-g {_packageId}");
            var command = new ToolUpdateGlobalOrToolPathCommand(
                result,
                (location, forwardArguments) => (_store, _store,
                    new ToolPackageInstallerMock(
                        _fileSystem,
                        _store,
                        new ProjectRestorerMock(
                            _fileSystem,
                            _reporter,
                            _mockFeeds
                        ),
                        installCallback: () => throw new ToolConfigurationException("Simulated error")),
                    new ToolPackageUninstallerMock(_fileSystem, _store)),
                (_, _) => GetMockedShellShimRepository(),
                _reporter);

            Action a = () => command.Execute();

            _store.EnumeratePackageVersions(_packageId).Single().Version.ToFullString().Should()
                .Be(LowerPackageVersion);
        }

        [Fact]
        public void GivenPackagedShimIsProvidedWhenRunWithPackageIdItCreatesShimUsingPackagedShim()
        {
            CreateInstallCommand($"-g {_packageId} --version {LowerPackageVersion}").Execute();
            _reporter.Lines.Clear();

            var extension = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : string.Empty;
            var prepackagedShimPath = Path.Combine(Path.GetTempPath(), "SimulatorCommand" + extension);
            var tokenToIdentifyPackagedShim = "packagedShim";
            _fileSystem.File.WriteAllText(prepackagedShimPath, tokenToIdentifyPackagedShim);

            var packagedShimsMap = new Dictionary<PackageId, IReadOnlyList<FilePath>>
            {
                [_packageId] = new[] {new FilePath(prepackagedShimPath)}
            };

            string options = $"-g {_packageId}";
            ParseResult result = Parser.Instance.Parse("dotnet tool update " + options);
            var command = new ToolUpdateGlobalOrToolPathCommand(
                result,
                (_, _) => (_store, _store, new ToolPackageInstallerMock(
                        _fileSystem,
                        _store,
                        new ProjectRestorerMock(
                            _fileSystem,
                            _reporter,
                            _mockFeeds
                        ),
                        packagedShimsMap: packagedShimsMap),
                    new ToolPackageUninstallerMock(_fileSystem, _store)),
                (_, _) => GetMockedShellShimRepository(),
                _reporter);

            command.Execute();

            _fileSystem.File.ReadAllText(ExpectedCommandPath()).Should().Be(tokenToIdentifyPackagedShim);

            string ExpectedCommandPath()
            {
                var extension = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : string.Empty;
                return Path.Combine(
                    _shimsDirectory,
                    "SimulatorCommand" + extension);
            }
        }

        private ToolInstallGlobalOrToolPathCommand CreateInstallCommand(string options)
        {
            ParseResult result = Parser.Instance.Parse("dotnet tool install " + options);

            return new ToolInstallGlobalOrToolPathCommand(
                result,
                (location, forwardArguments) => (_store, _store, new ToolPackageInstallerMock(
                    _fileSystem,
                    _store,
                    new ProjectRestorerMock(
                        _fileSystem,
                        _reporter,
                        _mockFeeds
                    ))),
                (_, _) => GetMockedShellShimRepository(),
                _environmentPathInstructionMock,
                _reporter);
        }

        private ToolUpdateGlobalOrToolPathCommand CreateUpdateCommand(string options)
        {
            ParseResult result = Parser.Instance.Parse("dotnet tool update " + options);

            return new ToolUpdateGlobalOrToolPathCommand(
                result,
                (location, forwardArguments) => (_store, _store, new ToolPackageInstallerMock(
                    _fileSystem,
                    _store,
                    new ProjectRestorerMock(
                        _fileSystem,
                        _reporter,
                        _mockFeeds
                    )),
                    new ToolPackageUninstallerMock(_fileSystem, _store)),
                (_, _) => GetMockedShellShimRepository(),
                _reporter);
        }

        private ShellShimRepository GetMockedShellShimRepository()
        {
            return new ShellShimRepository(
                    new DirectoryPath(_shimsDirectory),
                    string.Empty,
                    fileSystem: _fileSystem,
                    appHostShellShimMaker: new AppHostShellShimMakerMock(_fileSystem),
                    filePermissionSetter: new ToolInstallGlobalOrToolPathCommandTests.NoOpFilePermissionSetter());
        }
    }
}
