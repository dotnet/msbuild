// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
using Moq;

namespace Microsoft.NET.Sdk.Razor.Tests
{
    public class ReadStaticWebAssetsManifestFileTest
    {
        public ReadStaticWebAssetsManifestFileTest()
        {
            Directory.CreateDirectory(Path.Combine(TestContext.Current.TestExecutionDirectory, nameof(ReadStaticWebAssetsManifestFileTest)));
            TempFilePath = Path.Combine(TestContext.Current.TestExecutionDirectory, nameof(ReadStaticWebAssetsManifestFileTest), Guid.NewGuid().ToString("N") + ".json");
        }

        public string TempFilePath { get; }

        [Fact]
        public void CanReadManifestWithoutProperties()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var emptyManifest = "{}";
            File.WriteAllText(TempFilePath, emptyManifest);

            var task = new ReadStaticWebAssetsManifestFile
            {
                BuildEngine = buildEngine.Object,
                ManifestPath = TempFilePath
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true);
            task.Assets.Should().BeEmpty();
            task.DiscoveryPatterns.Should().BeEmpty();
            task.ReferencedProjectsConfiguration.Should().BeEmpty();
        }

        [Fact]
        public void CanReadEmptyManifest()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var emptyManifest = @"{
  ""Version"": 1,
  ""Hash"": ""__hash__"",
  ""Source"": ""ComponentApp"",
  ""BasePath"": ""_content/ComponentApp"",
  ""Mode"": ""Default"",
  ""ManifestType"": ""Build"",
  ""ReferencedProjectsConfiguration"": [],
  ""DiscoveryPatterns"": [],
  ""Assets"": []
}";
            File.WriteAllText(TempFilePath, emptyManifest);

            var task = new ReadStaticWebAssetsManifestFile
            {
                BuildEngine = buildEngine.Object,
                ManifestPath = TempFilePath
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true);
            task.Assets.Should().BeEmpty();
            task.DiscoveryPatterns.Should().BeEmpty();
            task.ReferencedProjectsConfiguration.Should().BeEmpty();
        }

        [Fact]
        public void ConvertsAssetsToTaskItems()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var contentRoot = Path.GetFullPath(".");
            var encodedContentRoot = JsonEncodedText.Encode(contentRoot);
            var identity = Path.Combine(contentRoot, "ComponentApp.styles.css");
            var encodedIdentity = JsonEncodedText.Encode(identity);
            var manifest = $@"{{
  ""Version"": 1,
  ""Hash"": ""__hash__"",
  ""Source"": ""ComponentApp"",
  ""BasePath"": ""_content/ComponentApp"",
  ""Mode"": ""Default"",
  ""ManifestType"": ""Build"",
  ""ReferencedProjectsConfiguration"": [],
  ""DiscoveryPatterns"": [],
  ""Assets"": [
    {{
      ""Identity"": ""{encodedIdentity}"",
      ""SourceId"": ""ComponentApp"",
      ""SourceType"": ""Computed"",
      ""ContentRoot"": ""{encodedContentRoot}"",
      ""BasePath"": ""_content/ComponentApp"",
      ""RelativePath"": ""ComponentApp.styles.css"",
      ""AssetKind"": ""All"",
      ""AssetMode"": ""CurrentProject"",
      ""AssetRole"": ""Primary"",
      ""RelatedAsset"": """",
      ""AssetTraitName"": ""ScopedCss"",
      ""AssetTraitValue"": ""ApplicationBundle"",
      ""CopyToOutputDirectory"": ""Never"",
      ""CopyToPublishDirectory"": ""PreserveNewest"",
      ""OriginalItemSpec"": ""{encodedIdentity}""
    }}
]
}}";
            File.WriteAllText(TempFilePath, manifest);

            var task = new ReadStaticWebAssetsManifestFile
            {
                BuildEngine = buildEngine.Object,
                ManifestPath = TempFilePath
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true);
            task.ReferencedProjectsConfiguration.Should().BeEmpty();
            task.DiscoveryPatterns.Should().BeEmpty();
            task.Assets.Length.Should().Be(1);
            var asset = task.Assets[0];
            asset.GetMetadata(nameof(StaticWebAsset.Identity)).Should().BeEquivalentTo($"{identity}");
            asset.GetMetadata(nameof(StaticWebAsset.SourceId)).Should().BeEquivalentTo("ComponentApp");
            asset.GetMetadata(nameof(StaticWebAsset.SourceType)).Should().BeEquivalentTo("Computed");
            asset.GetMetadata(nameof(StaticWebAsset.ContentRoot)).Should().BeEquivalentTo($"{contentRoot}");
            asset.GetMetadata(nameof(StaticWebAsset.BasePath)).Should().BeEquivalentTo("_content/ComponentApp");
            asset.GetMetadata(nameof(StaticWebAsset.RelativePath)).Should().BeEquivalentTo("ComponentApp.styles.css");
            asset.GetMetadata(nameof(StaticWebAsset.AssetKind)).Should().BeEquivalentTo("All");
            asset.GetMetadata(nameof(StaticWebAsset.AssetMode)).Should().BeEquivalentTo("CurrentProject");
            asset.GetMetadata(nameof(StaticWebAsset.AssetRole)).Should().BeEquivalentTo("Primary");
            asset.GetMetadata(nameof(StaticWebAsset.RelatedAsset)).Should().BeEquivalentTo("");
            asset.GetMetadata(nameof(StaticWebAsset.AssetTraitName)).Should().BeEquivalentTo("ScopedCss");
            asset.GetMetadata(nameof(StaticWebAsset.AssetTraitValue)).Should().BeEquivalentTo("ApplicationBundle");
            asset.GetMetadata(nameof(StaticWebAsset.CopyToOutputDirectory)).Should().BeEquivalentTo("Never");
            asset.GetMetadata(nameof(StaticWebAsset.CopyToPublishDirectory)).Should().BeEquivalentTo("PreserveNewest");
            asset.GetMetadata(nameof(StaticWebAsset.OriginalItemSpec)).Should().BeEquivalentTo($"{identity}");
        }

        [Fact]
        public void ConvertsReferencedProjectsConfigurationsToTaskItems()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var contentRoot = Path.GetFullPath(".");
            var identity = Path.Combine(contentRoot, "AnotherClassLib", "AnotherClassLib.csproj");
            var encodedIdentity = JsonEncodedText.Encode(identity);
            var manifest = $@"{{
  ""Version"": 1,
  ""Hash"": ""__hash__"",
  ""Source"": ""ComponentApp"",
  ""BasePath"": ""_content/ComponentApp"",
  ""Mode"": ""Default"",
  ""ManifestType"": ""Build"",
  ""ReferencedProjectsConfiguration"": [
    {{
      ""Identity"": ""{encodedIdentity}"",
      ""Version"": 2,
      ""Source"": ""AnotherClassLib"",
      ""GetPublishAssetsTargets"": ""ComputeReferencedStaticWebAssetsPublishManifest;GetCurrentProjectPublishStaticWebAssetItems"",
      ""AdditionalPublishProperties"": "";"",
      ""AdditionalPublishPropertiesToRemove"": "";WebPublishProfileFile"",
      ""GetBuildAssetsTargets"": ""GetCurrentProjectBuildStaticWebAssetItems"",
      ""AdditionalBuildProperties"": "";"",
      ""AdditionalBuildPropertiesToRemove"": "";WebPublishProfileFile""
    }}
],
  ""DiscoveryPatterns"": [],
  ""Assets"": []
}}";
            File.WriteAllText(TempFilePath, manifest);

            var task = new ReadStaticWebAssetsManifestFile
            {
                BuildEngine = buildEngine.Object,
                ManifestPath = TempFilePath
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true);
            task.ReferencedProjectsConfiguration.Length.Should().Be(1);
            task.Assets.Should().BeEmpty();
            task.DiscoveryPatterns.Should().BeEmpty();
            var projectConfiguration = task.ReferencedProjectsConfiguration[0];
            projectConfiguration.ItemSpec.Should().BeEquivalentTo(identity);
            projectConfiguration.GetMetadata(nameof(StaticWebAssetsManifest.ReferencedProjectConfiguration.Version)).Should().BeEquivalentTo("2");
            projectConfiguration.GetMetadata(nameof(StaticWebAssetsManifest.ReferencedProjectConfiguration.Source)).Should().BeEquivalentTo("AnotherClassLib");
            projectConfiguration.GetMetadata(nameof(StaticWebAssetsManifest.ReferencedProjectConfiguration.GetPublishAssetsTargets)).Should().BeEquivalentTo("ComputeReferencedStaticWebAssetsPublishManifest;GetCurrentProjectPublishStaticWebAssetItems");
            projectConfiguration.GetMetadata(nameof(StaticWebAssetsManifest.ReferencedProjectConfiguration.AdditionalPublishProperties)).Should().BeEquivalentTo(";");
            projectConfiguration.GetMetadata(nameof(StaticWebAssetsManifest.ReferencedProjectConfiguration.AdditionalPublishPropertiesToRemove)).Should().BeEquivalentTo(";WebPublishProfileFile");
            projectConfiguration.GetMetadata(nameof(StaticWebAssetsManifest.ReferencedProjectConfiguration.GetBuildAssetsTargets)).Should().BeEquivalentTo("GetCurrentProjectBuildStaticWebAssetItems");
            projectConfiguration.GetMetadata(nameof(StaticWebAssetsManifest.ReferencedProjectConfiguration.AdditionalBuildProperties)).Should().BeEquivalentTo(";");
            projectConfiguration.GetMetadata(nameof(StaticWebAssetsManifest.ReferencedProjectConfiguration.AdditionalBuildPropertiesToRemove)).Should().BeEquivalentTo(";WebPublishProfileFile");
        }

        [Fact]
        public void ConvertsDiscoveryPatternsToTaskItems()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var contentRoot = Path.Combine(Path.GetFullPath("."), "AnotherClassLib", "wwwroot");
            var encodedContentRoot = JsonEncodedText.Encode(contentRoot);
            var manifest = $@"{{
  ""Version"": 1,
  ""Hash"": ""__hash__"",
  ""Source"": ""ComponentApp"",
  ""BasePath"": ""_content/ComponentApp"",
  ""Mode"": ""Default"",
  ""ManifestType"": ""Build"",
  ""ReferencedProjectsConfiguration"": [ ],
  ""DiscoveryPatterns"": [
    {{
      ""Name"": ""AnotherClassLib\\wwwroot"",
      ""Source"": ""AnotherClassLib"",
      ""ContentRoot"": ""{encodedContentRoot}"",
      ""BasePath"": ""_content/AnotherClassLib"",
      ""Pattern"": ""**""
    }}
],
  ""Assets"": []
}}";
            File.WriteAllText(TempFilePath, manifest);

            var task = new ReadStaticWebAssetsManifestFile
            {
                BuildEngine = buildEngine.Object,
                ManifestPath = TempFilePath
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true);
            task.DiscoveryPatterns.Length.Should().Be(1);
            task.ReferencedProjectsConfiguration.Should().BeEmpty();
            task.Assets.Should().BeEmpty();
            var discoveryPattern = task.DiscoveryPatterns[0];

            discoveryPattern.ItemSpec.Should().BeEquivalentTo(Path.Combine("AnotherClassLib", "wwwroot"));
            discoveryPattern.GetMetadata(nameof(StaticWebAssetsDiscoveryPattern.Source)).Should().BeEquivalentTo("AnotherClassLib");
            discoveryPattern.GetMetadata(nameof(StaticWebAssetsDiscoveryPattern.ContentRoot)).Should().BeEquivalentTo($"{contentRoot}");
            discoveryPattern.GetMetadata(nameof(StaticWebAssetsDiscoveryPattern.BasePath)).Should().BeEquivalentTo("_content/AnotherClassLib");
            discoveryPattern.GetMetadata(nameof(StaticWebAssetsDiscoveryPattern.Pattern)).Should().BeEquivalentTo("**");
        }

        [Fact]
        public void ReturnsErrorwhenManifestDoesNotExist()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new ReadStaticWebAssetsManifestFile
            {
                BuildEngine = buildEngine.Object,
                ManifestPath = "nonexisting.staticwebassets.json"
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(false);
            errorMessages.Count.Should().Be(1);
            errorMessages[0].Should().Be("Manifest file at 'nonexisting.staticwebassets.json' not found.");
            task.Assets.Should().BeNull();
            task.DiscoveryPatterns.Should().BeNull();
            task.ReferencedProjectsConfiguration.Should().BeNull();
        }

        [Fact]
        public void ReturnsErrorwhenManifestIsMalformed()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var manifest = "{";
            File.WriteAllText(TempFilePath, manifest);
            var task = new ReadStaticWebAssetsManifestFile
            {
                BuildEngine = buildEngine.Object,
                ManifestPath = TempFilePath
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(false);
            errorMessages.Count.Should().Be(1);
            task.Assets.Should().BeNull();
            task.DiscoveryPatterns.Should().BeNull();
            task.ReferencedProjectsConfiguration.Should().BeNull();
        }
    }
}
