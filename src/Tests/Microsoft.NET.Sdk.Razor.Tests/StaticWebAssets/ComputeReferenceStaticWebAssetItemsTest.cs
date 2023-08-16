// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
using Moq;

namespace Microsoft.NET.Sdk.Razor.Tests
{
    public class ComputeReferenceStaticWebAssetItemsTest
    {
        [Fact]
        public void IncludesAssetsFromCurrentProjectAsReferencedAssets()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new ComputeReferenceStaticWebAssetItems
            {
                BuildEngine = buildEngine.Object,
                Source = "MyPackage",
                Assets = new[] { CreateCandidate("wwwroot\\candidate.js", "MyPackage", "Discovered", "candidate.js", "All", "All") },
                Patterns = new ITaskItem[] { },
                AssetKind = "Build",
                ProjectMode = "Default"
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true);
            task.StaticWebAssets.Should().HaveCount(1);
        }

        [Fact]
        public void IncludesPatternsFromCurrentProject()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new ComputeReferenceStaticWebAssetItems
            {
                BuildEngine = buildEngine.Object,
                Source = "MyPackage",
                Assets = new[] { CreateCandidate("wwwroot\\candidate.js", "MyPackage", "Discovered", "candidate.js", "All", "All") },
                Patterns = new[] { CreatePatternCandidate("MyPackage\\wwwroot", "base", Directory.GetCurrentDirectory(), "wwwroot\\**", "MyPackage") },
                AssetKind = "Build",
                ProjectMode = "Default"
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true);
            task.DiscoveryPatterns.Should().HaveCount(1);
        }

        [Fact]
        public void FiltersPatternsFromReferencedProjects()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new ComputeReferenceStaticWebAssetItems
            {
                BuildEngine = buildEngine.Object,
                Source = "MyPackage",
                Assets = new[] { CreateCandidate("wwwroot\\candidate.js", "MyPackage", "Discovered", "candidate.js", "All", "All") },
                Patterns = new[] { CreatePatternCandidate("Other\\wwwroot", "base", Directory.GetCurrentDirectory(), "wwwroot\\**", "Other") },
                AssetKind = "Build",
                ProjectMode = "Default"
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true);
            task.DiscoveryPatterns.Should().HaveCount(0);
        }

        [Fact]
        public void PrefersSpecificKindAssetsOverAllKindAssets()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new ComputeReferenceStaticWebAssetItems
            {
                BuildEngine = buildEngine.Object,
                Source = "MyPackage",
                Assets = new[]
                {
                    CreateCandidate("wwwroot\\candidate.js", "MyPackage", "Discovered", "candidate.js", "All", "All"),
                    CreateCandidate("wwwroot\\candidate.other.js", "MyPackage", "Discovered", "candidate.js", "Build", "All")
                },
                Patterns = new ITaskItem[] { },
                AssetKind = "Build",
                ProjectMode = "Default"
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true);
            task.StaticWebAssets.Should().HaveCount(1);
            task.StaticWebAssets[0].ItemSpec.Should().Be(Path.GetFullPath(Path.Combine("wwwroot", "candidate.other.js")));
        }

        [Fact]
        public void AllAssetGetsIgnoredWhenBuildAndPublishAssetsAreDefined()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new ComputeReferenceStaticWebAssetItems
            {
                BuildEngine = buildEngine.Object,
                Source = "MyPackage",
                Assets = new[]
                {
                    CreateCandidate("wwwroot\\candidate.js", "MyPackage", "Discovered", "candidate.js", "All", "All"),
                    CreateCandidate("wwwroot\\candidate.other.js", "MyPackage", "Discovered", "candidate.js", "Build", "All"),
                    CreateCandidate("wwwroot\\candidate.publish.js", "MyPackage", "Discovered", "candidate.js", "Publish", "All")
                },
                Patterns = new ITaskItem[] { },
                AssetKind = "Build",
                ProjectMode = "Default"
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true);
            task.StaticWebAssets.Should().HaveCount(1);
            task.StaticWebAssets[0].ItemSpec.Should().Be(Path.GetFullPath(Path.Combine("wwwroot", "candidate.other.js")));
        }

        [Theory]
        [InlineData("Build", "Publish")]
        [InlineData("Publish", "Build")]
        public void FiltersAssetsForOppositeKind(string assetKind, string manifestKind)
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new ComputeReferenceStaticWebAssetItems
            {
                BuildEngine = buildEngine.Object,
                Source = "MyPackage",
                Assets = new[] { CreateCandidate("wwwroot\\candidate.js", "MyPackage", "Discovered", "candidate.js", assetKind, "All") },
                Patterns = new ITaskItem[] { },
                AssetKind = manifestKind,
                ProjectMode = "Default"
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true);
            task.StaticWebAssets.Should().HaveCount(0);
        }

        [Fact]
        public void FiltersCurrentProjectOnlyAssetsInDefaultMode()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new ComputeReferenceStaticWebAssetItems
            {
                BuildEngine = buildEngine.Object,
                Source = "MyPackage",
                Assets = new[] { CreateCandidate("wwwroot\\candidate.js", "MyPackage", "Discovered", "candidate.js", "All", "CurrentProject") },
                Patterns = new ITaskItem[] { },
                AssetKind = "Default",
                ProjectMode = "Default"
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true);
            task.StaticWebAssets.Should().HaveCount(0);
        }

        [Fact]
        public void IncludesReferenceAssetsInDefaultMode()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new ComputeReferenceStaticWebAssetItems
            {
                BuildEngine = buildEngine.Object,
                Source = "MyPackage",
                Assets = new[] { CreateCandidate("wwwroot\\candidate.js", "MyPackage", "Discovered", "candidate.js", "All", "Reference") },
                Patterns = new ITaskItem[] { },
                AssetKind = "Default",
                ProjectMode = "Default"
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true);
            task.StaticWebAssets.Should().HaveCount(1);
        }

        [Fact]
        public void IncludesCurrentProjectAssetsInRootMode()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new ComputeReferenceStaticWebAssetItems
            {
                BuildEngine = buildEngine.Object,
                Source = "MyPackage",
                Assets = new[] { CreateCandidate("wwwroot\\candidate.js", "MyPackage", "Discovered", "candidate.js", "All", "CurrentProject") },
                Patterns = new ITaskItem[] { },
                AssetKind = "Default",
                ProjectMode = "Root"
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true);
            task.StaticWebAssets.Should().HaveCount(1);
        }

        [Fact]
        public void FiltersReferenceOnlyAssetsInRootMode()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new ComputeReferenceStaticWebAssetItems
            {
                BuildEngine = buildEngine.Object,
                Source = "MyPackage",
                Assets = new[] { CreateCandidate("wwwroot\\candidate.js", "MyPackage", "Discovered", "candidate.js", "All", "Reference") },
                Patterns = new ITaskItem[] { },
                AssetKind = "Default",
                ProjectMode = "Root"
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true);
            task.StaticWebAssets.Should().HaveCount(0);
        }

        [Fact]
        public void FiltersAssetsFromOtherProjects()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new ComputeReferenceStaticWebAssetItems
            {
                BuildEngine = buildEngine.Object,
                Source = "MyPackage",
                Assets = new[] { CreateCandidate("wwwroot\\candidate.js", "Other", "Project", "candidate.js", "All", "All") },
                Patterns = new ITaskItem[] { },
                AssetKind = "Build",
                ProjectMode = "Default"
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true);
            task.StaticWebAssets.Should().HaveCount(0);
        }

        [Fact]
        public void FiltersAssetsFromPackages()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new ComputeReferenceStaticWebAssetItems
            {
                BuildEngine = buildEngine.Object,
                Source = "MyPackage",
                Assets = new[] { CreateCandidate("wwwroot\\candidate.js", "Other", "Package", "candidate.js", "All", "All") },
                Patterns = new ITaskItem[] { },
                AssetKind = "Build",
                ProjectMode = "Default"
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true);
            task.StaticWebAssets.Should().HaveCount(0);
        }

        private ITaskItem CreateCandidate(
            string itemSpec,
            string sourceId,
            string sourceType,
            string relativePath,
            string assetKind,
            string assetMode)
        {
            var result = new StaticWebAsset()
            {
                Identity = Path.GetFullPath(itemSpec),
                SourceId = sourceId,
                SourceType = sourceType,
                ContentRoot = Directory.GetCurrentDirectory(),
                BasePath = "base",
                RelativePath = relativePath,
                AssetKind = assetKind,
                AssetMode = assetMode,
                AssetRole = "Primary",
                RelatedAsset = "",
                AssetTraitName = "",
                AssetTraitValue = "",
                CopyToOutputDirectory = "",
                CopyToPublishDirectory = "",
                OriginalItemSpec = itemSpec,
            };

            result.ApplyDefaults();
            result.Normalize();

            return result.ToTaskItem();
        }

        private ITaskItem CreatePatternCandidate(
            string name,
            string basePath,
            string contentRoot,
            string pattern,
            string source)
        {
            var result = new StaticWebAssetsDiscoveryPattern()
            {
                Name = name,
                BasePath = basePath,
                ContentRoot = contentRoot,
                Pattern = pattern,
                Source = source
            };

            return result.ToTaskItem();
        }
    }
}
