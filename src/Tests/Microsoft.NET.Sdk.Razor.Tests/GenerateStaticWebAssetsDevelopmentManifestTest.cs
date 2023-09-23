// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
using Moq;
using static Microsoft.AspNetCore.StaticWebAssets.Tasks.GenerateStaticWebAssetsDevelopmentManifest;

namespace Microsoft.NET.Sdk.Razor.Tests
{
    public class GenerateStaticWebAssetsDevelopmentManifestTest
    {
        [Fact]
        public void SkipsManifestGenerationWhen_ThereAreNoAssetsNorDiscoveryPatterns()
        {
            // Arrange
            var messages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogMessageEvent(It.IsAny<BuildMessageEventArgs>()))
                .Callback<BuildMessageEventArgs>(args => messages.Add(args.Message));

            var task = new GenerateStaticWebAssetsDevelopmentManifest()
            {
                BuildEngine = buildEngine.Object,
                Assets = Array.Empty<ITaskItem>(),
                DiscoveryPatterns = Array.Empty<ITaskItem>()
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().BeTrue();
            messages.Should().HaveCount(1);
        }

        [Fact]
        public void ComputeDevelopmentManifest_IncludesBuildAssets()
        {
            // Arrange
            var messages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogMessageEvent(It.IsAny<BuildMessageEventArgs>()))
                .Callback<BuildMessageEventArgs>(args => messages.Add(args.Message));

            var expectedManifest = CreateExpectedManifest(
                CreateIntermediateNode(
                    ("index.html", CreateMatchNode(0, "index.html"))),
            Environment.CurrentDirectory);

            var task = new GenerateStaticWebAssetsDevelopmentManifest()
            {
                BuildEngine = buildEngine.Object,
            };

            var assets = new[] { CreateAsset("index.html", "index.html", assetKind: StaticWebAsset.AssetKinds.Build) };
            var patterns = Array.Empty<StaticWebAssetsDiscoveryPattern>();

            // Act
            var manifest = task.ComputeDevelopmentManifest(assets, patterns);

            // Assert
            manifest.Should().BeEquivalentTo(expectedManifest);
        }

        [Fact]
        public void ComputeDevelopmentManifest_IncludesAllAssets()
        {
            // Arrange
            var messages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogMessageEvent(It.IsAny<BuildMessageEventArgs>()))
                .Callback<BuildMessageEventArgs>(args => messages.Add(args.Message));

            var expectedManifest = CreateExpectedManifest(
                CreateIntermediateNode(
                    ("index.html", CreateMatchNode(0, "index.html"))),
            Environment.CurrentDirectory);

            var task = new GenerateStaticWebAssetsDevelopmentManifest()
            {
                BuildEngine = buildEngine.Object,
            };

            var assets = new[] { CreateAsset("index.html", "index.html", assetKind: StaticWebAsset.AssetKinds.All) };
            var patterns = Array.Empty<StaticWebAssetsDiscoveryPattern>();

            // Act
            var manifest = task.ComputeDevelopmentManifest(assets, patterns);

