// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
using Moq;

namespace Microsoft.NET.Sdk.Razor.Tests
{
    public class GenerateStaticWebAssetsManifestTest
    {
        public GenerateStaticWebAssetsManifestTest()
        {
            Directory.CreateDirectory(Path.Combine(TestContext.Current.TestExecutionDirectory, nameof(GenerateStaticWebAssetsManifestTest)));
            TempFilePath = Path.Combine(TestContext.Current.TestExecutionDirectory, nameof(GenerateStaticWebAssetsManifestTest), Guid.NewGuid().ToString("N") + ".json");
        }

        public string TempFilePath { get; }

        [Fact]
        public void CanGenerateEmptyManifest()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            // GetTempFilePath automatically creates the file, which interferes with the test.
            File.Delete(TempFilePath);

            var task = new GenerateStaticWebAssetsManifest
            {
                BuildEngine = buildEngine.Object,
                Assets = Array.Empty<ITaskItem>(),
                ReferencedProjectsConfigurations = Array.Empty<ITaskItem>(),
                DiscoveryPatterns = Array.Empty<ITaskItem>(),
                BasePath = "/",
                Source = "MyProject",
                ManifestType = "Build",
                Mode = "Default",
                ManifestPath = TempFilePath,
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true);
            var manifest = StaticWebAssetsManifest.FromJsonString(File.ReadAllText(TempFilePath));
            manifest.Should().NotBeNull();
            manifest.Assets.Should().BeNullOrEmpty();
            manifest.DiscoveryPatterns.Should().BeNullOrEmpty();
            manifest.ReferencedProjectsConfiguration.Should().BeNullOrEmpty();
            manifest.Version.Should().Be(1);
            manifest.Hash.Should().NotBeNullOrWhiteSpace();
            manifest.Mode.Should().Be("Default");
            manifest.ManifestType.Should().Be("Build");
            manifest.BasePath.Should().Be("/");
            manifest.Source.Should().Be("MyProject");
        }

