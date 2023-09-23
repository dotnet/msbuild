// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ToolManifest;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Tools.Tool.Uninstall;
using Microsoft.Extensions.DependencyModel.Tests;
using Microsoft.Extensions.EnvironmentAbstractions;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.Uninstall.LocalizableStrings;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.Commands.Tool
{
    public class ToolUninstallLocalCommandTests
    {
        private readonly IFileSystem _fileSystem;
        private readonly string _temporaryDirectoryParent;
        private readonly ParseResult _parseResult;
        private readonly BufferedReporter _reporter;
        private readonly string _temporaryDirectory;
        private readonly string _manifestFilePath;
        private readonly PackageId _packageIdDotnsay = new("dotnetsay");
        private readonly ToolManifestFinder _toolManifestFinder;
        private readonly ToolManifestEditor _toolManifestEditor;
        private readonly ToolUninstallLocalCommand _defaultToolUninstallLocalCommand;

        public ToolUninstallLocalCommandTests()
        {
            _reporter = new BufferedReporter();
            _fileSystem = new FileSystemMockBuilder().UseCurrentSystemTemporaryDirectory().Build();
            _temporaryDirectoryParent = _fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;
            _temporaryDirectory = Path.Combine(_temporaryDirectoryParent, "sub");
            _fileSystem.Directory.CreateDirectory(_temporaryDirectory);

            _manifestFilePath = Path.Combine(_temporaryDirectory, "dotnet-tools.json");
            _fileSystem.File.WriteAllText(Path.Combine(_temporaryDirectory, _manifestFilePath), _jsonContent);
            _toolManifestFinder = new ToolManifestFinder(new DirectoryPath(_temporaryDirectory), _fileSystem, new FakeDangerousFileDetector());
            _toolManifestEditor = new ToolManifestEditor(_fileSystem, new FakeDangerousFileDetector());

            _parseResult = Parser.Instance.Parse($"dotnet tool uninstall {_packageIdDotnsay.ToString()}");
            _defaultToolUninstallLocalCommand = new ToolUninstallLocalCommand(
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

            a.Should().Throw<GracefulException>()
               .And.Message.Should()
               .Contain(Tools.Tool.Common.LocalizableStrings.NoManifestGuide);

            a.Should().Throw<GracefulException>()
                .And.Message.Should()
                .Contain(ToolManifest.LocalizableStrings.CannotFindAManifestFile);

            a.Should().Throw<GracefulException>()
                .And.VerboseMessage.Should().Contain(string.Format(ToolManifest.LocalizableStrings.ListOfSearched, ""));
        }

        [Fact]
        public void GivenNoManifestFileContainPackageIdItShouldThrow()
        {
            _fileSystem.File.Delete(_manifestFilePath);
            _fileSystem.File.WriteAllText(_manifestFilePath, _jsonContentContainNoPackageId);

            Action a = () => _defaultToolUninstallLocalCommand.Execute().Should().Be(0);

            a.Should().Throw<GracefulException>()
               .And.Message.Should()
               .Contain(string.Format(LocalizableStrings.NoManifestFileContainPackageId, _packageIdDotnsay));
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
            var toolUninstallLocalCommand = new ToolUninstallLocalCommand(
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
            var toolUninstallLocalCommand = new ToolUninstallLocalCommand(
                parseResult,
                _toolManifestFinder,
                _toolManifestEditor,
                _reporter);
            var toolUninstallCommand = new ToolUninstallCommand(
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

        [Fact]
        public void GivenParentDirHasManifestWithSamePackageIdWhenRunWithPackageIdItShouldOnlyChangTheClosestOne()
        {
            var parentManifestFilePath = Path.Combine(_temporaryDirectoryParent, "dotnet-tools.json");
            _fileSystem.File.WriteAllText(parentManifestFilePath, _jsonContent);

            _defaultToolUninstallLocalCommand.Execute();

            _fileSystem.File.ReadAllText(_manifestFilePath).Should().Be(_entryRemovedJsonContent, "Change the closest one");
            _fileSystem.File.ReadAllText(parentManifestFilePath).Should().Be(_jsonContent, "Do not change the manifest layer above");
        }

        [Fact]
        public void GivenParentDirHasManifestWithSamePackageIdWhenRunWithPackageIdItShouldOnlyChangTheClosestOne2()
        {
            var parentManifestFilePath = Path.Combine(_temporaryDirectoryParent, "dotnet-tools.json");
            _fileSystem.File.WriteAllText(parentManifestFilePath, _jsonContent);

            _defaultToolUninstallLocalCommand.Execute();
            _defaultToolUninstallLocalCommand.Execute();

            _fileSystem.File.ReadAllText(parentManifestFilePath).Should().Be(
                _entryRemovedJsonContent,
                "First invoke remove the one in current dir, the second invoke remove the one in parent dir.");
        }

        [Fact]
        public void GivenParentDirHasManifestWithSamePackageIdWhenRunWithPackageIdItShouldWarningTheOtherManifests()
        {
            var parentManifestFilePath = Path.Combine(_temporaryDirectoryParent, "dotnet-tools.json");
            _fileSystem.File.WriteAllText(parentManifestFilePath, _jsonContent);

            _defaultToolUninstallLocalCommand.Execute();

            _reporter.Lines[0].Should().Contain(parentManifestFilePath);
            _reporter.Lines[0].Should().NotContain(_manifestFilePath);
        }

        private string _jsonContent =
            @"{
   ""version"":1,
   ""isRoot"":false,
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

        private string _jsonContentContainNoPackageId =
            @"{
   ""version"":1,
   ""isRoot"":false,
   ""tools"":{}
}";

        private string _entryRemovedJsonContent =
            @"{
  ""version"": 1,
  ""isRoot"": false,
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
