// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Microsoft.DotNet.ToolManifest;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.Extensions.DependencyModel.Tests;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Frameworks;
using NuGet.Versioning;
using Xunit;
using LocalizableStrings = Microsoft.DotNet.ToolManifest.LocalizableStrings;
using System.Linq;

namespace Microsoft.DotNet.Tests.Commands.Tool
{
    public class ToolManifestEditorTests
    {
        private readonly IFileSystem _fileSystem;
        private readonly string _testDirectoryRoot;
        private const string _manifestFilename = "dotnet-tools.json";

        public ToolManifestEditorTests()
        {
            _fileSystem = new FileSystemMockBuilder().UseCurrentSystemTemporaryDirectory().Build();
            _testDirectoryRoot = _fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;
        }

        [Fact]
        public void GivenManifestFileItCanAddEntryToIt()
        {
            string manifestFile = Path.Combine(_testDirectoryRoot, _manifestFilename);
            _fileSystem.File.WriteAllText(manifestFile, _jsonContent);

            var toolManifestFileEditor = new ToolManifestEditor(_fileSystem, new FakeDangerousFileDetector());

            toolManifestFileEditor.Add(new FilePath(manifestFile),
                new PackageId("new-tool"),
                NuGetVersion.Parse("3.0.0"),
                new[] {new ToolCommandName("newtool")});

            _fileSystem.File.ReadAllText(manifestFile).Should().Be(
                @"{
  ""version"": 1,
  ""isRoot"": true,
  ""tools"": {
    ""t-rex"": {
      ""version"": ""1.0.53"",
      ""commands"": [
        ""t-rex""
      ]
    },
    ""dotnetsay"": {
      ""version"": ""2.1.4"",
      ""commands"": [
        ""dotnetsay""
      ]
    },
    ""new-tool"": {
      ""version"": ""3.0.0"",
      ""commands"": [
        ""newtool""
      ]
    }
  }
}");
        }

        [Fact]
        public void GivenManifestFileWithoutToolsEntryItCanAddEntryToIt()
        {
            string manifestFile = Path.Combine(_testDirectoryRoot, _manifestFilename);
            _fileSystem.File.WriteAllText(manifestFile, _jsonContentWithoutToolsEntry);

            var toolManifestFileEditor = new ToolManifestEditor(_fileSystem, new FakeDangerousFileDetector());

            toolManifestFileEditor.Add(new FilePath(manifestFile),
                new PackageId("new-tool"),
                NuGetVersion.Parse("3.0.0"),
                new[] {new ToolCommandName("newtool")});

            _fileSystem.File.ReadAllText(manifestFile).Should().Be(
                @"{
  ""isRoot"": true,
  ""tools"": {
    ""new-tool"": {
      ""version"": ""3.0.0"",
      ""commands"": [
        ""newtool""
      ]
    }
  }
}");
        }

        [Fact]
        public void GivenManifestFileWhenAddingTheSamePackageIdToolItThrows()
        {
            string manifestFile = Path.Combine(_testDirectoryRoot, _manifestFilename);
            _fileSystem.File.WriteAllText(manifestFile, _jsonContent);

            var toolManifestFileEditor = new ToolManifestEditor(_fileSystem, new FakeDangerousFileDetector());

            PackageId packageId = new PackageId("dotnetsay");
            NuGetVersion nuGetVersion = NuGetVersion.Parse("3.0.0");
            Action a = () => toolManifestFileEditor.Add(new FilePath(manifestFile),
                packageId,
                nuGetVersion,
                new[] {new ToolCommandName("dotnetsay")});

            var expectedString = string.Format(
                LocalizableStrings.ManifestPackageIdCollision,
                "2.1.4",
                packageId.ToString(),
                manifestFile,
                nuGetVersion.ToNormalizedString());

            a.ShouldThrow<ToolManifestException>()
                .And.Message.Should().Contain(expectedString);

            _fileSystem.File.ReadAllText(manifestFile).Should().Be(_jsonContent);
        }

        [Fact]
        public void GivenManifestFileWhenAddingTheSamePackageIdSameVersionSameCommandsItDoesNothing()
        {
            string manifestFile = Path.Combine(_testDirectoryRoot, _manifestFilename);
            _fileSystem.File.WriteAllText(manifestFile, _jsonContent);

            var toolManifestFileEditor = new ToolManifestEditor(_fileSystem, new FakeDangerousFileDetector());

            PackageId packageId = new PackageId("dotnetsay");
            NuGetVersion nuGetVersion = NuGetVersion.Parse("2.1.4");
            Action a = () => toolManifestFileEditor.Add(new FilePath(manifestFile),
                packageId,
                nuGetVersion,
                new[] {new ToolCommandName("dotnetsay")});

            a.ShouldNotThrow();

            _fileSystem.File.ReadAllText(manifestFile).Should().Be(_jsonContent);
        }

        [Fact]
        public void GivenAnInvalidManifestFileWhenAddItThrows()
        {
            string manifestFile = Path.Combine(_testDirectoryRoot, _manifestFilename);
            _fileSystem.File.WriteAllText(manifestFile, _jsonWithInvalidField);

            var toolManifestFileEditor = new ToolManifestEditor(_fileSystem, new FakeDangerousFileDetector());

            PackageId packageId = new PackageId("dotnetsay");
            NuGetVersion nuGetVersion = NuGetVersion.Parse("3.0.0");
            Action a = () => toolManifestFileEditor.Add(new FilePath(manifestFile),
                packageId,
                nuGetVersion,
                new[] {new ToolCommandName("dotnetsay")});

            a.ShouldThrow<ToolManifestException>()
                .And.Message.Should().Contain(
                    string.Format(LocalizableStrings.InvalidManifestFilePrefix,
                        manifestFile,
                        string.Empty));

            _fileSystem.File.ReadAllText(manifestFile).Should().Be(_jsonWithInvalidField);
        }

