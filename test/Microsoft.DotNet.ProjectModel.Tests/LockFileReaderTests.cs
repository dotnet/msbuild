// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Linq;
using System.Text;
using FluentAssertions;
using Microsoft.DotNet.ProjectModel.Graph;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

namespace Microsoft.DotNet.ProjectModel.Tests
{
    public class LockFileReaderTests : TestBase
    {
        [Fact]
        public void ReadsAllLibraryPropertiesWhenPathIsPresent()
        {
            // Arrange
            var lockFileJson = @"
            {
              ""libraries"": {
                ""PackageA/1.0.1-Alpha"": {
                  ""sha512"": ""FAKE-HASH"",
                  ""type"": ""package"",
                  ""serviceable"": true,
                  ""files"": [
                    ""a.txt"",
                    ""foo/b.txt""
                  ],
                  ""path"": ""PackageA/1.0.1-beta-PATH""
                },
                ""ProjectA/1.0.2-Beta"": {
                  ""type"": ""project"",
                  ""path"": ""ProjectA-PATH"",
                  ""msbuildProject"": ""some-msbuild""
                }
              }
            }";

            var lockFileStream = new MemoryStream(Encoding.UTF8.GetBytes(lockFileJson));
            var lockFileReader = new LockFileReader();

            // Act
            var lockFile = lockFileReader.ReadLockFile(
                lockFilePath: null,
                stream: lockFileStream,
                designTime: true);

            // Assert
            lockFile.PackageLibraries.Should().HaveCount(1);
            var package = lockFile.PackageLibraries.First();
            package.Name.Should().Be("PackageA");
            package.Version.ToString().Should().Be("1.0.1-Alpha");
            package.Sha512.Should().Be("FAKE-HASH");
            package.IsServiceable.Should().BeTrue();
            package.Files.Should().HaveCount(2);
            package.Files[0].Should().Be("a.txt");
            package.Files[1].Should().Be(Path.Combine("foo", "b.txt"));
            package.Path.Should().Be("PackageA/1.0.1-beta-PATH");

            lockFile.ProjectLibraries.Should().HaveCount(1);
            var project = lockFile.ProjectLibraries.First();
            project.Name.Should().Be("ProjectA");
            project.Version.ToString().Should().Be("1.0.2-Beta");
            project.Path.Should().Be("ProjectA-PATH");
        }

        [Fact]
        public void ReadsAllLibraryPropertiesWhenPathIsNotPresent()
        {
            // Arrange
            var lockFileJson = @"
            {
              ""libraries"": {
                ""PackageA/1.0.1-Alpha"": {
                  ""sha512"": ""FAKE-HASH"",
                  ""type"": ""package"",
                  ""serviceable"": true,
                  ""files"": [
                    ""a.txt"",
                    ""foo/b.txt""
                  ]
                },
                ""ProjectA/1.0.2-Beta"": {
                  ""type"": ""project"",
                  ""msbuildProject"": ""some-msbuild""
                }
              }
            }";

            var lockFileStream = new MemoryStream(Encoding.UTF8.GetBytes(lockFileJson));
            var lockFileReader = new LockFileReader();

            // Act
            var lockFile = lockFileReader.ReadLockFile(
                lockFilePath: null,
                stream: lockFileStream,
                designTime: true);

            // Assert
            lockFile.PackageLibraries.Should().HaveCount(1);
            var package = lockFile.PackageLibraries.First();
            package.Name.Should().Be("PackageA");
            package.Version.ToString().Should().Be("1.0.1-Alpha");
            package.Sha512.Should().Be("FAKE-HASH");
            package.IsServiceable.Should().BeTrue();
            package.Files.Should().HaveCount(2);
            package.Files[0].Should().Be("a.txt");
            package.Files[1].Should().Be(Path.Combine("foo", "b.txt"));
            package.Path.Should().BeNull();

            lockFile.ProjectLibraries.Should().HaveCount(1);
            var project = lockFile.ProjectLibraries.First();
            project.Name.Should().Be("ProjectA");
            project.Version.ToString().Should().Be("1.0.2-Beta");
            project.Path.Should().BeNull();
        }
    }
}
