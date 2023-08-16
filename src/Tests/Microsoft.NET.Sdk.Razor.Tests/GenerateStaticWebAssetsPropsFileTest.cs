// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Moq;

namespace Microsoft.NET.Sdk.Razor.Test
{
    public class GenerateStaticWebAssetsPropsFileTest
    {
        [Fact]
        public void Fails_WhenStaticWebAsset_DoesNotContainSourceType()
        {
            // Arrange
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new GenerateStaticWebAsssetsPropsFile
            {
                BuildEngine = buildEngine.Object,
                StaticWebAssets = new TaskItem[]
                {
                    CreateItem(Path.Combine("wwwroot","js","sample.js"), new Dictionary<string,string>
                    {
                        ["SourceId"] = "MyLibrary",
                        ["ContentRoot"] = @"$(MSBuildThisFileDirectory)..\staticwebassets",
                        ["BasePath"] = "_content/mylibrary",
                        ["RelativePath"] = Path.Combine("js", "sample.js"),
                    })
                }
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().BeFalse();
            var expectedError = $"Missing required metadata 'SourceType' for '{Path.Combine("wwwroot", "js", "sample.js")}'.";
            errorMessages.Should().ContainSingle(message => message == expectedError);
        }

        [Fact]
        public void Fails_WhenStaticWebAsset_DoesNotContainSourceId()
        {
            // Arrange
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new GenerateStaticWebAsssetsPropsFile
            {
                BuildEngine = buildEngine.Object,
                StaticWebAssets = new TaskItem[]
                {
                    CreateItem(Path.Combine("wwwroot","js","sample.js"), new Dictionary<string,string>
                    {
                        ["SourceType"] = "Discovered",
                        ["ContentRoot"] = @"$(MSBuildThisFileDirectory)..\staticwebassets",
                        ["BasePath"] = "_content/mylibrary",
                        ["RelativePath"] = Path.Combine("js", "sample.js"),
                    })
                }
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().BeFalse();
            var expectedError = $"Missing required metadata 'SourceId' for '{Path.Combine("wwwroot", "js", "sample.js")}'.";
            errorMessages.Should().ContainSingle(message => message == expectedError);
        }

        [Fact]
        public void Fails_WhenStaticWebAsset_DoesNotContainContentRoot()
        {
            // Arrange
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new GenerateStaticWebAsssetsPropsFile
            {
                BuildEngine = buildEngine.Object,
                StaticWebAssets = new TaskItem[]
                {
                    CreateItem(Path.Combine("wwwroot","js","sample.js"), new Dictionary<string,string>
                    {
                        ["SourceType"] = "Discovered",
                        ["SourceId"] = "MyLibrary",
                        ["BasePath"] = "_content/mylibrary",
                        ["RelativePath"] = Path.Combine("js", "sample.js"),
                    })
                }
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().BeFalse();
            var expectedError = $"Missing required metadata 'ContentRoot' for '{Path.Combine("wwwroot", "js", "sample.js")}'.";
            errorMessages.Should().ContainSingle(message => message == expectedError);
        }

        [Fact]
        public void Fails_WhenStaticWebAsset_DoesNotContainBasePath()
        {
            // Arrange
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new GenerateStaticWebAsssetsPropsFile
            {
                BuildEngine = buildEngine.Object,
                StaticWebAssets = new TaskItem[]
                {
                    CreateItem(Path.Combine("wwwroot","js","sample.js"), new Dictionary<string,string>
                    {
                        ["SourceType"] = "Discovered",
                        ["SourceId"] = "MyLibrary",
                        ["ContentRoot"] = @"$(MSBuildThisFileDirectory)..\staticwebassets",
                        ["RelativePath"] = Path.Combine("js", "sample.js"),
                    })
                }
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().BeFalse();
            var expectedError = $"Missing required metadata 'BasePath' for '{Path.Combine("wwwroot", "js", "sample.js")}'.";
            errorMessages.Should().ContainSingle(message => message == expectedError);
        }

        [Fact]
        public void Fails_WhenStaticWebAsset_DoesNotContainRelativePath()
        {
            // Arrange
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new GenerateStaticWebAsssetsPropsFile
            {
                BuildEngine = buildEngine.Object,
                StaticWebAssets = new TaskItem[]
                {
                    CreateItem(Path.Combine("wwwroot","js","sample.js"), new Dictionary<string,string>
                    {
                        ["SourceType"] = "Discovered",
                        ["SourceId"] = "MyLibrary",
                        ["ContentRoot"] = @"$(MSBuildThisFileDirectory)..\staticwebassets",
                        ["BasePath"] = "_content/mylibrary",
                        ["AssetKind"] = "All",
                        ["AssetMode"] = "All",
                        ["AssetRole"] = "Primary",
                        ["RelatedAsset"] = "",
                        ["AssetTraitName"] = "",
                        ["AssetTraitValue"] = "",
                        ["CopyToOutputDirectory"] = "Never",
                        ["CopyToPublishDirectory"] = "PreserveNewest"
                    })
                }
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().BeFalse();
            var expectedError = $"Missing required metadata 'RelativePath' for '{Path.Combine("wwwroot", "js", "sample.js")}'.";
            errorMessages.Should().ContainSingle(message => message == expectedError);
        }

        [Fact]
        public void Fails_WhenStaticWebAsset_HasInvalidSourceType()
        {
            // Arrange

            var expectedError = $"Static web asset '{Path.Combine("wwwroot", "css", "site.css")}' has invalid source type 'Package'.";

            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new GenerateStaticWebAsssetsPropsFile
            {
                BuildEngine = buildEngine.Object,
                StaticWebAssets = new TaskItem[]
                {
                    CreateItem(Path.Combine("wwwroot","js","sample.js"), new Dictionary<string,string>
                    {
                        ["SourceType"] = "Discovered",
                        ["SourceId"] = "MyLibrary",
                        ["ContentRoot"] = @"$(MSBuildThisFileDirectory)..\staticwebassets",
                        ["BasePath"] = "_content/mylibrary",
                        ["RelativePath"] = Path.Combine("js", "sample.js"),
                        ["AssetKind"] = "All",
                        ["AssetMode"] = "All",
                        ["AssetRole"] = "Primary",
                        ["RelatedAsset"] = "",
                        ["AssetTraitName"] = "",
                        ["AssetTraitValue"] = "",
                        ["CopyToOutputDirectory"] = "Never",
                        ["CopyToPublishDirectory"] = "PreserveNewest"
                    }),
                    CreateItem(Path.Combine("wwwroot","css","site.css"), new Dictionary<string,string>
                    {
                        ["SourceType"] = "Package",
                        ["SourceId"] = "MyLibrary",
                        ["ContentRoot"] = @"$(MSBuildThisFileDirectory)..\staticwebassets",
                        ["BasePath"] = "_content/mylibrary",
                        ["RelativePath"] = Path.Combine("css", "site.css"),
                        ["AssetKind"] = "All",
                        ["AssetMode"] = "All",
                        ["AssetRole"] = "Primary",
                        ["RelatedAsset"] = "",
                        ["AssetTraitName"] = "",
                        ["AssetTraitValue"] = "",
                        ["CopyToOutputDirectory"] = "Never",
                        ["CopyToPublishDirectory"] = "PreserveNewest"
                    })
                }
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().BeFalse();
            errorMessages.Should().ContainSingle(message => message == expectedError);
        }

        [Fact]
        public void Fails_WhenStaticWebAsset_HaveDifferentSourceId()
        {
            // Arrange
            var expectedError = "Static web assets have different 'SourceId' metadata values " +
                "'MyLibrary' and 'MyLibrary2' " +
                $"for '{Path.Combine("wwwroot", "js", "sample.js")}' and '{Path.Combine("wwwroot", "css", "site.css")}'.";

            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new GenerateStaticWebAsssetsPropsFile
            {
                BuildEngine = buildEngine.Object,
                StaticWebAssets = new TaskItem[]
                {
                    CreateItem(Path.Combine("wwwroot","js","sample.js"), new Dictionary<string,string>
                    {
                        ["SourceType"] = "Discovered",
                        ["SourceId"] = "MyLibrary",
                        ["ContentRoot"] = @"$(MSBuildThisFileDirectory)..\staticwebassets",
                        ["BasePath"] = "_content/mylibrary",
                        ["RelativePath"] = Path.Combine("js", "sample.js"),
                        ["AssetKind"] = "All",
                        ["AssetMode"] = "All",
                        ["AssetRole"] = "Primary",
                        ["RelatedAsset"] = "",
                        ["AssetTraitName"] = "",
                        ["AssetTraitValue"] = "",
                        ["CopyToOutputDirectory"] = "Never",
                        ["CopyToPublishDirectory"] = "PreserveNewest"
                    }),
                    CreateItem(Path.Combine("wwwroot","css","site.css"), new Dictionary<string,string>
                    {
                        ["SourceType"] = "Discovered",
                        ["SourceId"] = "MyLibrary2",
                        ["ContentRoot"] = @"$(MSBuildThisFileDirectory)..\staticwebassets",
                        ["BasePath"] = "_content/mylibrary",
                        ["RelativePath"] = Path.Combine("css", "site.css"),
                        ["AssetKind"] = "All",
                        ["AssetMode"] = "All",
                        ["AssetRole"] = "Primary",
                        ["RelatedAsset"] = "",
                        ["AssetTraitName"] = "",
                        ["AssetTraitValue"] = "",
                        ["CopyToOutputDirectory"] = "Never",
                        ["CopyToPublishDirectory"] = "PreserveNewest"
                    })
                }
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().BeFalse();
            errorMessages.Should().ContainSingle(message => message == expectedError);
        }

        [Fact(Skip = "https://github.com/dotnet/aspnetcore/issues/24257")]
        public void Fails_WhenStaticWebAsset_HaveDifferentContentRoot()
        {
            // Arrange
            var expectedError = "Static web assets have different 'ContentRoot' metadata values " +
                @"'$(MSBuildThisFileDirectory)..\staticwebassets' and '..\staticwebassets' " +
                $"for '{Path.Combine("wwwroot", "js", "sample.js")}' and '{Path.Combine("wwwroot", "css", "site.css")}'.";

            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new GenerateStaticWebAsssetsPropsFile
            {
                BuildEngine = buildEngine.Object,
                StaticWebAssets = new TaskItem[]
                {
                    CreateItem(Path.Combine("wwwroot","js","sample.js"), new Dictionary<string,string>
                    {
                        ["SourceType"] = "Discovered",
                        ["SourceId"] = "MyLibrary",
                        ["ContentRoot"] = @"$(MSBuildThisFileDirectory)..\staticwebassets",
                        ["BasePath"] = "_content/mylibrary",
                        ["RelativePath"] = Path.Combine("js", "sample.js"),
                        ["AssetKind"] = "All",
                        ["AssetMode"] = "All",
                        ["AssetRole"] = "Primary",
                        ["RelatedAsset"] = "",
                        ["AssetTraitName"] = "",
                        ["AssetTraitValue"] = "",
                        ["CopyToOutputDirectory"] = "Never",
                        ["CopyToPublishDirectory"] = "PreserveNewest"
                    }),
                    CreateItem(Path.Combine("wwwroot","css","site.css"), new Dictionary<string,string>
                    {
                        ["SourceType"] = "Discovered",
                        ["SourceId"] = "MyLibrary",
                        ["ContentRoot"] = @"..\staticwebassets",
                        ["BasePath"] = "_content/mylibrary",
                        ["RelativePath"] = Path.Combine("css", "site.css"),
                        ["AssetKind"] = "All",
                        ["AssetMode"] = "All",
                        ["AssetRole"] = "Primary",
                        ["RelatedAsset"] = "",
                        ["AssetTraitName"] = "",
                        ["AssetTraitValue"] = "",
                        ["CopyToOutputDirectory"] = "Never",
                        ["CopyToPublishDirectory"] = "PreserveNewest"
                    })
                }
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().BeFalse();
            errorMessages.Should().ContainSingle(message => message == expectedError);
        }

        [Fact]
        public void WritesPropsFile_WhenThereIsAtLeastOneStaticAsset()
        {
            // Arrange
            var file = Path.GetTempFileName();
            var expectedDocument = @"<Project>
  <ItemGroup>
    <StaticWebAsset Include=""$([System.IO.Path]::GetFullPath($(MSBuildThisFileDirectory)..\staticwebassets\js\sample.js))"">
      <SourceType>Package</SourceType>
      <SourceId>MyLibrary</SourceId>
      <ContentRoot>$(MSBuildThisFileDirectory)..\staticwebassets\</ContentRoot>
      <BasePath>_content/mylibrary</BasePath>
      <RelativePath>js/sample.js</RelativePath>
      <AssetKind>All</AssetKind>
      <AssetMode>All</AssetMode>
      <AssetRole>Primary</AssetRole>
      <RelatedAsset></RelatedAsset>
      <AssetTraitName></AssetTraitName>
      <AssetTraitValue></AssetTraitValue>
      <CopyToOutputDirectory>Never</CopyToOutputDirectory>
      <CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>
      <OriginalItemSpec>$([System.IO.Path]::GetFullPath($(MSBuildThisFileDirectory)..\staticwebassets\js\sample.js))</OriginalItemSpec>
    </StaticWebAsset>
  </ItemGroup>
</Project>";

            try
            {
                var buildEngine = new Mock<IBuildEngine>();

                var task = new GenerateStaticWebAsssetsPropsFile
                {
                    BuildEngine = buildEngine.Object,
                    TargetPropsFilePath = file,
                    StaticWebAssets = new TaskItem[]
                    {
                        CreateItem(Path.Combine("wwwroot","js","sample.js"), new Dictionary<string,string>
                        {
                            ["SourceType"] = "Discovered",
                            ["SourceId"] = "MyLibrary",
                            ["ContentRoot"] = @"$(MSBuildThisFileDirectory)..\staticwebassets",
                            ["BasePath"] = "_content/mylibrary",
                            ["RelativePath"] = Path.Combine("js", "sample.js").Replace("\\","/"),
                            ["AssetKind"] = "All",
                            ["AssetMode"] = "All",
                            ["AssetRole"] = "Primary",
                            ["RelatedAsset"] = "",
                            ["AssetTraitName"] = "",
                            ["AssetTraitValue"] = "",
                            ["CopyToOutputDirectory"] = "Never",
                            ["CopyToPublishDirectory"] = "PreserveNewest"
                        }),
                    }
                };

                // Act
                var result = task.Execute();

                // Assert
                result.Should().BeTrue();
                var document = File.ReadAllText(file);
                document.Should().Contain(expectedDocument);
            }
            finally
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
        }

        [Fact]
        public void WritesIndividualItems_WithTheirRespectiveBaseAndRelativePaths()
        {
            // Arrange
            var file = Path.GetTempFileName();
            var expectedDocument = @"<Project>
  <ItemGroup>
    <StaticWebAsset Include=""$([System.IO.Path]::GetFullPath($(MSBuildThisFileDirectory)..\staticwebassets\App.styles.css))"">
      <SourceType>Package</SourceType>
      <SourceId>MyLibrary</SourceId>
      <ContentRoot>$(MSBuildThisFileDirectory)..\staticwebassets\</ContentRoot>
      <BasePath>/</BasePath>
      <RelativePath>App.styles.css</RelativePath>
      <AssetKind>All</AssetKind>
      <AssetMode>All</AssetMode>
      <AssetRole>Primary</AssetRole>
      <RelatedAsset></RelatedAsset>
      <AssetTraitName></AssetTraitName>
      <AssetTraitValue></AssetTraitValue>
      <CopyToOutputDirectory>Never</CopyToOutputDirectory>
      <CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>
      <OriginalItemSpec>$([System.IO.Path]::GetFullPath($(MSBuildThisFileDirectory)..\staticwebassets\App.styles.css))</OriginalItemSpec>
    </StaticWebAsset>
    <StaticWebAsset Include=""$([System.IO.Path]::GetFullPath($(MSBuildThisFileDirectory)..\staticwebassets\js\sample.js))"">
      <SourceType>Package</SourceType>
      <SourceId>MyLibrary</SourceId>
      <ContentRoot>$(MSBuildThisFileDirectory)..\staticwebassets\</ContentRoot>
      <BasePath>_content/mylibrary</BasePath>
      <RelativePath>js/sample.js</RelativePath>
      <AssetKind>All</AssetKind>
      <AssetMode>All</AssetMode>
      <AssetRole>Primary</AssetRole>
      <RelatedAsset></RelatedAsset>
      <AssetTraitName></AssetTraitName>
      <AssetTraitValue></AssetTraitValue>
      <CopyToOutputDirectory>Never</CopyToOutputDirectory>
      <CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>
      <OriginalItemSpec>$([System.IO.Path]::GetFullPath($(MSBuildThisFileDirectory)..\staticwebassets\js\sample.js))</OriginalItemSpec>
    </StaticWebAsset>
  </ItemGroup>
</Project>";

            try
            {
                var buildEngine = new Mock<IBuildEngine>();

                var task = new GenerateStaticWebAsssetsPropsFile
                {
                    BuildEngine = buildEngine.Object,
                    TargetPropsFilePath = file,
                    StaticWebAssets = new TaskItem[]
                    {
                        CreateItem(Path.Combine("wwwroot","js","sample.js"), new Dictionary<string,string>
                        {
                            ["SourceType"] = "Discovered",
                            ["SourceId"] = "MyLibrary",
                            ["ContentRoot"] = @"$(MSBuildThisFileDirectory)..\staticwebassets",
                            ["BasePath"] = "_content/mylibrary",
                            ["RelativePath"] = Path.Combine("js", "sample.js").Replace("\\","/"),
                            ["AssetKind"] = "All",
                            ["AssetMode"] = "All",
                            ["AssetRole"] = "Primary",
                            ["RelatedAsset"] = "",
                            ["AssetTraitName"] = "",
                            ["AssetTraitValue"] = "",
                            ["CopyToOutputDirectory"] = "Never",
                            ["CopyToPublishDirectory"] = "PreserveNewest"
                        }),
                        CreateItem(Path.Combine("wwwroot","App.styles.css"), new Dictionary<string,string>
                        {
                            ["SourceType"] = "Discovered",
                            ["SourceId"] = "MyLibrary",
                            ["ContentRoot"] = @"$(MSBuildThisFileDirectory)..\staticwebassets",
                            ["BasePath"] = "/",
                            ["RelativePath"] = "App.styles.css",
                            ["AssetKind"] = "All",
                            ["AssetMode"] = "All",
                            ["AssetRole"] = "Primary",
                            ["RelatedAsset"] = "",
                            ["AssetTraitName"] = "",
                            ["AssetTraitValue"] = "",
                            ["CopyToOutputDirectory"] = "Never",
                            ["CopyToPublishDirectory"] = "PreserveNewest"
                        }),
                    }
                };

                // Act
                var result = task.Execute();

                // Assert
                Assert.True(result);
                var document = File.ReadAllText(file);
                Assert.Equal(expectedDocument, document, ignoreLineEndingDifferences: true);
            }
            finally
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
        }

        private static TaskItem CreateItem(
            string spec,
            IDictionary<string, string> metadata)
        {
            var result = new TaskItem(spec);
            foreach (var (key, value) in metadata)
            {
                result.SetMetadata(key, value);
            }

            return result;
        }
    }
}