        [Fact]
        public void GivenAnMissingManifestFileVersionItShouldNotThrow()
        {
            string manifestFile = Path.Combine(_testDirectoryRoot, _manifestFilename);
            _fileSystem.File.WriteAllText(manifestFile, _jsonContentMissingVersion);

            var toolManifestFileEditor = new ToolManifestEditor(_fileSystem, new FakeDangerousFileDetector());

            Action a = () =>
                toolManifestFileEditor.Read(new FilePath(manifestFile), new DirectoryPath(_testDirectoryRoot));

            a.ShouldNotThrow<ToolManifestException>();
        }

        [Fact]
        public void GivenManifestFileItCanRemoveEntryFromIt()
        {
            string manifestFile = Path.Combine(_testDirectoryRoot, _manifestFilename);
            _fileSystem.File.WriteAllText(manifestFile, _jsonContent);

            var toolManifestFileEditor = new ToolManifestEditor(_fileSystem, new FakeDangerousFileDetector());

            toolManifestFileEditor.Remove(new FilePath(manifestFile),
                new PackageId("dotnetsay"));

            _fileSystem.File.ReadAllText(manifestFile).Should().Be(
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
}");
        }

        [Fact]
        public void GivenManifestFileWhenRemoveNonExistPackageIdToolItThrows()
        {
            string manifestFile = Path.Combine(_testDirectoryRoot, _manifestFilename);
            _fileSystem.File.WriteAllText(manifestFile, _jsonContent);

            var toolManifestFileEditor = new ToolManifestEditor(_fileSystem, new FakeDangerousFileDetector());

            Action a = () => toolManifestFileEditor.Remove(
                new FilePath(manifestFile),
                new PackageId("non-exist"));

            a.ShouldThrow<ToolManifestException>()
                .And.Message.Should().Contain(string.Format(
                    LocalizableStrings.CannotFindPackageIdInManifest, "non-exist"));

            _fileSystem.File.ReadAllText(manifestFile).Should().Be(_jsonContent);
        }

        [Fact]
        public void GivenAnInvalidManifestFileWhenRemoveItThrows()
        {
            string manifestFile = Path.Combine(_testDirectoryRoot, _manifestFilename);
            _fileSystem.File.WriteAllText(manifestFile, _jsonWithInvalidField);

            var toolManifestFileEditor = new ToolManifestEditor(_fileSystem, new FakeDangerousFileDetector());

            Action a = () => toolManifestFileEditor.Remove(
                new FilePath(manifestFile),
                new PackageId("dotnetsay"));

            a.ShouldThrow<ToolManifestException>()
                .And.Message.Should().Contain(
                    string.Format(LocalizableStrings.InvalidManifestFilePrefix,
                        manifestFile,
                        string.Empty));

            _fileSystem.File.ReadAllText(manifestFile).Should().Be(_jsonWithInvalidField);
        }

        [Fact]
        public void GivenManifestFileWhenEditNonExistPackageIdItThrows()
        {
            string manifestFile = Path.Combine(_testDirectoryRoot, _manifestFilename);
            _fileSystem.File.WriteAllText(manifestFile, _jsonContent);

            var toolManifestFileEditor = new ToolManifestEditor(_fileSystem, new FakeDangerousFileDetector());

            Action a = () => toolManifestFileEditor.Edit(new FilePath(manifestFile),
                new PackageId("non-exist"),
                NuGetVersion.Parse("3.0.0"),
                new[] {new ToolCommandName("t-rex3")});

            a.ShouldThrow<ArgumentException>().And.Message.Should()
                .Contain($"Manifest {manifestFile} does not contain package id 'non-exist'.");
        }


        [Fact]
        public void GivenManifestFileItCanEditEntry()
        {
            string manifestFile = Path.Combine(_testDirectoryRoot, _manifestFilename);
            _fileSystem.File.WriteAllText(manifestFile, _jsonContent);

            var toolManifestFileEditor = new ToolManifestEditor(_fileSystem, new FakeDangerousFileDetector());

            toolManifestFileEditor.Edit(new FilePath(manifestFile),
                new PackageId("t-rex"),
                NuGetVersion.Parse("3.0.0"),
                new[] {new ToolCommandName("t-rex3")});

            _fileSystem.File.ReadAllText(manifestFile).Should().Be(
                @"{
  ""version"": 1,
  ""isRoot"": true,
  ""tools"": {
    ""t-rex"": {
      ""version"": ""3.0.0"",
      ""commands"": [
        ""t-rex3""
      ]
    },
    ""dotnetsay"": {
      ""version"": ""2.1.4"",
      ""commands"": [
        ""dotnetsay""
      ]
    }
  }
}", "And original tools entry order is preserved.");
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

        private string _jsonContentWithoutToolsEntry =
            @"{
   ""isRoot"":true
}";

        private string _jsonWithInvalidField =
            @"{
   ""version"":1,
   ""isRoot"":true,
   ""tools"":{
      ""t-rex"":{
         ""version"":""1.*"",
         ""commands"":[
            ""t-rex""
         ]
      }
   }
}";

        private string _jsonContentMissingVersion =
            @"{
   ""isRoot"":true,
   ""tools"":{
      ""t-rex"":{
         ""version"":""1.0.53"",
         ""commands"":[
            ""t-rex""
         ]
      }
   }
}";
    }
}
