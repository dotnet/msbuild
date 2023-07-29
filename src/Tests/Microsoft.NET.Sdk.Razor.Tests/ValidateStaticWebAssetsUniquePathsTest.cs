// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Moq;

namespace Microsoft.AspNetCore.Razor.Tasks
{
    public class ValidateStaticWebAssetsUniquePathsTest
    {
        [Fact]
        public void ReturnsError_WhenStaticWebAssetsWebRootPathMatchesExistingContentItemPath()
        {
            // Arrange
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new ValidateStaticWebAssetsUniquePaths
            {
                BuildEngine = buildEngine.Object,
                StaticWebAssets = new TaskItem[]
                {
                    CreateItem(Path.Combine("wwroot", "js", "project-transitive-dep.js"), new Dictionary<string,string>
                    {
                        ["BasePath"] = "_content/ClassLibrary",
                        ["RelativePath"] = "js/project-transitive-dep.js",
                        ["SourceId"] = "ClassLibrary",
                        ["SourceType"] = "Project",
                    }),
                },
                WebRootFiles = new TaskItem[]
                {
                    CreateItem(Path.Combine("wwwroot", "_content", "ClassLibrary", "js", "project-transitive-dep.js"), new Dictionary<string,string>
                    {
                        ["CopyToPublishDirectory"] = "PreserveNewest",
                        ["ExcludeFromSingleFile"] = "true",
                        ["OriginalItemSpec"] = Path.Combine("wwwroot", "_content", "ClassLibrary", "js", "project-transitive-dep.js"),
                        ["TargetPath"] = Path.Combine("wwwroot", "_content", "ClassLibrary", "js", "project-transitive-dep.js"),
                    })
                }
            };

            var expectedMessage = $"The static web asset '{Path.Combine("wwroot", "js", "project-transitive-dep.js")}' " +
                "has a conflicting web root path '/wwwroot/_content/ClassLibrary/js/project-transitive-dep.js' with the " +
                $"project file '{Path.Combine("wwwroot", "_content", "ClassLibrary", "js", "project-transitive-dep.js")}'.";

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(false);
            errorMessages.Should().NotBeEmpty().And.HaveCount(1);
            errorMessages.Should().Contain(expectedMessage);
        }

        [Fact]
        public void AllowsAssetsHavingTheSameBasePathAcrossDifferentSources_WhenTheirFinalDestinationPathIsDifferent()
        {
            // Arrange
            var task = new ValidateStaticWebAssetsUniquePaths
            {
                StaticWebAssets = new TaskItem[]
                {
                    CreateItem(Path.Combine("wwwroot","sample.js"), new Dictionary<string,string>
                    {
                        ["BasePath"] = "MyLibrary",
                        ["ContentRoot"] = Path.Combine("nuget", "MyLibrary"),
                        ["RelativePath"] = "sample.js",
                        ["SourceId"] = "MyLibrary"
                    }),
                    CreateItem(Path.Combine("wwwroot", "otherLib.js"), new Dictionary<string,string>
                    {
                        ["BasePath"] = "MyLibrary",
                        ["ContentRoot"] = Path.Combine("nuget", "MyOtherLibrary"),
                        ["RelativePath"] = "otherLib.js",
                        ["SourceId"] = "MyOtherLibrary"
                    })
                },
                WebRootFiles = Array.Empty<TaskItem>()
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true);
        }

        [Fact]
        public void AllowsAssetsHavingTheSameContentRootAndDifferentBasePathsAcrossDifferentSources_WhenTheirFinalDestinationPathIsDifferent()
        {
            // Arrange
            var task = new ValidateStaticWebAssetsUniquePaths
            {
                StaticWebAssets = new TaskItem[]
                {
                    CreateItem(Path.Combine("wwwroot","sample.js"), new Dictionary<string,string>
                    {
                        ["BasePath"] = "MyLibrary",
                        ["SourceId"] = "MyLibrary",
                        ["RelativePath"] = "sample.js",
                        ["ContentRoot"] = Path.Combine(".", "MyLibrary")
                    }),
                    CreateItem(Path.Combine("wwwroot", "otherLib.js"), new Dictionary<string,string>
                    {
                        ["BasePath"] = "MyOtherLibrary",
                        ["SourceId"] = "MyOtherLibrary",
                        ["RelativePath"] = "otherlib.js",
                        ["ContentRoot"] = Path.Combine(".", "MyLibrary")
                    })
                },
                WebRootFiles = Array.Empty<TaskItem>()
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true);
        }

        [Fact]
        public void ReturnsError_WhenMultipleStaticWebAssetsHaveTheSameWebRootPath()
        {
            // Arrange
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new ValidateStaticWebAssetsUniquePaths
            {
                BuildEngine = buildEngine.Object,
                StaticWebAssets = new TaskItem[]
                {
                    CreateItem(Path.Combine(".", "Library", "wwwroot", "sample.js"), new Dictionary<string,string>
                    {
                        ["BasePath"] = "/",
                        ["RelativePath"] = "/sample.js",
                    }),
                    CreateItem(Path.Combine(".", "Library", "bin", "dist", "sample.js"), new Dictionary<string,string>
                    {
                        ["BasePath"] = "/",
                        ["RelativePath"] = "/sample.js",
                    })
                }
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(false);
            errorMessages.Should().NotBeEmpty().And.HaveCount(1);
            errorMessages.Should().Contain($"Conflicting assets with the same path '/wwwroot/sample.js' for content root paths '{Path.Combine(".", "Library", "bin", "dist", "sample.js")}' and '{Path.Combine(".", "Library", "wwwroot", "sample.js")}'.");
        }

        [Fact]
        public void ReturnsSuccess_WhenStaticWebAssetsDontConflictWithApplicationContentItems()
        {
            // Arrange
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();

            var task = new ValidateStaticWebAssetsUniquePaths
            {
                BuildEngine = buildEngine.Object,
                StaticWebAssets = new TaskItem[]
                {
                    CreateItem(Path.Combine(".", "Library", "wwwroot", "sample.js"), new Dictionary<string,string>
                    {
                        ["BasePath"] = "/_library",
                        ["RelativePath"] = "/sample.js",
                    }),
                    CreateItem(Path.Combine(".", "Library", "wwwroot", "sample.js"), new Dictionary<string,string>
                    {
                        ["BasePath"] = "/_library",
                        ["RelativePath"] = "/sample.js",
                    })
                },
                WebRootFiles = new TaskItem[]
                {
                    CreateItem(Path.Combine(".", "App", "wwwroot", "sample.js"), new Dictionary<string,string>
                    {
                        ["TargetPath"] = "/SAMPLE.js",
                    })
                }
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true);
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
