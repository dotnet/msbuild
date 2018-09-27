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

namespace Microsoft.DotNet.Tests.Commands
{
    public class ToolManifestTests
    {
        private readonly IFileSystem _fileSystem;

        public ToolManifestTests()
        {
            _fileSystem = new FileSystemMockBuilder().UseCurrentSystemTemporaryDirectory().Build();
            _testDirectoryRoot = _fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;

            _defaultExpectedResult = new List<ToolManifestPackage>
            {
                new ToolManifestPackage(
                    new PackageId("t-rex"),
                    NuGetVersion.Parse("1.0.53"),
                    new[] {new ToolCommandName("t-rex")}),
                new ToolManifestPackage(
                    new PackageId("dotnetsay"),
                    NuGetVersion.Parse("2.1.4"),
                    new[] {new ToolCommandName("dotnetsay")})
            };
        }

        [Fact]
        public void GivenManifestFileOnSameDirectoryItGetContent()
        {
            _fileSystem.File.WriteAllText(Path.Combine(_testDirectoryRoot, _manifestFilename), _jsonContent);
            var toolManifest = new ToolManifestFinder(new DirectoryPath(_testDirectoryRoot), _fileSystem);
            var manifestResult = toolManifest.Find();

            manifestResult.ShouldBeEquivalentTo(_defaultExpectedResult);
        }

        [Fact]
        public void GivenManifestFileOnParentDirectoryItGetContent()
        {
            var subdirectoryOfTestRoot = Path.Combine(_testDirectoryRoot, "sub");
            _fileSystem.File.WriteAllText(Path.Combine(_testDirectoryRoot, _manifestFilename), _jsonContent);
            var toolManifest = new ToolManifestFinder(new DirectoryPath(subdirectoryOfTestRoot), _fileSystem);
            var manifestResult = toolManifest.Find();

            manifestResult.ShouldBeEquivalentTo(_defaultExpectedResult);
        }

        [Fact]
        // https://github.com/JamesNK/Newtonsoft.Json/issues/931#issuecomment-224104005
        // Due to a limitation of newtonsoft json
        public void GivenManifestWithDuplicatedPackageIdItReturnsTheLastValue()
        {
            _fileSystem.File.WriteAllText(Path.Combine(_testDirectoryRoot, _manifestFilename),
                _jsonWithDuplicatedPackagedId);
            var toolManifest = new ToolManifestFinder(new DirectoryPath(_testDirectoryRoot), _fileSystem);
            var manifestResult = toolManifest.Find();

            manifestResult.Should()
                .Contain(
                    new ToolManifestPackage(
                        new PackageId("t-rex"),
                        NuGetVersion.Parse("2.1.4"),
                        new[] { new ToolCommandName("t-rex") }));
        }

        [Fact]
        public void WhenCalledWithFilePathItGetContent()
        {
            string customFileName = "customname.file";
            _fileSystem.File.WriteAllText(Path.Combine(_testDirectoryRoot, customFileName), _jsonContent);
            var toolManifest = new ToolManifestFinder(new DirectoryPath(_testDirectoryRoot), _fileSystem);
            var manifestResult =
                toolManifest.Find(new FilePath(Path.Combine(_testDirectoryRoot, customFileName)));

            manifestResult.ShouldBeEquivalentTo(_defaultExpectedResult);
        }

        [Fact]
        public void WhenCalledWithNonExistsFilePathItThrows()
        {
            var toolManifest = new ToolManifestFinder(new DirectoryPath(_testDirectoryRoot), _fileSystem);
            Action a = () => toolManifest.Find(new FilePath(Path.Combine(_testDirectoryRoot, "non-exists")));
            a.ShouldThrow<ToolManifestCannotFindException>().And.Message.Should().Contain(string.Format(LocalizableStrings.CannotFindAnyManifestsFileSearched, ""));
        }

        [Fact]
        public void GivenNoManifestFileItThrows()
        {
            var toolManifest = new ToolManifestFinder(new DirectoryPath(_testDirectoryRoot), _fileSystem);
            Action a = () => toolManifest.Find();
            a.ShouldThrow<ToolManifestCannotFindException>().And.Message.Should().Contain(string.Format(LocalizableStrings.CannotFindAnyManifestsFileSearched, ""));
        }

        [Fact]
        public void GivenMissingFieldManifestFileItReturnError()
        {
            _fileSystem.File.WriteAllText(Path.Combine(_testDirectoryRoot, _manifestFilename), _jsonWithMissingField);
            var toolManifest = new ToolManifestFinder(new DirectoryPath(_testDirectoryRoot), _fileSystem);
            Action a = () => toolManifest.Find();

            a.ShouldThrow<ToolManifestException>().And.Message.Should().Contain(
                LocalizableStrings.InvalidManifestFilePrefix + Environment.NewLine + "  " +
                string.Format(LocalizableStrings.InPackage, "t-rex") + Environment.NewLine + "    " +
                LocalizableStrings.MissingVersion + Environment.NewLine + "    " +
                LocalizableStrings.FieldCommandsIsMissing);
        }

        [Fact]
        public void GivenInvalidFieldsManifestFileItReturnError()
        {
            _fileSystem.File.WriteAllText(Path.Combine(_testDirectoryRoot, _manifestFilename), _jsonWithInvalidField);
            var toolManifest = new ToolManifestFinder(new DirectoryPath(_testDirectoryRoot), _fileSystem);
            Action a = () => toolManifest.Find();

            a.ShouldThrow<ToolManifestException>().And.Message.Should().Contain(string.Format(LocalizableStrings.VersionIsInvalid, "1.*"));
        }

        // Remove this test when the follow pending test is enabled and feature is implemented.
        // https://github.com/dotnet/cli/issues/10032
        [Fact]
        public void RequireRootAndVersionIs1()
        {
            _fileSystem.File.WriteAllText(Path.Combine(_testDirectoryRoot, _manifestFilename), _jsonWithNonRoot);
            var toolManifest = new ToolManifestFinder(new DirectoryPath(_testDirectoryRoot), _fileSystem);
            Action a = () => toolManifest.Find();

            a.ShouldThrow<ToolManifestException>()
                .And.Message.Should()
                .Contain("  isRoot is false is not supported." + Environment.NewLine + "  Tools manifest format version 2 is not supported.");
        }

        [Fact(Skip = "pending implementation")]
        public void GivenConflictedManifestFileInDifferentFieldsItReturnMergedContent()
        {
        }

        [Fact(Skip = "pending implementation")]
        public void DifferentVersionOfManifestFileItShouldHaveWarnings()
        {
        }

        [Fact(Skip = "pending implementation")]
        public void DifferentVersionOfManifestFileItShouldNotThrow()
        {
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

        private string _jsonWithDuplicatedPackagedId =
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
      ""t-rex"":{  
         ""version"":""2.1.4"",
         ""commands"":[  
            ""t-rex""
         ]
      }
   }
}";

        private string _jsonWithMissingField =
            @"{  
   ""version"":1,
   ""isRoot"":true,
   ""tools"":{  
      ""t-rex"":{  
         ""extra"":1
      }
   }
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

        private string _jsonWithNonRoot =
            @"{  
   ""version"":2,
   ""isRoot"":false,
   ""tools"":{  
      ""t-rex"":{  
         ""version"":""1.0.53"",
         ""commands"":[  
            ""trex""
         ]
      }
   }
}";

        private readonly List<ToolManifestPackage> _defaultExpectedResult;
        private readonly string _testDirectoryRoot;
        private const string _manifestFilename = "localtool.manifest.json";
    }
}
