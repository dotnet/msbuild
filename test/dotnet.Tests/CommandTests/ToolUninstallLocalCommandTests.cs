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
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Tools.Tool.Uninstall;
using Microsoft.DotNet.Tools.Tests.ComponentMocks;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.DotNet.ShellShim;
using Microsoft.Extensions.DependencyModel.Tests;
using Microsoft.Extensions.EnvironmentAbstractions;
using Newtonsoft.Json;
using Xunit;
using Parser = Microsoft.DotNet.Cli.Parser;
using System.Runtime.InteropServices;
using NuGet.Versioning;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.Uninstall.LocalizableStrings;
using Microsoft.DotNet.ToolManifest;
using NuGet.Frameworks;


namespace Microsoft.DotNet.Tests.Commands
{
    public class ToolUninstallLocalCommandTests
    {
        private readonly IFileSystem _fileSystem;
        private readonly AppliedOption _appliedCommand;
        private readonly ParseResult _parseResult;
        private readonly BufferedReporter _reporter;
        private readonly string _temporaryDirectory;
        private readonly string _manifestFilePath;
        private readonly PackageId _packageIdDotnsay = new PackageId("dotnetsay");
        private readonly ToolManifestFinder _toolManifestFinder;
        private readonly ToolManifestEditor _toolManifestEditor;
        private readonly ToolUninstallLocalCommand _defaultToolUninstallLocalCommand;

        public ToolUninstallLocalCommandTests()
        {
            _reporter = new BufferedReporter();
            _fileSystem = new FileSystemMockBuilder().UseCurrentSystemTemporaryDirectory().Build();
            _temporaryDirectory = _fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;

            _manifestFilePath = Path.Combine(_temporaryDirectory, "dotnet-tools.json");
            _fileSystem.File.WriteAllText(Path.Combine(_temporaryDirectory, _manifestFilePath), _jsonContent);
            _toolManifestFinder = new ToolManifestFinder(new DirectoryPath(_temporaryDirectory), _fileSystem, new FakeDangerousFileDetector());
            _toolManifestEditor = new ToolManifestEditor(_fileSystem, new FakeDangerousFileDetector());

            _parseResult = Parser.Instance.Parse($"dotnet tool uninstall {_packageIdDotnsay.ToString()}");
            _appliedCommand = _parseResult["dotnet"]["tool"]["uninstall"];
            _defaultToolUninstallLocalCommand = new ToolUninstallLocalCommand(
                _appliedCommand,
                _parseResult,
                _toolManifestFinder,
                _toolManifestEditor,
                _reporter);
        }

        [Fact]
        public void WhenRunWithPackageIdItShouldRemoveFromManifestFile()
        {
            _defaultToolUninstallLocalCommand.Execute().Should().Be(0);

            _fileSystem.File.ReadAllText(_manifestFilePath).Should().Be(_entryRemovedJsonContent);
        }

        [Fact]
        public void GivenNoManifestFileItShouldThrow()
        {
            _fileSystem.File.Delete(_manifestFilePath);
            Action a = () => _defaultToolUninstallLocalCommand.Execute().Should().Be(0);

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
        public void WhenRunWithExplicitManifestFileItShouldRemoveFromExplicitManifestFile()
        {
            var explicitManifestFilePath = Path.Combine(_temporaryDirectory, "subdirectory", "dotnet-tools.json");
            _fileSystem.File.Delete(_manifestFilePath);
            _fileSystem.Directory.CreateDirectory(Path.Combine(_temporaryDirectory, "subdirectory"));
            _fileSystem.File.WriteAllText(explicitManifestFilePath, _jsonContent);

            var parseResult
                = Parser.Instance.Parse(
                    $"dotnet tool uninstall {_packageIdDotnsay.ToString()} --tool-manifest {explicitManifestFilePath}");
            var appliedCommand = parseResult["dotnet"]["tool"]["uninstall"];
            var toolUninstallLocalCommand = new ToolUninstallLocalCommand(
                appliedCommand,
                parseResult,
                _toolManifestFinder,
                _toolManifestEditor,
                _reporter);

            toolUninstallLocalCommand.Execute().Should().Be(0);
            _fileSystem.File.ReadAllText(explicitManifestFilePath).Should().Be(_entryRemovedJsonContent);
        }

        [Fact]
        public void WhenRunFromToolUninstallRedirectCommandWithPackageIdItShouldRemoveFromManifestFile()
        {
            var parseResult = Parser.Instance.Parse($"dotnet tool uninstall {_packageIdDotnsay.ToString()}");
            var appliedCommand = parseResult["dotnet"]["tool"]["uninstall"];
            var toolUninstallLocalCommand = new ToolUninstallLocalCommand(
                appliedCommand,
                parseResult,
                _toolManifestFinder,
                _toolManifestEditor,
                _reporter);
            var toolUninstallCommand = new ToolUninstallCommand(
                appliedCommand,
                parseResult,
                toolUninstallLocalCommand: toolUninstallLocalCommand);

            toolUninstallCommand.Execute().Should().Be(0);

            _fileSystem.File.ReadAllText(_manifestFilePath).Should().Be(_entryRemovedJsonContent);
        }

        [Fact]
        public void WhenRunWithPackageIdItShouldShowSuccessMessage()
        {
            _defaultToolUninstallLocalCommand.Execute();
            _reporter.Lines.Single()
                .Should().Contain(
                    string.Format(
                        LocalizableStrings.UninstallLocalToolSucceeded,
                        _packageIdDotnsay,
                        _manifestFilePath).Green());
        }

        private string _jsonContent =
            @"{
   ""version"":1,
   ""isRoot"":true,
   ""tools"":{
      ""t-rex"":{
         ""version"":""1.0.53"",
         ""commands"":[
            ""t-rex""
         ]
      },
      ""dotnetsay"":{
         ""version"":""2.1.4"",
         ""commands"":[
            ""dotnetsay""
         ]
      }
   }
}";

        private string _entryRemovedJsonContent =
            @"{
  ""version"": 1,
  ""isRoot"": true,
  ""tools"": {
    ""t-rex"": {
      ""version"": ""1.0.53"",
      ""commands"": [
        ""t-rex""
      ]
    }
  }
}";
    }
}
