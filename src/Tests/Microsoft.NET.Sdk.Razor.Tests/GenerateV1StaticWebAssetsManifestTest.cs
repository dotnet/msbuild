// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Moq;
using Microsoft.AspNetCore.StaticWebAssets.Tasks;

namespace Microsoft.NET.Sdk.Razor.Test
{
    public class GenerateV1StaticWebAssetsManifestTest
    {
        [Fact]
        public void ReturnsError_WhenBasePathIsMissing()
        {
            // Arrange
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new GenerateV1StaticWebAssetsManifest
            {
                BuildEngine = buildEngine.Object,
                ContentRootDefinitions = new TaskItem[]
                {
                    CreateItem(Path.Combine("wwwroot", "sample.js"), new Dictionary<string,string>
                    {
                        ["ContentRoot"] = "/"
                    })
                }
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().BeFalse();
            var expectedError = $"Missing required metadata 'BasePath' for '{Path.Combine("wwwroot", "sample.js")}'.";
            errorMessages.Should().ContainSingle(message => message == expectedError);
        }

        [Fact]
        public void ReturnsError_WhenContentRootIsMissing()
        {
            // Arrange
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new GenerateV1StaticWebAssetsManifest
            {
                BuildEngine = buildEngine.Object,
                ContentRootDefinitions = new TaskItem[]
                {
                    CreateItem(Path.Combine("wwwroot","sample.js"), new Dictionary<string,string>
                    {
                        ["BasePath"] = "MyLibrary"
                    })
                }
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().BeFalse();
            var expectedError = $"Missing required metadata 'ContentRoot' for '{Path.Combine("wwwroot", "sample.js")}'.";
            errorMessages.Should().ContainSingle(message => message == expectedError);
        }

        [Fact]
        public void AllowsMultipleContentRootsWithSameBasePath_ForTheSameSourceId()
        {
            // Arrange
            var file = Path.GetTempFileName();
            var expectedDocument = $@"<StaticWebAssets Version=""1.0"">
  <ContentRoot BasePath=""Blazor.Client"" Path=""{Path.Combine(".", "nuget", "bin", "debug", $"netstandard2.1{Path.DirectorySeparatorChar}")}"" />
  <ContentRoot BasePath=""Blazor.Client"" Path=""{Path.Combine(".", "nuget", $"Blazor.Client{Path.DirectorySeparatorChar}")}"" />
</StaticWebAssets>";

            var buildEngine = new Mock<IBuildEngine>();

            var task = new GenerateV1StaticWebAssetsManifest
            {
                BuildEngine = buildEngine.Object,
                ContentRootDefinitions = new TaskItem[]
                {
                    CreateItem(Path.Combine("wwwroot","sample.js"), new Dictionary<string,string>
                    {
                        ["BasePath"] = "Blazor.Client",
                        ["ContentRoot"] = Path.Combine(".", "nuget","Blazor.Client"),
                        ["SourceId"] = "Blazor.Client"
                    }),
                    CreateItem(Path.Combine("wwwroot", "otherLib.js"), new Dictionary<string,string>
                    {
                        ["BasePath"] = "Blazor.Client",
                        ["ContentRoot"] = Path.Combine(".", "nuget", "bin","debug","netstandard2.1"),
                        ["SourceId"] = "Blazor.Client"
                    })
                },
                TargetManifestPath = file
            };

            try
            {
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
        public void Generates_EmptyManifest_WhenNoItems_Passed()
        {
            // Arrange
            var file = Path.GetTempFileName();
            var expectedDocument = @"<StaticWebAssets Version=""1.0"" />";

            try
            {
                var buildEngine = new Mock<IBuildEngine>();

                var task = new GenerateV1StaticWebAssetsManifest
                {
                    BuildEngine = buildEngine.Object,
                    ContentRootDefinitions = new TaskItem[] { },
                    TargetManifestPath = file
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
        public void Generates_Manifest_WhenContentRootsAvailable()
        {
            // Arrange
            var file = Path.GetTempFileName();
            var expectedDocument = $@"<StaticWebAssets Version=""1.0"">
  <ContentRoot BasePath=""MyLibrary"" Path=""{Path.Combine(".", "nuget", "MyLibrary", $"razorContent{Path.DirectorySeparatorChar}")}"" />
</StaticWebAssets>";

            try
            {
                var buildEngine = new Mock<IBuildEngine>();

                var task = new GenerateV1StaticWebAssetsManifest
                {
                    BuildEngine = buildEngine.Object,
                    ContentRootDefinitions = new TaskItem[]
                    {
                        CreateItem(Path.Combine("wwwroot","sample.js"), new Dictionary<string,string>
                        {
                            ["BasePath"] = "MyLibrary",
                            ["ContentRoot"] = Path.Combine(".", "nuget", "MyLibrary", "razorContent"),
                            ["SourceId"] = "MyLibrary"
                        }),
                    },
                    TargetManifestPath = file
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
        public void SkipsAdditionalElements_WithSameBasePathAndSameContentRoot()
        {
            // Arrange
            var file = Path.GetTempFileName();
            var expectedDocument = $@"<StaticWebAssets Version=""1.0"">
  <ContentRoot BasePath=""Base/MyLibrary"" Path=""{Path.Combine(".", "nuget", "MyLibrary", $"razorContent{Path.DirectorySeparatorChar}")}"" />
</StaticWebAssets>";

            try
            {
                var buildEngine = new Mock<IBuildEngine>();

                var task = new GenerateV1StaticWebAssetsManifest
                {
                    BuildEngine = buildEngine.Object,
                    ContentRootDefinitions = new TaskItem[]
                    {
                        CreateItem(Path.Combine("wwwroot","sample.js"), new Dictionary<string,string>
                        {
                            // Base path needs to be normalized to '/' as it goes in the url
                            ["BasePath"] = "Base\\MyLibrary",
                            ["SourceId"] = "MyLibrary",
                            ["ContentRoot"] = Path.Combine(".", "nuget", "MyLibrary", "razorContent")
                        }),
                        // Comparisons are case insensitive
                        CreateItem(Path.Combine("wwwroot, site.css"), new Dictionary<string,string>
                        {
                            ["BasePath"] = "Base\\MyLIBRARY",
                            ["SourceId"] = "MyLibrary",
                            ["ContentRoot"] = Path.Combine(".", "nuget", "MyLIBRARY", "razorContent")
                        }),
                    },
                    TargetManifestPath = file
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