            // Assert
            manifest.Should().BeEquivalentTo(expectedManifest);
        }

        [Fact]
        public void ComputeDevelopmentManifest_ExcludesPublishAssets()
        {
            // Arrange
            var messages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogMessageEvent(It.IsAny<BuildMessageEventArgs>()))
                .Callback<BuildMessageEventArgs>(args => messages.Add(args.Message));

            var expectedManifest = CreateExpectedManifest(
                CreateIntermediateNode());

            var task = new GenerateStaticWebAssetsDevelopmentManifest()
            {
                BuildEngine = buildEngine.Object,
            };

            var assets = new[] { CreateAsset("index.html", "index.html", assetKind: StaticWebAsset.AssetKinds.Publish) };
            var patterns = Array.Empty<StaticWebAssetsDiscoveryPattern>();

            // Act
            var manifest = task.ComputeDevelopmentManifest(assets, patterns);

            // Assert
            manifest.Should().BeEquivalentTo(expectedManifest);
        }

        [Fact]
        public void ComputeDevelopmentManifest_ExcludesReferenceAssets()
        {
            // Arrange
            var messages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogMessageEvent(It.IsAny<BuildMessageEventArgs>()))
                .Callback<BuildMessageEventArgs>(args => messages.Add(args.Message));

            var expectedManifest = CreateExpectedManifest(
                CreateIntermediateNode());

            var task = new GenerateStaticWebAssetsDevelopmentManifest()
            {
                BuildEngine = buildEngine.Object,
                Source = "CurrentProjectId"
            };

            var assets = new[] { CreateAsset("index.html", "index.html", assetMode: StaticWebAsset.AssetModes.Reference) };
            var patterns = Array.Empty<StaticWebAssetsDiscoveryPattern>();

            // Act
            var manifest = task.ComputeDevelopmentManifest(assets, patterns);

            // Assert
            manifest.Should().BeEquivalentTo(expectedManifest);
        }

        [Fact]
        public void ComputeDevelopmentManifest_PrefersBuildAssetsOverAllAssets()
        {
            // Arrange
            var messages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogMessageEvent(It.IsAny<BuildMessageEventArgs>()))
                .Callback<BuildMessageEventArgs>(args => messages.Add(args.Message));

            var expectedManifest = CreateExpectedManifest(
                CreateIntermediateNode(
                    ("index.html", CreateMatchNode(0, "index.build.html"))),
                Environment.CurrentDirectory);

            var task = new GenerateStaticWebAssetsDevelopmentManifest()
            {
                BuildEngine = buildEngine.Object,
                Source = "CurrentProjectId"
            };

            var assets = new[] {
                CreateAsset("index.build.html", "index.html", assetKind: StaticWebAsset.AssetKinds.Build),
                CreateAsset("index.html", "index.html", assetKind: StaticWebAsset.AssetKinds.All)
            };
            var patterns = Array.Empty<StaticWebAssetsDiscoveryPattern>();

            // Act
            var manifest = task.ComputeDevelopmentManifest(assets, patterns);

            // Assert
            manifest.Should().BeEquivalentTo(expectedManifest);
        }

        [Fact]
        public void ComputeDevelopmentManifest_UsesIdentityWhenContentRootStartsByIdentity()
        {
            // Arrange
            var messages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogMessageEvent(It.IsAny<BuildMessageEventArgs>()))
                .Callback<BuildMessageEventArgs>(args => messages.Add(args.Message));

            var expectedManifest = CreateExpectedManifest(
                CreateIntermediateNode(
                    ("index.html", CreateMatchNode(0, StaticWebAsset.Normalize(Path.Combine("some", "subfolder", "index.build.html"))))),
                Environment.CurrentDirectory);

            var task = new GenerateStaticWebAssetsDevelopmentManifest()
            {
                BuildEngine = buildEngine.Object,
                Source = "CurrentProjectId"
            };

            var assets = new[] {
                CreateAsset(Path.Combine("some", "subfolder", "index.build.html"), "index.html"),
            };
            var patterns = Array.Empty<StaticWebAssetsDiscoveryPattern>();

            // Act
            var manifest = task.ComputeDevelopmentManifest(assets, patterns);

            // Assert
            manifest.Should().BeEquivalentTo(expectedManifest);
        }

        [Fact]
        public void ComputeDevelopmentManifest_UsesRelativePathContentRootDoesNotStartByIdentity()
        {
            // Arrange
            var messages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogMessageEvent(It.IsAny<BuildMessageEventArgs>()))
                .Callback<BuildMessageEventArgs>(args => messages.Add(args.Message));

            var expectedManifest = CreateExpectedManifest(
                CreateIntermediateNode(
                    ("index.html", CreateMatchNode(0, "index.html"))),
                Path.GetFullPath(Path.Combine("bin", "debug", "wwwroot")));

            var task = new GenerateStaticWebAssetsDevelopmentManifest()
            {
                BuildEngine = buildEngine.Object,
                Source = "CurrentProjectId"
            };

            var assets = new[] {
                CreateAsset(Path.Combine("some", "subfolder", "index.build.html"), "index.html", contentRoot: Path.Combine("bin", "debug", "wwwroot")),
            };
            var patterns = Array.Empty<StaticWebAssetsDiscoveryPattern>();

            // Act
            var manifest = task.ComputeDevelopmentManifest(assets, patterns);

            // Assert
            manifest.Should().BeEquivalentTo(expectedManifest);
        }

        [Fact]
        public void ComputeDevelopmentManifest_MapsPatternsFromCurrentProject()
        {
            // Arrange
            var messages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogMessageEvent(It.IsAny<BuildMessageEventArgs>()))
                .Callback<BuildMessageEventArgs>(args => messages.Add(args.Message));

            var expectedManifest = CreateExpectedManifest(
                CreateIntermediateNode()
                    .AddPatterns((0, "**", 0)),
                Path.GetFullPath("wwwroot"));

            var task = new GenerateStaticWebAssetsDevelopmentManifest()
            {
                BuildEngine = buildEngine.Object,
                Source = "CurrentProjectId"
            };

            var assets = Array.Empty<StaticWebAsset>();
            var patterns = new[] { CreatePattern() };

            // Act
            var manifest = task.ComputeDevelopmentManifest(assets, patterns);

            // Assert
            manifest.Should().BeEquivalentTo(expectedManifest);
        }

        [Fact]
        public void ComputeDevelopmentManifest_MapsPatternsFromOtherProjects()
        {
            // Arrange
            var messages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogMessageEvent(It.IsAny<BuildMessageEventArgs>()))
                .Callback<BuildMessageEventArgs>(args => messages.Add(args.Message));

            var expectedManifest = CreateExpectedManifest(
                CreateIntermediateNode(
                    ("_other", CreateIntermediateNode(
                        ("_project", CreateIntermediateNode().AddPatterns((0, "**", 2)))))),
                Path.GetFullPath("wwwroot"));

            var task = new GenerateStaticWebAssetsDevelopmentManifest()
            {
                BuildEngine = buildEngine.Object,
                Source = "CurrentProjectId"
            };

            var assets = Array.Empty<StaticWebAsset>();
            var patterns = new[] { CreatePattern(basePath: "_other/_project", source: "OtherProject") };

            // Act
            var manifest = task.ComputeDevelopmentManifest(assets, patterns);

            // Assert
            manifest.Should().BeEquivalentTo(expectedManifest);
        }

        [Fact]
        public void ComputeDevelopmentManifest_CanMapMultiplePatternsOnSameNode()
        {
            // Arrange
            var messages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogMessageEvent(It.IsAny<BuildMessageEventArgs>()))
                .Callback<BuildMessageEventArgs>(args => messages.Add(args.Message));

            var expectedManifest = CreateExpectedManifest(
                CreateIntermediateNode(
                    ("_other", CreateIntermediateNode(
                        ("_project", CreateIntermediateNode().AddPatterns(
                            (0, "*.js", 2),
                            (0, "*.css", 2)))))),
                Path.GetFullPath("wwwroot"));

            var task = new GenerateStaticWebAssetsDevelopmentManifest()
            {
                BuildEngine = buildEngine.Object,
                Source = "CurrentProjectId"
            };

            var assets = Array.Empty<StaticWebAsset>();
            var patterns = new[]
            {
                CreatePattern(basePath: "_other/_project", source: "OtherProject", pattern: "*.js"),
                CreatePattern(basePath: "_other/_project", source: "OtherProject", pattern: "*.css")
            };

            // Act
            var manifest = task.ComputeDevelopmentManifest(assets, patterns);

            // Assert
            manifest.Should().BeEquivalentTo(expectedManifest);
        }

        [Fact]
        public void ComputeDevelopmentManifest_CanMapMultiplePatternsOnSameNodeWithDifferentContentRoots()
        {
            // Arrange
            var messages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogMessageEvent(It.IsAny<BuildMessageEventArgs>()))
                .Callback<BuildMessageEventArgs>(args => messages.Add(args.Message));

            var expectedManifest = CreateExpectedManifest(
                CreateIntermediateNode(
                    ("_other", CreateIntermediateNode(
                        ("_project", CreateIntermediateNode().AddPatterns(
                            (0, "*.js", 2),
                            (1, "*.css", 2)))))),
                Path.GetFullPath("wwwroot"),
                Path.GetFullPath("styles"));

            var task = new GenerateStaticWebAssetsDevelopmentManifest()
            {
                BuildEngine = buildEngine.Object,
                Source = "CurrentProjectId"
            };

            var assets = Array.Empty<StaticWebAsset>();
            var patterns = new[]
            {
                CreatePattern(basePath: "_other/_project", source: "OtherProject", pattern: "*.js"),
                CreatePattern(basePath: "_other/_project", source: "OtherProject", pattern: "*.css", contentRoot: Path.GetFullPath("styles"))
            };

            // Act
            var manifest = task.ComputeDevelopmentManifest(assets, patterns);

            // Assert
            manifest.Should().BeEquivalentTo(expectedManifest);
        }

        [Fact]
        public void ComputeDevelopmentManifest_MultipleAssetsSameContentRoot()
        {
            // Arrange
            var messages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogMessageEvent(It.IsAny<BuildMessageEventArgs>()))
                .Callback<BuildMessageEventArgs>(args => messages.Add(args.Message));

            var expectedManifest = CreateExpectedManifest(
                CreateIntermediateNode(
                    ("css", CreateIntermediateNode(("site.css", CreateMatchNode(0, "css/site.css")))),
                    ("js", CreateIntermediateNode(("index.js", CreateMatchNode(0, "js/index.js"))))),
                Environment.CurrentDirectory);

            var task = new GenerateStaticWebAssetsDevelopmentManifest()
            {
                BuildEngine = buildEngine.Object,
                Source = "CurrentProjectId"
            };

            var assets = new[]
            {
                CreateAsset(Path.Combine(Environment.CurrentDirectory, "css", "site.css"), "css/site.css"),
                CreateAsset(Path.Combine(Environment.CurrentDirectory, "js", "index.js"), "js/index.js")
            };

            var patterns = Array.Empty<StaticWebAssetsDiscoveryPattern>();

            // Act
            var manifest = task.ComputeDevelopmentManifest(assets, patterns);

            // Assert
            manifest.Should().BeEquivalentTo(expectedManifest);
        }

        [Fact]
        public void ComputeDevelopmentManifest_DifferentCasingEndUpInDifferentNodes()
        {
            // Arrange
            var messages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogMessageEvent(It.IsAny<BuildMessageEventArgs>()))
                .Callback<BuildMessageEventArgs>(args => messages.Add(args.Message));

            var expectedManifest = CreateExpectedManifest(
                CreateIntermediateNode(
                    ("css", CreateIntermediateNode(("site.css", CreateMatchNode(0, "css/site.css")))),
                    ("CSS", CreateIntermediateNode(("site.css", CreateMatchNode(0, "CSS/site.css"))))),
                Environment.CurrentDirectory);

            var task = new GenerateStaticWebAssetsDevelopmentManifest()
            {
                BuildEngine = buildEngine.Object,
                Source = "CurrentProjectId"
            };

            var assets = new[]
            {
                CreateAsset(Path.Combine(Environment.CurrentDirectory, "css", "site.css"), "css/site.css"),
                CreateAsset(Path.Combine(Environment.CurrentDirectory, "CSS", "site.css"), "CSS/site.css"),
            };

            var patterns = Array.Empty<StaticWebAssetsDiscoveryPattern>();

            // Act
            var manifest = task.ComputeDevelopmentManifest(assets, patterns);

            // Assert
            manifest.Should().BeEquivalentTo(expectedManifest);
        }

        [Fact]
        public void ComputeDevelopmentManifest_UsesBasePathForAssetsFromDifferentProjects()
        {
            // Arrange
            var messages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogMessageEvent(It.IsAny<BuildMessageEventArgs>()))
                .Callback<BuildMessageEventArgs>(args => messages.Add(args.Message));

            var expectedManifest = CreateExpectedManifest(
                CreateIntermediateNode(
                    ("css", CreateIntermediateNode(("site.css", CreateMatchNode(0, "css/site.css")))),
                    ("_content", CreateIntermediateNode(
                        ("OtherProject", CreateIntermediateNode(
                            ("CSS", CreateIntermediateNode(("site.css", CreateMatchNode(1, "CSS/site.css"))))))))),
                Environment.CurrentDirectory,
                Path.GetFullPath("otherProject"));

            var task = new GenerateStaticWebAssetsDevelopmentManifest()
            {
                BuildEngine = buildEngine.Object,
                Source = "CurrentProjectId"
            };

            var assets = new[]
            {
                CreateAsset(Path.Combine(Environment.CurrentDirectory, "css", "site.css"), "css/site.css"),
                CreateAsset(
                    Path.Combine(Environment.CurrentDirectory, "CSS", "site.css"),
                    "CSS/site.css",
                    basePath: "_content/OtherProject",
                    sourceType: "Project",
                    contentRoot: Path.GetFullPath("otherProject")),
            };

            var patterns = Array.Empty<StaticWebAssetsDiscoveryPattern>();

            // Act
            var manifest = task.ComputeDevelopmentManifest(assets, patterns);

            // Assert
            manifest.Should().BeEquivalentTo(expectedManifest);
        }

        private static StaticWebAssetsDiscoveryPattern CreatePattern(
            string name = null,
            string contentRoot = null,
            string pattern = null,
            string basePath = null,
            string source = null) =>
            new()
            {
                Name = name ?? "CurrentProjectId\\wwwroot",
                Pattern = pattern ?? "**",
                BasePath = basePath ?? "_content/CurrentProjectId",
                Source = source ?? "CurrentProjectId",
                ContentRoot = StaticWebAsset.NormalizeContentRootPath(contentRoot ?? Path.Combine(Environment.CurrentDirectory, "wwwroot"))
            };

        private static StaticWebAssetsDevelopmentManifest CreateExpectedManifest(StaticWebAssetNode root, params string[] contentRoots)
        {
            return new StaticWebAssetsDevelopmentManifest()
            {
                ContentRoots = contentRoots.Select(cr => StaticWebAsset.NormalizeContentRootPath(cr)).ToArray(),
                Root = root
            };
        }

        private static StaticWebAssetNode CreateIntermediateNode(params (string key, StaticWebAssetNode node)[] children) => new()
        {
            Children = children.Length == 0 ? null : children.ToDictionary(pair => pair.key, pair => pair.node)
        };

        private static StaticWebAssetNode CreateMatchNode(int index, string subpath) => new()
        {
            Asset = new StaticWebAssetMatch { ContentRootIndex = index, SubPath = subpath }
        };

        private StaticWebAsset CreateAsset(
            string identity,
            string relativePath,
            string assetKind = default,
            string assetMode = default,
            string sourceId = default,
            string sourceType = default,
            string basePath = default,
            string contentRoot = default)
        {
            return new StaticWebAsset()
            {
                Identity = Path.GetFullPath(identity),
                SourceId = sourceId ?? "CurrentProjectId",
                SourceType = sourceType ?? StaticWebAsset.SourceTypes.Discovered,
                BasePath = basePath ?? "_content/Base",
                RelativePath = relativePath,
                AssetKind = assetKind ?? StaticWebAsset.AssetKinds.All,
                AssetMode = assetMode ?? StaticWebAsset.AssetModes.All,
                AssetRole = StaticWebAsset.AssetRoles.Primary,
                ContentRoot = StaticWebAsset.NormalizeContentRootPath(contentRoot ?? Environment.CurrentDirectory),
                OriginalItemSpec = identity
            };
        }
    }

    internal static class StaticWebAssetNodeTestExtensions
    {
        public static StaticWebAssetNode AddPatterns(this StaticWebAssetNode node, params (int contentRoot, string pattern, int depth)[] patterns)
        {
            node.Patterns = patterns.Select(p => new StaticWebAssetPattern { ContentRootIndex = p.contentRoot, Pattern = p.pattern, Depth = p.depth }).ToArray();
            return node;
        }
    }
}
