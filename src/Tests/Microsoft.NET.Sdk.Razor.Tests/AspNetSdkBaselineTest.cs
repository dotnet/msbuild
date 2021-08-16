// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Razor.Tasks;
using Microsoft.NET.TestFramework;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Sdk.Razor.Tests
{
    [Trait("AspNetCore", "BaselineTest")]
    public class AspNetSdkBaselineTest : AspNetSdkTest
    {
        private static readonly JsonSerializerOptions BaselineSerializationOptions = new() { WriteIndented = true };

        private string _baselinesFolder;

#if GENERATE_SWA_BASELINES
        public static bool GenerateBaselines = true;
#else
        public static bool GenerateBaselines = false;
#endif

        private bool _generateBaselines = GenerateBaselines;

        // This allows templatizing paths that don't have a deterministic name, for example the files we gzip or brotli as part
        // of Blazor compilations.  We only need to do this for the manifest since the tests for files don't use the original
        // path. Returning null avoids any transformation
        protected Func<StaticWebAsset, string, StaticWebAsset, string> PathTemplatizer { get; set; } = (asset, originalValue, related) => null;

        public AspNetSdkBaselineTest(ITestOutputHelper log) : base(log)
        {
            TestAssembly = Assembly.GetCallingAssembly();
            var testAssemblyMetadata = TestAssembly.GetCustomAttributes<AssemblyMetadataAttribute>();
            RuntimeVersion = testAssemblyMetadata.SingleOrDefault(a => a.Key == "NetCoreAppRuntimePackageVersion").Value;
            DefaultPackageVersion = testAssemblyMetadata.SingleOrDefault(a => a.Key == "DefaultTestBaselinePackageVersion").Value;
        }

        public AspNetSdkBaselineTest(ITestOutputHelper log, bool generateBaselines) : this(log)
        {
            _generateBaselines = generateBaselines;
        }

        public TestAsset ProjectDirectory { get; set; }

        public string RuntimeVersion { get; set; }

        public string DefaultPackageVersion { get; set; }

        public string BaselinesFolder =>
            _baselinesFolder ??= ComputeBaselineFolder();

        protected Assembly TestAssembly { get; }

        protected virtual string ComputeBaselineFolder() =>
            Path.Combine(TestContext.GetRepoRoot() ?? AppContext.BaseDirectory, "src", "Tests", "Microsoft.NET.Sdk.Razor.Tests", "StaticWebAssetsBaselines");

        protected virtual string EmbeddedResourcePrefix => string.Join('.', "Microsoft.NET.Sdk.Razor.Tests", "StaticWebAssetsBaselines");


        public StaticWebAssetsManifest LoadBuildManifest(string suffix = "", [CallerMemberName] string name = "")
        {
            if (_generateBaselines)
            {
                return default;
            }
            else
            {
                using var stream = GetManifestEmbeddedResource(suffix, name, "Build");
                var manifest = StaticWebAssetsManifest.FromStream(stream);
                return manifest;
            }
        }

        public StaticWebAssetsManifest LoadPublishManifest(string suffix = "", [CallerMemberName] string name = "")
        {
            if (_generateBaselines)
            {
                return default;
            }
            else
            {
                using var stream = GetManifestEmbeddedResource(suffix, name, "Publish");
                var manifest = StaticWebAssetsManifest.FromStream(stream);
                return manifest;
            }
        }

        private void ApplyTemplatizerToAssets(StaticWebAssetsManifest manifest)
        {
            var assets = manifest.Assets;
            var assetsById = manifest.Assets.ToDictionary(a => a.Identity);
            for (int i = 0; i < assets.Length; i++)
            {
                var asset = assets[i];
                var relatedAsset = string.IsNullOrEmpty(asset.RelatedAsset) || !assetsById.TryGetValue(asset.RelatedAsset, out var related) ?
                    null : related;
                asset.Identity = PathTemplatizer(asset, asset.Identity, null) ?? asset.Identity;
                asset.RelatedAsset = PathTemplatizer(asset, asset.RelatedAsset, relatedAsset) ?? asset.RelatedAsset;
                asset.OriginalItemSpec = PathTemplatizer(asset, asset.OriginalItemSpec, null) ?? asset.OriginalItemSpec;
            }
        }

        private void UpdateCustomPackageVersions(string restorePath, StaticWebAssetsManifest manifest)
        {
            foreach (var asset in manifest.Assets)
            {
                asset.Identity = UpdateAssetVersion(restorePath, asset.Identity);
                asset.ContentRoot = UpdateAssetVersion(restorePath, asset.ContentRoot);
                asset.ContentRoot = asset.ContentRoot.EndsWith(Path.DirectorySeparatorChar) ? asset.ContentRoot : asset.ContentRoot + Path.DirectorySeparatorChar;
                asset.OriginalItemSpec = UpdateAssetVersion(restorePath, asset.OriginalItemSpec);
                asset.RelatedAsset = UpdateAssetVersion(restorePath, asset.RelatedAsset);
            }

            string UpdateAssetVersion(string restorePath, string property)
            {
                if (property.Contains(restorePath))
                {
                    var segments = property.Substring(restorePath.Length).Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
                    ref var versionSegment = ref segments[1];
                    if (versionSegment != RuntimeVersion && versionSegment != DefaultPackageVersion)
                    {
                        versionSegment = "[[CustomPackageVersion]]";
                        property = Path.Combine(segments.Prepend(restorePath).ToArray());
                    }
                }

                return property;
            }
        }

        protected void AssertBuildAssets(
            StaticWebAssetsManifest manifest,
            string outputFolder,
            string intermediateOutputPath,
            string suffix = "",
            [CallerMemberName] string name = "")
        {
            var fileEnumerationOptions = new EnumerationOptions { RecurseSubdirectories = true };
            var wwwRootFolder = Path.Combine(outputFolder, "wwwroot");
            var wwwRootFiles = Directory.Exists(wwwRootFolder) ?
                Directory.GetFiles(wwwRootFolder, "*", fileEnumerationOptions) :
                Array.Empty<string>();

            var computedFiles = manifest.Assets
                .Where(a => a.SourceType is StaticWebAsset.SourceTypes.Computed &&
                            a.AssetKind is not StaticWebAsset.AssetKinds.Publish);

            // We keep track of assets that need to be copied to the output folder.
            // In addition to that, we copy assets that are defined somewhere different
            // from their content root folder when the content root does not match the output folder.
            // We do this to allow copying things like Publish assets to temporary locations during the
            // build process if they are later on going to be transformed.
            var copyToOutputDirectoryFiles = manifest.Assets
                .Where(a => a.ShouldCopyToOutputDirectory())
                .Select(a => Path.GetFullPath(Path.Combine(outputFolder, "wwwroot", a.RelativePath)))
                .Concat(manifest.Assets
                    .Where(a => !a.HasContentRoot(Path.Combine(outputFolder, "wwwroot")) && File.Exists(a.Identity) && !File.Exists(Path.Combine(a.ContentRoot, a.RelativePath)))
                    .Select(a => Path.GetFullPath(Path.Combine(a.ContentRoot, a.RelativePath))))
                .ToArray();

            if (!_generateBaselines)
            {
                var expected = LoadExpectedFilesBaseline(manifest.ManifestType, outputFolder, intermediateOutputPath, suffix, name)
                    .OrderBy(f => f, StringComparer.Ordinal);

                var existingFiles = wwwRootFiles.Concat(computedFiles.Select(a => PathTemplatizer(a, a.Identity, null) ?? a.Identity)).Concat(copyToOutputDirectoryFiles)
                    .Distinct()
                    .OrderBy(f => f, StringComparer.Ordinal)
                    .ToArray();

                existingFiles.ShouldBeEquivalentTo(expected);
            }
            else
            {
                var templatizedFiles = TemplatizeExpectedFiles(
                    wwwRootFiles
                        .Concat(computedFiles.Select(a => PathTemplatizer(a, a.Identity, null) ?? a.Identity))
                        .Concat(copyToOutputDirectoryFiles)
                        .Distinct()
                        .OrderBy(f => f, StringComparer.Ordinal)
                        .ToArray(),
                    TestContext.Current.NuGetCachePath,
                    outputFolder,
                    ProjectDirectory.TestRoot,
                    intermediateOutputPath)
                    .OrderBy(o => o, StringComparer.Ordinal);

                File.WriteAllText(
                    GetExpectedFilesPath(suffix, name, manifest.ManifestType),
                    JsonSerializer.Serialize(templatizedFiles, BaselineSerializationOptions));
            }
        }

        protected void AssertPublishAssets(
            StaticWebAssetsManifest manifest,
            string publishFolder,
            string intermediateOutputPath,
            string suffix = "",
            [CallerMemberName] string name = "")
        {
            var fileEnumerationOptions = new EnumerationOptions { RecurseSubdirectories = true };
            string wwwRootFolder = Path.Combine(publishFolder, "wwwroot");
            var wwwRootFiles = Directory.Exists(wwwRootFolder) ?
                Directory.GetFiles(wwwRootFolder, "*", fileEnumerationOptions) :
                Array.Empty<string>();

            // Computed publish assets must exist on disk (we do this check to quickly identify when something is not being
            // generated vs when its being copied to the wrong place)
            var computedFiles = manifest.Assets
                .Where(a => a.SourceType is StaticWebAsset.SourceTypes.Computed &&
                            a.AssetKind is not StaticWebAsset.AssetKinds.Build);

            // For assets that are copied to the publish folder, the path is always based on
            // the wwwroot folder, the relative path and the base path for project or package
            // assets.
            var copyToPublishDirectoryFiles = manifest.Assets
                .Where(a => !string.Equals(a.SourceId, manifest.Source, StringComparison.Ordinal) ||
                            !string.Equals(a.AssetMode, StaticWebAsset.AssetModes.Reference))
                .Select(a => Path.Combine(wwwRootFolder, a.ComputeTargetPath("", Path.DirectorySeparatorChar)));

            var existingFiles = wwwRootFiles.Concat(computedFiles.Select(f => PathTemplatizer(f, f.Identity, null) ?? f.Identity)).Concat(copyToPublishDirectoryFiles)
                .Distinct()
                .OrderBy(f => f, StringComparer.Ordinal)
                .ToArray();

            if (!_generateBaselines)
            {
                var expected = LoadExpectedFilesBaseline(manifest.ManifestType, publishFolder, intermediateOutputPath, suffix, name);
                existingFiles.ShouldBeEquivalentTo(expected);
            }
            else
            {
                var templatizedFiles = TemplatizeExpectedFiles(
                    wwwRootFiles
                        .Concat(computedFiles.Select(f => PathTemplatizer(f, f.Identity, null) ?? f.Identity))
                        .Concat(copyToPublishDirectoryFiles)
                        .Distinct()
                        .OrderBy(f => f, StringComparer.Ordinal)
                        .ToArray(),
                    TestContext.Current.NuGetCachePath,
                    publishFolder,
                    ProjectDirectory.TestRoot,
                    intermediateOutputPath);

                File.WriteAllText(
                    GetExpectedFilesPath(suffix, name, manifest.ManifestType),
                    JsonSerializer.Serialize(templatizedFiles, BaselineSerializationOptions));
            }
        }

        public string[] LoadExpectedFilesBaseline(
            string type,
            string buildOrPublishPath,
            string intermediateOutputPath,
            string suffix,
            string name)
        {
            if (!_generateBaselines)
            {
                using var filesBaselineStream = GetExpectedFilesEmbeddedResource(suffix, name, type);
                return ApplyPathsToTemplatedFilePaths(
                    JsonSerializer.Deserialize<string[]>(filesBaselineStream),
                    TestContext.Current.NuGetCachePath,
                    buildOrPublishPath,
                    ProjectDirectory.TestRoot,
                    intermediateOutputPath)
                    .ToArray();
            }
            else
            {

                return Array.Empty<string>();
            }
        }

        private IEnumerable<string> TemplatizeExpectedFiles(
            IEnumerable<string> files,
            string restorePath,
            string buildOrPublishFolder,
            string projectPath,
            string intermediateOutputPath)
        {
            foreach (var f in files)
            {
                var updated = f.Replace(restorePath, "${RestorePath}")
                    .Replace(RuntimeVersion, "${RuntimeVersion}")
                    .Replace(DefaultPackageVersion, "${PackageVersion}")
                    .Replace(buildOrPublishFolder, "${OutputPath}")
                    .Replace(projectPath, "${ProjectPath}")
                    .Replace(intermediateOutputPath, "${IntermediateOutputPath}")
                    .Replace(Path.DirectorySeparatorChar, '\\');
                yield return updated;
            }
        }

        private IEnumerable<string> ApplyPathsToTemplatedFilePaths(
            IEnumerable<string> files,
            string restorePath,
            string buildOrPublishFolder,
            string projectPath,
            string intermediateOutputPath) =>
                files.Select(f => f.Replace("${RestorePath}", restorePath)
                                .Replace("${RuntimeVersion}", RuntimeVersion)
                               .Replace("${PackageVersion}", DefaultPackageVersion)
                               .Replace("${OutputPath}", buildOrPublishFolder)
                               .Replace("${IntermediateOutputPath}", intermediateOutputPath)
                               .Replace("${ProjectPath}", projectPath)
                               .Replace('\\', Path.DirectorySeparatorChar));


        internal void AssertManifest(
            StaticWebAssetsManifest manifest,
            StaticWebAssetsManifest expected,
            string suffix = "",
            [CallerMemberName] string name = "")
        {
            if (!_generateBaselines)
            {
                ApplyPathsToAssets(expected, ProjectDirectory.TestRoot, TestContext.Current.NuGetCachePath);
                ApplyTemplatizerToAssets(manifest);
                UpdateCustomPackageVersions(TestContext.Current.NuGetCachePath, manifest);
                //Many of the properties in the manifest contain full paths, to avoid flakiness on the tests, we don't compare the full paths.
                manifest.Version.Should().Be(expected.Version);
                manifest.Source.Should().Be(expected.Source);
                manifest.BasePath.Should().Be(expected.BasePath);
                manifest.Mode.Should().Be(expected.Mode);
                manifest.ManifestType.Should().Be(expected.ManifestType);
                manifest.ReferencedProjectsConfiguration.OrderBy(cm => cm.Identity)
                    .Should()
                    .BeEquivalentTo(expected.ReferencedProjectsConfiguration.OrderBy(cm => cm.Identity));
                manifest.DiscoveryPatterns.OrderBy(dp => dp.Name).ShouldBeEquivalentTo(expected.DiscoveryPatterns.OrderBy(dp => dp.Name));
                manifest.Assets.OrderBy(a => a.BasePath).ThenBy(a => a.RelativePath).ThenBy(a => a.AssetKind)
                    .ShouldBeEquivalentTo(expected.Assets.OrderBy(a => a.BasePath).ThenBy(a => a.RelativePath).ThenBy(a => a.AssetKind));
            }
            else
            {
                var template = Templatize(manifest, ProjectDirectory.Path, TestContext.Current.NuGetCachePath);
                if (!Directory.Exists(Path.Combine(BaselinesFolder)))
                {
                    Directory.CreateDirectory(Path.Combine(BaselinesFolder));
                }

                File.WriteAllText(GetManifestPath(suffix, name, manifest.ManifestType), template);
            }
        }

        private string GetManifestPath(string suffix, string name, string manifestType)
            => Path.Combine(BaselinesFolder, $"{name}{(!string.IsNullOrEmpty(suffix) ? $"_{suffix}" : "")}.{manifestType}.staticwebassets.json");

        private Stream GetManifestEmbeddedResource(string suffix, string name, string manifestType)
            => TestAssembly.GetManifestResourceStream(string.Join('.', EmbeddedResourcePrefix, $"{name}{(!string.IsNullOrEmpty(suffix) ? $"_{suffix}" : "")}.{manifestType}.staticwebassets.json"));


        private string GetExpectedFilesPath(string suffix, string name, string manifestType)
            => Path.Combine(BaselinesFolder, $"{name}{(!string.IsNullOrEmpty(suffix) ? $"_{suffix}" : "")}.{manifestType}.files.json");

        private Stream GetExpectedFilesEmbeddedResource(string suffix, string name, string manifestType)
            => TestAssembly.GetManifestResourceStream(string.Join('.', EmbeddedResourcePrefix, $"{name}{(!string.IsNullOrEmpty(suffix) ? $"_{suffix}" : "")}.{manifestType}.files.json"));

        private void ApplyPathsToAssets(
            StaticWebAssetsManifest manifest,
            string projectRoot,
            string restorePath)
        {
            foreach (var asset in manifest.Assets)
            {
                asset.Identity = asset.Identity.Replace("${ProjectRoot}", projectRoot);
                asset.Identity = ReplaceRestorePath(restorePath, asset.Identity);

                asset.RelativePath = asset.RelativePath.Replace("${RuntimeVersion}", RuntimeVersion);

                asset.ContentRoot = asset.ContentRoot.Replace("${ProjectRoot}", projectRoot);
                asset.ContentRoot = ReplaceRestorePath(restorePath, asset.ContentRoot);

                asset.RelatedAsset = asset.RelatedAsset.Replace("${ProjectRoot}", projectRoot);
                asset.RelatedAsset = ReplaceRestorePath(restorePath, asset.RelatedAsset);

                asset.OriginalItemSpec = asset.OriginalItemSpec.Replace("${ProjectRoot}", projectRoot);
                asset.OriginalItemSpec = ReplaceRestorePath(restorePath, asset.OriginalItemSpec);
            }

            foreach (var discovery in manifest.DiscoveryPatterns)
            {
                discovery.ContentRoot = discovery.ContentRoot.Replace("${ProjectRoot}", projectRoot);
                discovery.ContentRoot = discovery.ContentRoot
                    .Replace('\\', Path.DirectorySeparatorChar);

                discovery.Name = discovery.Name.Replace('\\', Path.DirectorySeparatorChar);
                discovery.Pattern.Replace('\\', Path.DirectorySeparatorChar);
            }

            foreach (var relatedConfiguration in manifest.ReferencedProjectsConfiguration)
            {
                relatedConfiguration.Identity = relatedConfiguration.Identity.Replace("${ProjectRoot}", projectRoot).Replace('\\', Path.DirectorySeparatorChar);
            }

            string ReplaceRestorePath(string restorePath, string property)
            {
                return property
                    .Replace("${RestorePath}", restorePath)
                    .Replace("${RuntimeVersion}", RuntimeVersion)
                    .Replace("${PackageVersion}", DefaultPackageVersion)
                    .Replace('\\', Path.DirectorySeparatorChar);
            }
        }

        private string Templatize(StaticWebAssetsManifest manifest, string projectRoot, string restorePath)
        {
            manifest.Hash = "__hash__";
            var assetsByIdentity = manifest.Assets.ToDictionary(a => a.Identity);
            foreach (var asset in manifest.Assets)
            {
                TemplatizeAsset(projectRoot, restorePath, asset);

                if (!string.IsNullOrEmpty(asset.RelatedAsset))
                {
                    var relatedAsset = string.IsNullOrEmpty(asset.RelatedAsset) || !assetsByIdentity.TryGetValue(asset.RelatedAsset, out var related) ?
                        null : related;
                    if (relatedAsset != null)
                    {
                        TemplatizeAsset(projectRoot, restorePath, relatedAsset);
                    }
                    TemplatizeAsset(projectRoot, restorePath, asset);
                    asset.RelatedAsset = PathTemplatizer(asset, asset.RelatedAsset, relatedAsset) ?? asset.RelatedAsset;
                }

                asset.OriginalItemSpec = asset.OriginalItemSpec.Replace(projectRoot, "${ProjectRoot}");
                asset.OriginalItemSpec = TemplatizeRestorePath(restorePath, asset.OriginalItemSpec);
                asset.OriginalItemSpec = PathTemplatizer(asset, asset.OriginalItemSpec, null) ?? asset.OriginalItemSpec;
            }

            foreach (var discovery in manifest.DiscoveryPatterns)
            {
                discovery.ContentRoot = discovery.ContentRoot.Replace(projectRoot, "${ProjectRoot}");
                discovery.ContentRoot = discovery.ContentRoot
                    .Replace(Path.DirectorySeparatorChar, '\\');
                discovery.Name = discovery.Name.Replace(Path.DirectorySeparatorChar, '\\');
                discovery.Pattern = discovery.Pattern.Replace(Path.DirectorySeparatorChar, '\\');
            }

            foreach (var relatedManifest in manifest.ReferencedProjectsConfiguration)
            {
                relatedManifest.Identity = relatedManifest.Identity.Replace(projectRoot, "${ProjectRoot}").Replace(Path.DirectorySeparatorChar, '\\');
            }

            // Sor everything now to ensure we produce stable baselines independent of the machine they were generated on.
            Array.Sort(manifest.DiscoveryPatterns, (l, r) => StringComparer.Ordinal.Compare(l.Name, r.Name));
            Array.Sort(manifest.Assets, (l, r) => StringComparer.Ordinal.Compare(l.Identity, r.Identity));
            Array.Sort(manifest.ReferencedProjectsConfiguration, (l, r) => StringComparer.Ordinal.Compare(l.Identity, r.Identity));
            return JsonSerializer.Serialize(manifest, BaselineSerializationOptions);

            void TemplatizeAsset(string projectRoot, string restorePath, StaticWebAsset asset)
            {
                asset.Identity = asset.Identity.Replace(projectRoot, "${ProjectRoot}");
                asset.Identity = TemplatizeRestorePath(restorePath, asset.Identity);
                asset.Identity = PathTemplatizer(asset, asset.Identity, null) ?? asset.Identity;

                asset.RelativePath = asset.RelativePath.Replace(RuntimeVersion, "${RuntimeVersion}");

                asset.ContentRoot = asset.ContentRoot.Replace(projectRoot, "${ProjectRoot}");
                asset.ContentRoot = TemplatizeRestorePath(restorePath, asset.ContentRoot) + '\\';

                asset.RelatedAsset = asset.RelatedAsset.Replace(projectRoot, "${ProjectRoot}");
                asset.RelatedAsset = TemplatizeRestorePath(restorePath, asset.RelatedAsset);
            }

            string TemplatizeRestorePath(string restorePath, string property)
            {
                property = property
                    .Replace(restorePath, "${RestorePath}")
                    .Replace(Path.DirectorySeparatorChar, '\\');

                var customPackageVersion = true;
                var segments = property.Split('\\', StringSplitOptions.RemoveEmptyEntries);
                for (var i = 0; i < segments.Length; i++)
                {
                    ref var segment = ref segments[i];
                    if (segment.Contains(RuntimeVersion))
                    {
                        segment = segment.Replace(RuntimeVersion, "${RuntimeVersion}");
                        customPackageVersion = false;
                    }
                    if (segment == DefaultPackageVersion)
                    {
                        segment = "${PackageVersion}";
                        customPackageVersion = false;
                    }
                }

                if (segments.Length > 0 && segments[0] == "${RestorePath}" && customPackageVersion)
                {
                    segments[2] = "[[CustomPackageVersion]]";
                }

                return string.Join('\\', segments);
            }
        }
    }
}