        [Fact]
        public void GeneratesManifestWithAssets()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            // GetTempFilePath automatically creates the file, which interferes with the test.
            File.Delete(TempFilePath);
            var asset = CreateAsset(Path.Combine("wwwroot", "candidate.js"), "MyProject", "Computed", "candidate.js", "All", "All");
            var task = new GenerateStaticWebAssetsManifest
            {
                BuildEngine = buildEngine.Object,
                Assets = new[]
                {
                    asset.ToTaskItem()
                },
                ReferencedProjectsConfigurations = Array.Empty<ITaskItem>(),
                DiscoveryPatterns = Array.Empty<ITaskItem>(),
                BasePath = "/",
                Source = "MyProject",
                ManifestType = "Build",
                Mode = "Default",
                ManifestPath = TempFilePath,
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true);
            var manifest = StaticWebAssetsManifest.FromJsonString(File.ReadAllText(TempFilePath));
            manifest.Should().NotBeNull();
            manifest.Assets.Should().HaveCount(1);
            var newAsset = manifest.Assets[0];
            newAsset.Should().Be(asset);
        }

        public static TheoryData<Action<StaticWebAsset>> GeneratesManifestFailsWhenInvalidAssetsAreProvidedData
        {
            get
            {
                var theoryData = new TheoryData<Action<StaticWebAsset>>();
                theoryData.Add(a => a.SourceId = "");
                theoryData.Add(a => a.SourceType = "");
                theoryData.Add(a => a.RelativePath = "");
                theoryData.Add(a => a.ContentRoot = "");
                theoryData.Add(a => a.OriginalItemSpec = "");
                theoryData.Add(a => a.AssetKind = "");
                theoryData.Add(a => a.AssetRole = "");
                theoryData.Add(a => a.AssetMode = "");
                theoryData.Add(a =>
                {
                    a.AssetRole = "Related";
                    a.RelatedAsset = "";
                });
                theoryData.Add(a =>
                {
                    a.AssetRole = "Alternative";
                    a.RelatedAsset = "";
                });

                return theoryData;
            }
        }

        [Theory]
        [MemberData(nameof(GeneratesManifestFailsWhenInvalidAssetsAreProvidedData))]
        public void GeneratesManifestFailsWhenInvalidAssetsAreProvided(Action<StaticWebAsset> change)
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            // GetTempFilePath automatically creates the file, which interferes with the test.
            File.Delete(TempFilePath);
            var asset = CreateAsset(Path.Combine("wwwroot", "candidate.js"), "MyProject", "Computed", "candidate.js", "All", "All");
            change(asset);
            var task = new GenerateStaticWebAssetsManifest
            {
                BuildEngine = buildEngine.Object,
                Assets = new[]
                {
                    asset.ToTaskItem()
                },
                ReferencedProjectsConfigurations = Array.Empty<ITaskItem>(),
                DiscoveryPatterns = Array.Empty<ITaskItem>(),
                BasePath = "/",
                Source = "MyProject",
                ManifestType = "Build",
                Mode = "Default",
                ManifestPath = TempFilePath,
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(false);
        }

        public static TheoryData<StaticWebAsset, StaticWebAsset> GeneratesManifestFailsWhenTwoAssetsEndUpOnTheSamePathData
        {
            get
            {
                var data = new TheoryData<StaticWebAsset, StaticWebAsset>();
                // Duplicate assets
                data.Add(
                    CreateAsset(Path.Combine("wwwroot", "candidate.js"), "MyProject", "Computed", "candidate.js", "All", "All"),
                    CreateAsset(Path.Combine("wwwroot", "candidate.js"), "MyProject", "Computed", "candidate.js", "All", "All"));

                // Conflicting Build asssets from different projects
                data.Add(
                    CreateAsset(Path.Combine("wwwroot", "candidate.js"), "Package", "Package", "candidate.js", "All", "Build"),
                    CreateAsset(Path.Combine("wwwroot", "candidate.js"), "OtherProject", "Project", "candidate.js", "All", "Build"));

                // Conflicting Publish asssets from different projects
                data.Add(
                    CreateAsset(Path.Combine("wwwroot", "candidate.js"), "Package", "Package", "candidate.js", "All", "Publish"),
                    CreateAsset(Path.Combine("wwwroot", "candidate.js"), "OtherProject", "Project", "candidate.js", "All", "Publish"));

                // Conflicting All asssets from different projects
                data.Add(
                    CreateAsset(Path.Combine("wwwroot", "candidate.js"), "Package", "Package", "candidate.js", "All", "All"),
                    CreateAsset(Path.Combine("wwwroot", "candidate.js"), "OtherProject", "Project", "candidate.js", "All", "All"));

                // Assets with compatible kinds but from different projects
                data.Add(
                    CreateAsset(Path.Combine("wwwroot", "candidate.js"), "MyProject", "Computed", "candidate.js", "All", "Build"),
                    CreateAsset(Path.Combine("wwwroot", "candidate.js"), "Other", "Project", "candidate.js", "All", "Publish"));

                return data;
            }
        }

        [Theory]
        [MemberData(nameof(GeneratesManifestFailsWhenTwoAssetsEndUpOnTheSamePathData))]
        public void GeneratesManifestFailsWhenTwoAssetsEndUpOnTheSamePath(StaticWebAsset first, StaticWebAsset second)
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            // GetTempFilePath automatically creates the file, which interferes with the test.
            File.Delete(TempFilePath);
            var task = new GenerateStaticWebAssetsManifest
            {
                BuildEngine = buildEngine.Object,
                Assets = new[]
                {
                    first.ToTaskItem(),
                    second.ToTaskItem()
                },
                ReferencedProjectsConfigurations = Array.Empty<ITaskItem>(),
                DiscoveryPatterns = Array.Empty<ITaskItem>(),
                BasePath = "/",
                Source = "MyProject",
                ManifestType = "Build",
                Mode = "Default",
                ManifestPath = TempFilePath,
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(false);
        }


        [Fact]
        public void GeneratesManifestWithReferencedProjectConfigurations()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            // GetTempFilePath automatically creates the file, which interferes with the test.
            File.Delete(TempFilePath);
            var projectReference = CreateProjectReferenceConfiguration(2, "Other");
            var task = new GenerateStaticWebAssetsManifest
            {
                BuildEngine = buildEngine.Object,
                Assets = Array.Empty<ITaskItem>(),
                ReferencedProjectsConfigurations = new[] { projectReference.ToTaskItem() },
                DiscoveryPatterns = Array.Empty<ITaskItem>(),
                BasePath = "/",
                Source = "MyProject",
                ManifestType = "Build",
                Mode = "Default",
                ManifestPath = TempFilePath,
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true);
            var manifest = StaticWebAssetsManifest.FromJsonString(File.ReadAllText(TempFilePath));
            manifest.Should().NotBeNull();
            manifest.ReferencedProjectsConfiguration.Should().HaveCount(1);
            var newProjectConfig = manifest.ReferencedProjectsConfiguration[0];
            newProjectConfig.Should().Be(projectReference);
        }

        [Fact]
        public void GeneratesManifestWithDiscoveryPatterns()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            // GetTempFilePath automatically creates the file, which interferes with the test.
            File.Delete(TempFilePath);

            var candidatePattern = CreatePatternCandidate(Path.Combine("MyProject", "wwwroot"), "base", "wwwroot/**", "MyProject");
            var task = new GenerateStaticWebAssetsManifest
            {
                BuildEngine = buildEngine.Object,
                Assets = Array.Empty<ITaskItem>(),
                ReferencedProjectsConfigurations = Array.Empty<ITaskItem>(),
                DiscoveryPatterns = new[] { candidatePattern.ToTaskItem() },
                BasePath = "/",
                Source = "MyProject",
                ManifestType = "Build",
                Mode = "Default",
                ManifestPath = TempFilePath,
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true);
            var manifest = StaticWebAssetsManifest.FromJsonString(File.ReadAllText(TempFilePath));
            manifest.Should().NotBeNull();
            manifest.DiscoveryPatterns.Should().HaveCount(1);
            var newProjectConfig = manifest.DiscoveryPatterns[0];
            newProjectConfig.Should().Be(candidatePattern);
        }

        private StaticWebAssetsManifest.ReferencedProjectConfiguration CreateProjectReferenceConfiguration(
            int version,
            string source,
            string publishTargets = "ComputeReferencedStaticWebAssetsPublishManifest;GetCurrentProjectPublishStaticWebAssetItems",
            string additionalPublishProperties = ";",
            string additionalPublishPropertiesToRemove = ";WebPublishProfileFile",
            string buildTargets = "GetCurrentProjectBuildStaticWebAssetItems",
            string additionalBuildProperties = ";",
            string additionalBuildPropertiesToRemove = ";WebPublishProfileFile")
        {
            var result = new StaticWebAssetsManifest.ReferencedProjectConfiguration();
            result.Identity = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), $"{source}.csproj"));
            result.Version = version;
            result.Source = source;
            result.GetPublishAssetsTargets = publishTargets;
            result.AdditionalPublishProperties = additionalPublishProperties;
            result.AdditionalPublishPropertiesToRemove = additionalPublishPropertiesToRemove;
            result.GetBuildAssetsTargets = buildTargets;
            result.AdditionalBuildProperties = additionalBuildProperties;
            result.AdditionalBuildPropertiesToRemove = additionalBuildPropertiesToRemove;

            return result;
        }

        private static StaticWebAsset CreateAsset(
            string itemSpec,
            string sourceId,
            string sourceType,
            string relativePath,
            string assetKind,
            string assetMode,
            string basePath = "base",
            string assetRole = "Primary",
            string relatedAsset = "",
            string assetTraitName = "",
            string assetTraitValue = "",
            string copyToOutputDirectory = "Never",
            string copytToPublishDirectory = "PreserveNewest")
        {
            var result = new StaticWebAsset()
            {
                Identity = Path.GetFullPath(itemSpec),
                SourceId = sourceId,
                SourceType = sourceType,
                ContentRoot = Directory.GetCurrentDirectory(),
                BasePath = basePath,
                RelativePath = relativePath,
                AssetKind = assetKind,
                AssetMode = assetMode,
                AssetRole = assetRole,
                AssetMergeBehavior = StaticWebAsset.MergeBehaviors.PreferTarget,
                AssetMergeSource = "",
                RelatedAsset = relatedAsset,
                AssetTraitName = assetTraitName,
                AssetTraitValue = assetTraitValue,
                CopyToOutputDirectory = copyToOutputDirectory,
                CopyToPublishDirectory = copytToPublishDirectory,
                OriginalItemSpec = itemSpec,
            };

            result.ApplyDefaults();
            result.Normalize();

            return result;
        }

        private StaticWebAssetsDiscoveryPattern CreatePatternCandidate(
            string name,
            string basePath,
            string pattern,
            string source)
        {
            var result = new StaticWebAssetsDiscoveryPattern()
            {
                Name = name,
                BasePath = basePath,
                ContentRoot = Directory.GetCurrentDirectory(),
                Pattern = pattern,
                Source = source
            };

            return result;
        }
    }
}
