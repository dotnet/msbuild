// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Moq;

namespace Microsoft.NET.Sdk.Razor.Tests
{
    public class DiscoverStaticWebAssetsTest
    {
        [Fact]
        public void DiscoversMatchingAssetsBasedOnPattern()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new DiscoverStaticWebAssets
            {
                BuildEngine = buildEngine.Object,
                Candidates = new[]
                {
                    CreateCandidate(Path.Combine("wwwroot", "candidate.js"))
                },
                Pattern = "wwwroot\\**",
                SourceId = "MyProject",
                ContentRoot = "wwwroot",
                BasePath = "_content/Path"
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true);
            task.DiscoveredStaticWebAssets.Length.Should().Be(1);
            var asset = task.DiscoveredStaticWebAssets[0];
            asset.ItemSpec.Should().Be(Path.GetFullPath(Path.Combine("wwwroot", "candidate.js")));
            asset.GetMetadata(nameof(StaticWebAsset.SourceId)).Should().Be("MyProject");
            asset.GetMetadata(nameof(StaticWebAsset.SourceType)).Should().Be("Discovered");
            asset.GetMetadata(nameof(StaticWebAsset.ContentRoot)).Should().Be(Path.GetFullPath("wwwroot") + Path.DirectorySeparatorChar);
            asset.GetMetadata(nameof(StaticWebAsset.BasePath)).Should().Be("_content/Path");
            asset.GetMetadata(nameof(StaticWebAsset.RelativePath)).Should().Be("candidate.js");
            asset.GetMetadata(nameof(StaticWebAsset.AssetKind)).Should().Be("All");
            asset.GetMetadata(nameof(StaticWebAsset.AssetMode)).Should().Be("All");
            asset.GetMetadata(nameof(StaticWebAsset.AssetRole)).Should().Be("Primary");
            asset.GetMetadata(nameof(StaticWebAsset.RelatedAsset)).Should().Be("");
            asset.GetMetadata(nameof(StaticWebAsset.AssetTraitName)).Should().Be("");
            asset.GetMetadata(nameof(StaticWebAsset.AssetTraitValue)).Should().Be("");
            asset.GetMetadata(nameof(StaticWebAsset.CopyToOutputDirectory)).Should().Be("Never");
            asset.GetMetadata(nameof(StaticWebAsset.CopyToPublishDirectory)).Should().Be("PreserveNewest");
            asset.GetMetadata(nameof(StaticWebAsset.OriginalItemSpec)).Should().Be(Path.Combine("wwwroot", "candidate.js"));
        }

