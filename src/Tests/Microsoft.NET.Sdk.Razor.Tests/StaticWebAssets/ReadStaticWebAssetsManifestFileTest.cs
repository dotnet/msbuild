// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security.Principal;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Razor.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.NET.TestFramework;
using Moq;
using Xunit;

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
            asset.GetMetadata(nameof(StaticWebAsset.Identity)).ShouldBeEquivalentTo($"{identity}");
            asset.GetMetadata(nameof(StaticWebAsset.SourceId)).ShouldBeEquivalentTo("ComponentApp");
            asset.GetMetadata(nameof(StaticWebAsset.SourceType)).ShouldBeEquivalentTo("Computed");
            asset.GetMetadata(nameof(StaticWebAsset.ContentRoot)).ShouldBeEquivalentTo($"{contentRoot}");
            asset.GetMetadata(nameof(StaticWebAsset.BasePath)).ShouldBeEquivalentTo("_content/ComponentApp");
            asset.GetMetadata(nameof(StaticWebAsset.RelativePath)).ShouldBeEquivalentTo("ComponentApp.styles.css");
            asset.GetMetadata(nameof(StaticWebAsset.AssetKind)).ShouldBeEquivalentTo("All");
            asset.GetMetadata(nameof(StaticWebAsset.AssetMode)).ShouldBeEquivalentTo("CurrentProject");
            asset.GetMetadata(nameof(StaticWebAsset.AssetRole)).ShouldBeEquivalentTo("Primary");
            asset.GetMetadata(nameof(StaticWebAsset.RelatedAsset)).ShouldBeEquivalentTo("");
            asset.GetMetadata(nameof(StaticWebAsset.AssetTraitName)).ShouldBeEquivalentTo("ScopedCss");
            asset.GetMetadata(nameof(StaticWebAsset.AssetTraitValue)).ShouldBeEquivalentTo("ApplicationBundle");
            asset.GetMetadata(nameof(StaticWebAsset.CopyToOutputDirectory)).ShouldBeEquivalentTo("Never");
            asset.GetMetadata(nameof(StaticWebAsset.CopyToPublishDirectory)).ShouldBeEquivalentTo("PreserveNewest");
            asset.GetMetadata(nameof(StaticWebAsset.OriginalItemSpec)).ShouldBeEquivalentTo($"{identity}");
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
            projectConfiguration.ItemSpec.ShouldBeEquivalentTo(identity);
            projectConfiguration.GetMetadata(nameof(StaticWebAssetsManifest.ReferencedProjectConfiguration.Version)).ShouldBeEquivalentTo(2);
            projectConfiguration.GetMetadata(nameof(StaticWebAssetsManifest.ReferencedProjectConfiguration.Source)).ShouldBeEquivalentTo("AnotherClassLib");
            projectConfiguration.GetMetadata(nameof(StaticWebAssetsManifest.ReferencedProjectConfiguration.GetPublishAssetsTargets)).ShouldBeEquivalentTo("ComputeReferencedStaticWebAssetsPublishManifest;GetCurrentProjectPublishStaticWebAssetItems");
            projectConfiguration.GetMetadata(nameof(StaticWebAssetsManifest.ReferencedProjectConfiguration.AdditionalPublishProperties)).ShouldBeEquivalentTo(";");
            projectConfiguration.GetMetadata(nameof(StaticWebAssetsManifest.ReferencedProjectConfiguration.AdditionalPublishPropertiesToRemove)).ShouldBeEquivalentTo(";WebPublishProfileFile");
            projectConfiguration.GetMetadata(nameof(StaticWebAssetsManifest.ReferencedProjectConfiguration.GetBuildAssetsTargets)).ShouldBeEquivalentTo("GetCurrentProjectBuildStaticWebAssetItems");
            projectConfiguration.GetMetadata(nameof(StaticWebAssetsManifest.ReferencedProjectConfiguration.AdditionalBuildProperties)).ShouldBeEquivalentTo(";");
            projectConfiguration.GetMetadata(nameof(StaticWebAssetsManifest.ReferencedProjectConfiguration.AdditionalBuildPropertiesToRemove)).ShouldBeEquivalentTo(";WebPublishProfileFile");
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
            
            discoveryPattern.ItemSpec.ShouldBeEquivalentTo(Path.Combine("AnotherClassLib", "wwwroot"));
            discoveryPattern.GetMetadata(nameof(StaticWebAssetsManifest.DiscoveryPattern.Source)).ShouldBeEquivalentTo("AnotherClassLib");
            discoveryPattern.GetMetadata(nameof(StaticWebAssetsManifest.DiscoveryPattern.ContentRoot)).ShouldBeEquivalentTo($"{contentRoot}");
            discoveryPattern.GetMetadata(nameof(StaticWebAssetsManifest.DiscoveryPattern.BasePath)).ShouldBeEquivalentTo("_content/AnotherClassLib");
            discoveryPattern.GetMetadata(nameof(StaticWebAssetsManifest.DiscoveryPattern.Pattern)).ShouldBeEquivalentTo("**");
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