        [Fact]
        public void RespectsItemRelativePathWhenExplicitlySpecified()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new DiscoverStaticWebAssets
            {
                BuildEngine = buildEngine.Object,
                Candidates = new[]
                {
                    CreateCandidate(Path.Combine("wwwroot", "candidate.js"), relativePath: "subdir/candidate.js")
                },
                Pattern = "wwwroot\\**",
                SourceId = "MyProject",
                ContentRoot = "wwwroot",
                BasePath = "_content/Path"
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true);
            task.DiscoveredStaticWebAssets.Length.Should().Be(1);
            var asset = task.DiscoveredStaticWebAssets[0];
            asset.ItemSpec.Should().Be(Path.GetFullPath(Path.Combine("wwwroot", "candidate.js")));
            asset.GetMetadata(nameof(StaticWebAsset.SourceId)).Should().Be("MyProject");
            asset.GetMetadata(nameof(StaticWebAsset.SourceType)).Should().Be("Discovered");
            asset.GetMetadata(nameof(StaticWebAsset.ContentRoot)).Should().Be(Path.GetFullPath("wwwroot") + Path.DirectorySeparatorChar);
            asset.GetMetadata(nameof(StaticWebAsset.BasePath)).Should().Be("_content/Path");
            asset.GetMetadata(nameof(StaticWebAsset.RelativePath)).Should().Be("subdir/candidate.js");
            asset.GetMetadata(nameof(StaticWebAsset.AssetKind)).Should().Be("All");
            asset.GetMetadata(nameof(StaticWebAsset.AssetMode)).Should().Be("All");
            asset.GetMetadata(nameof(StaticWebAsset.AssetRole)).Should().Be("Primary");
            asset.GetMetadata(nameof(StaticWebAsset.RelatedAsset)).Should().Be("");
            asset.GetMetadata(nameof(StaticWebAsset.AssetTraitName)).Should().Be("");
            asset.GetMetadata(nameof(StaticWebAsset.AssetTraitValue)).Should().Be("");
            asset.GetMetadata(nameof(StaticWebAsset.CopyToOutputDirectory)).Should().Be("Never");
            asset.GetMetadata(nameof(StaticWebAsset.CopyToPublishDirectory)).Should().Be("PreserveNewest");
            asset.GetMetadata(nameof(StaticWebAsset.OriginalItemSpec)).Should().Be(Path.Combine("wwwroot", "candidate.js"));
        }

        [Fact]
        public void UsesTargetPathWhenFound()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new DiscoverStaticWebAssets
            {
                BuildEngine = buildEngine.Object,
                Candidates = new[]
                {
                    CreateCandidate(Path.Combine("wwwroot", "candidate.js"), targetPath: Path.Combine("wwwroot", "subdir", "candidate.publish.js"))
                },
                Pattern = "wwwroot\\**",
                SourceId = "MyProject",
                ContentRoot = "wwwroot",
                BasePath = "_content/Path"
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true);
            task.DiscoveredStaticWebAssets.Length.Should().Be(1);
            var asset = task.DiscoveredStaticWebAssets[0];
            asset.ItemSpec.Should().Be(Path.GetFullPath(Path.Combine("wwwroot", "candidate.js")));
            asset.GetMetadata(nameof(StaticWebAsset.SourceId)).Should().Be("MyProject");
            asset.GetMetadata(nameof(StaticWebAsset.SourceType)).Should().Be("Discovered");
            asset.GetMetadata(nameof(StaticWebAsset.ContentRoot)).Should().Be(Path.GetFullPath("wwwroot") + Path.DirectorySeparatorChar);
            asset.GetMetadata(nameof(StaticWebAsset.BasePath)).Should().Be("_content/Path");
            asset.GetMetadata(nameof(StaticWebAsset.RelativePath)).Should().Be("subdir/candidate.publish.js");
            asset.GetMetadata(nameof(StaticWebAsset.AssetKind)).Should().Be("All");
            asset.GetMetadata(nameof(StaticWebAsset.AssetMode)).Should().Be("All");
            asset.GetMetadata(nameof(StaticWebAsset.AssetRole)).Should().Be("Primary");
            asset.GetMetadata(nameof(StaticWebAsset.RelatedAsset)).Should().Be("");
            asset.GetMetadata(nameof(StaticWebAsset.AssetTraitName)).Should().Be("");
            asset.GetMetadata(nameof(StaticWebAsset.AssetTraitValue)).Should().Be("");
            asset.GetMetadata(nameof(StaticWebAsset.CopyToOutputDirectory)).Should().Be("Never");
            asset.GetMetadata(nameof(StaticWebAsset.CopyToPublishDirectory)).Should().Be("PreserveNewest");
            asset.GetMetadata(nameof(StaticWebAsset.OriginalItemSpec)).Should().Be(Path.Combine("wwwroot", "candidate.js"));
        }

        [Fact]
        public void UsesLinkPathWhenFound()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new DiscoverStaticWebAssets
            {
                BuildEngine = buildEngine.Object,
                Candidates = new[]
                {
                    CreateCandidate(Path.Combine("wwwroot", "candidate.js"), link: Path.Combine("wwwroot", "subdir", "candidate.link.js"))
                },
                Pattern = "wwwroot\\**",
                SourceId = "MyProject",
                ContentRoot = "wwwroot",
                BasePath = "_content/Path"
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true);
            task.DiscoveredStaticWebAssets.Length.Should().Be(1);
            var asset = task.DiscoveredStaticWebAssets[0];
            asset.ItemSpec.Should().Be(Path.GetFullPath(Path.Combine("wwwroot", "candidate.js")));
            asset.GetMetadata(nameof(StaticWebAsset.SourceId)).Should().Be("MyProject");
            asset.GetMetadata(nameof(StaticWebAsset.SourceType)).Should().Be("Discovered");
            asset.GetMetadata(nameof(StaticWebAsset.ContentRoot)).Should().Be(Path.GetFullPath("wwwroot") + Path.DirectorySeparatorChar);
            asset.GetMetadata(nameof(StaticWebAsset.BasePath)).Should().Be("_content/Path");
            asset.GetMetadata(nameof(StaticWebAsset.RelativePath)).Should().Be("subdir/candidate.link.js");
            asset.GetMetadata(nameof(StaticWebAsset.AssetKind)).Should().Be("All");
            asset.GetMetadata(nameof(StaticWebAsset.AssetMode)).Should().Be("All");
            asset.GetMetadata(nameof(StaticWebAsset.AssetRole)).Should().Be("Primary");
            asset.GetMetadata(nameof(StaticWebAsset.RelatedAsset)).Should().Be("");
            asset.GetMetadata(nameof(StaticWebAsset.AssetTraitName)).Should().Be("");
            asset.GetMetadata(nameof(StaticWebAsset.AssetTraitValue)).Should().Be("");
            asset.GetMetadata(nameof(StaticWebAsset.CopyToOutputDirectory)).Should().Be("Never");
            asset.GetMetadata(nameof(StaticWebAsset.CopyToPublishDirectory)).Should().Be("PreserveNewest");
            asset.GetMetadata(nameof(StaticWebAsset.OriginalItemSpec)).Should().Be(Path.Combine("wwwroot", "candidate.js"));
        }

        [Fact]
        public void AutomaticallyDetectsAssetKindWhenMultipleAssetsTargetTheSameRelativePath()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new DiscoverStaticWebAssets
            {
                BuildEngine = buildEngine.Object,
                Candidates = new[]
                {
                    CreateCandidate(Path.Combine("wwwroot", "candidate.js"), copyToPublishDirectory: "Never"),
                    CreateCandidate(Path.Combine("wwwroot", "candidate.publish.js"), relativePath: "candidate.js")
                },
                Pattern = "wwwroot\\**",
                SourceId = "MyProject",
                ContentRoot = "wwwroot",
                BasePath = "_content/Path"
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true);
            task.DiscoveredStaticWebAssets.Length.Should().Be(2);
            var buildAsset = task.DiscoveredStaticWebAssets.Single(a => a.ItemSpec == Path.GetFullPath(Path.Combine("wwwroot", "candidate.js")));
            var publishAsset = task.DiscoveredStaticWebAssets.Single(a => a.ItemSpec == Path.GetFullPath(Path.Combine("wwwroot", "candidate.publish.js")));
            buildAsset.ItemSpec.Should().Be(Path.GetFullPath(Path.Combine("wwwroot", "candidate.js")));
            buildAsset.GetMetadata(nameof(StaticWebAsset.AssetKind)).Should().Be("Build");
            buildAsset.GetMetadata(nameof(StaticWebAsset.CopyToOutputDirectory)).Should().Be("Never");
            buildAsset.GetMetadata(nameof(StaticWebAsset.CopyToPublishDirectory)).Should().Be("Never");

            publishAsset.ItemSpec.Should().Be(Path.GetFullPath(Path.Combine("wwwroot", "candidate.publish.js")));
            publishAsset.GetMetadata(nameof(StaticWebAsset.AssetKind)).Should().Be("Publish");
            publishAsset.GetMetadata(nameof(StaticWebAsset.CopyToOutputDirectory)).Should().Be("Never");
            publishAsset.GetMetadata(nameof(StaticWebAsset.CopyToPublishDirectory)).Should().Be("PreserveNewest");
        }

        [Theory]
        [InlineData("Never", "Never", "Build", "Never", "Never", "Build")]
        [InlineData("PreserveNewest", "PreserveNewest", "All", "PreserveNewest", "PreserveNewest", "All")]
        [InlineData("Always", "Always", "All", "Always", "Always", "All")]
        [InlineData("Never", "Always", "All", "Never", "Always", "All")]
        [InlineData("Always", "Never", "Build", "Always", "Never", "Build")]
        public void FailsDiscoveringAssetsWhenThereIsAConflict(
            string copyToOutputDirectoryFirst,
            string copyToPublishDirectoryFirst,
            string firstKind,
            string copyToOutputDirectorySecond,
            string copyToPublishDirectorySecond,
            string secondKind)
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new DiscoverStaticWebAssets
            {
                BuildEngine = buildEngine.Object,
                Candidates = new[]
                {
                    CreateCandidate(
                        Path.Combine("wwwroot","candidate.js"),
                        copyToOutputDirectory: copyToOutputDirectoryFirst,
                        copyToPublishDirectory: copyToPublishDirectoryFirst),

                    CreateCandidate(
                        Path.Combine("wwwroot","candidate.publish.js"),
                        relativePath: "candidate.js",
                        copyToOutputDirectory: copyToOutputDirectorySecond,
                        copyToPublishDirectory: copyToPublishDirectorySecond)
                },
                Pattern = "wwwroot\\**",
                SourceId = "MyProject",
                ContentRoot = "wwwroot",
                BasePath = "_content/Path"
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(false);
            errorMessages.Count.Should().Be(1);
            errorMessages[0].Should().Be($@"Two assets found targeting the same path with incompatible asset kinds: 
'{Path.GetFullPath(Path.Combine("wwwroot", "candidate.js"))}' with kind '{firstKind}'
'{Path.GetFullPath(Path.Combine("wwwroot", "candidate.publish.js"))}' with kind '{secondKind}'
for path 'candidate.js'");
        }

        [Theory]
        [InlineData("\\_content\\Path\\", "_content/Path")]
        [InlineData("\\_content\\Path", "_content/Path")]
        [InlineData("_content\\Path", "_content/Path")]
        [InlineData("/_content/Path/", "_content/Path")]
        [InlineData("/_content/Path", "_content/Path")]
        [InlineData("_content/Path", "_content/Path")]
        [InlineData("\\_content/Path\\", "_content/Path")]
        [InlineData("/_content\\Path/", "_content/Path")]
        [InlineData("", "/")]
        [InlineData("/", "/")]
        [InlineData("\\", "/")]
        public void NormalizesBasePath(string givenPath, string expectedPath)
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new DiscoverStaticWebAssets
            {
                BuildEngine = buildEngine.Object,
                Candidates = new[]
                {
                    CreateCandidate("wwwroot\\candidate.js")
                },
                Pattern = "wwwroot\\**",
                SourceId = "MyProject",
                ContentRoot = "wwwroot",
                BasePath = givenPath
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true);
            task.DiscoveredStaticWebAssets.Length.Should().Be(1);
            var asset = task.DiscoveredStaticWebAssets[0];
            asset.ItemSpec.Should().Be(Path.GetFullPath(Path.Combine("wwwroot", "candidate.js")));
            asset.GetMetadata(nameof(StaticWebAsset.BasePath)).Should().Be(expectedPath);
        }

        public static TheoryData<string, string> NormalizesContentRootData
        {
            get
            {
                var currentPath = Path.GetFullPath(".");
                var result = new TheoryData<string, string>();
                result.Add("wwwroot", Path.GetFullPath("wwwroot") + Path.DirectorySeparatorChar);
                result.Add(currentPath + Path.DirectorySeparatorChar + "wwwroot" + Path.DirectorySeparatorChar + "subdir", Path.GetFullPath("wwwroot/subdir") + Path.DirectorySeparatorChar);
                result.Add(currentPath + Path.DirectorySeparatorChar + "wwwroot" + Path.DirectorySeparatorChar + "subdir" + Path.DirectorySeparatorChar, Path.GetFullPath("wwwroot/subdir") + Path.DirectorySeparatorChar);
                result.Add(currentPath + Path.DirectorySeparatorChar + "wwwroot" + Path.DirectorySeparatorChar + "subdir" + Path.AltDirectorySeparatorChar, Path.GetFullPath("wwwroot/subdir") + Path.DirectorySeparatorChar);
                result.Add(currentPath + Path.AltDirectorySeparatorChar + "wwwroot" + Path.AltDirectorySeparatorChar + "subdir", Path.GetFullPath("wwwroot/subdir") + Path.DirectorySeparatorChar);
                result.Add(currentPath + Path.DirectorySeparatorChar + "wwwroot" + Path.AltDirectorySeparatorChar + "subdir", Path.GetFullPath("wwwroot/subdir") + Path.DirectorySeparatorChar);
                result.Add(currentPath + Path.AltDirectorySeparatorChar + "wwwroot" + Path.DirectorySeparatorChar + "subdir", Path.GetFullPath("wwwroot/subdir") + Path.DirectorySeparatorChar);
                return result;
            }
        }

        [Theory]
        [MemberData(nameof(NormalizesContentRootData))]
        public void NormalizesContentRoot(string contentRoot, string expected)
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new DiscoverStaticWebAssets
            {
                BuildEngine = buildEngine.Object,
                Candidates = new[]
                {
                    CreateCandidate("wwwroot\\candidate.js")
                },
                Pattern = "wwwroot\\**",
                SourceId = "MyProject",
                ContentRoot = contentRoot,
                BasePath = "base"
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true);
            task.DiscoveredStaticWebAssets.Length.Should().Be(1);
            var asset = task.DiscoveredStaticWebAssets[0];
            asset.ItemSpec.Should().Be(Path.GetFullPath(Path.Combine("wwwroot", "candidate.js")));
            asset.GetMetadata(nameof(StaticWebAsset.ContentRoot)).Should().Be(expected);
        }


        private ITaskItem CreateCandidate(
            string itemSpec,
            string relativePath = null,
            string targetPath = null,
            string link = null,
            string copyToOutputDirectory = null,
            string copyToPublishDirectory = null)
        {
            return new TaskItem(itemSpec, new Dictionary<string, string>
            {
                ["RelativePath"] = relativePath ?? "",
                ["TargetPath"] = targetPath ?? "",
                ["Link"] = link ?? "",
                ["CopyToOutputDirectory"] = copyToOutputDirectory ?? "",
                ["CopyToPublishDirectory"] = copyToPublishDirectory ?? ""
            });
        }
    }
}
