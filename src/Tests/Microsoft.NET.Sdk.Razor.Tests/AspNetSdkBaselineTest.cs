// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
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
        private static readonly string BaselineGenerationInstructions =
            @"If the difference in baselines is expected, please re-generate the baselines.
Note, baseline generation must be done on a Windows device.
Start by ensuring you're dogfooding the SDK from the current branch (dotnet --version should be '*.0.0-dev').
    If you're not on the dogfood sdk, from the root of the repository run:
        1. dotnet clean
        2. .\restore.cmd
        3. .\build.cmd
        4. .\eng\dogfood.cmd

Then, using the dogfood SDK run the .\src\RazorSdk\update-test-baselines.ps1 script.";

        protected static readonly string DotNetJSHashRegexPattern = "\\.[a-z0-9]{10}\\.js";
        protected static readonly string DotNetJSHashTemplate = ".[[hash]].js";

        private string _baselinesFolder;

#if GENERATE_SWA_BASELINES
        public static bool GenerateBaselines = true;
#else
        public static bool GenerateBaselines = bool.TryParse(Environment.GetEnvironmentVariable("ASPNETCORE_TEST_BASELINES"), out var result) && result;
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
                RemoveHashFromAsset(asset);
                var relatedAsset = string.IsNullOrEmpty(asset.RelatedAsset) || !assetsById.TryGetValue(asset.RelatedAsset, out var related) ?
                    null : related;
                asset.Identity = PathTemplatizer(asset, asset.Identity, null) ?? asset.Identity;
                asset.RelatedAsset = PathTemplatizer(asset, asset.RelatedAsset, relatedAsset) ?? asset.RelatedAsset;
                asset.OriginalItemSpec = PathTemplatizer(asset, asset.OriginalItemSpec, null) ?? asset.OriginalItemSpec;
            }
        }

        private void RemoveHashFromAsset(StaticWebAsset asset)
        {
            asset.RelativePath = Regex.Replace(asset.RelativePath, DotNetJSHashRegexPattern, DotNetJSHashTemplate);
            asset.Identity = Regex.Replace(asset.Identity, DotNetJSHashRegexPattern, DotNetJSHashTemplate);
            asset.OriginalItemSpec = Regex.Replace(asset.OriginalItemSpec, DotNetJSHashRegexPattern, DotNetJSHashTemplate);
            asset.RelatedAsset = Regex.Replace(asset.RelatedAsset, DotNetJSHashRegexPattern, DotNetJSHashTemplate);
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
                    .Select(f => Regex.Replace(f, DotNetJSHashRegexPattern, DotNetJSHashTemplate))
                    .OrderBy(f => f, StringComparer.Ordinal)
                    .ToArray();

                existingFiles.Should().BeEquivalentTo(expected);
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
                existingFiles = existingFiles.Select(f => Regex.Replace(f, DotNetJSHashRegexPattern, DotNetJSHashTemplate)).ToArray();

                var expected = LoadExpectedFilesBaseline(manifest.ManifestType, publishFolder, intermediateOutputPath, suffix, name);
                existingFiles.Should().BeEquivalentTo(expected);
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
                var updated = Regex.Replace(f, DotNetJSHashRegexPattern, DotNetJSHashTemplate);
                updated = updated.Replace(restorePath, "${RestorePath}")
                    .Replace(RuntimeVersion, "${RuntimeVersion}")
                    .Replace(DefaultTfm, "${Tfm}")
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
                                .Replace("${Tfm}", DefaultTfm)
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

                manifest.ReferencedProjectsConfiguration.Should().HaveSameCount(expected.ReferencedProjectsConfiguration);

                // Relax the check for project reference configuration items see
                // https://github.com/dotnet/sdk/pull/27381#issuecomment-1228764471
                // for details.
                //manifest.ReferencedProjectsConfiguration.OrderBy(cm => cm.Identity)
                //    .Should()
                //    .BeEquivalentTo(expected.ReferencedProjectsConfiguration.OrderBy(cm => cm.Identity));

                manifest.DiscoveryPatterns.OrderBy(dp => dp.Name).Should().BeEquivalentTo(expected.DiscoveryPatterns.OrderBy(dp => dp.Name));

                var manifestAssets = manifest.Assets.OrderBy(a => a.BasePath).ThenBy(a => a.RelativePath).ThenBy(a => a.AssetKind);
                var expectedAssets = expected.Assets.OrderBy(a => a.BasePath).ThenBy(a => a.RelativePath).ThenBy(a => a.AssetKind);

                // If there's a mismatch in the number of assets, just print the strict difference in the asset `Identity`
                if (manifestAssets.Count() != expectedAssets.Count())
                {
                    ThrowAssetCountMismatchError(manifestAssets, expectedAssets);
                }

                // Otherwise, do a property level comparison of all assets
                var manifestAssetsEnumerator = manifestAssets.GetEnumerator();
                var expectedAssetsEnumerator = expectedAssets.GetEnumerator();

                var differences = new List<string>();

                do
                {
                    var manifestAsset = manifestAssetsEnumerator.Current;
                    var expectedAsset = expectedAssetsEnumerator.Current;

                    if (manifestAsset is null && expectedAsset is null)
                    {
                        continue;
                    }

                    var assetDifferences = new List<string>();

                    if (manifestAsset.Identity != expectedAsset.Identity)
                    {
                        assetDifferences.Add($"Expected manifest Identity of {expectedAsset.Identity} but found {manifestAsset.Identity}.");
                    }
                    if (manifestAsset.SourceType != expectedAsset.SourceType)
                    {
                        assetDifferences.Add($"Expected manifest SourceType of {expectedAsset.SourceType} but found {manifestAsset.SourceType}.");
                    }
                    if (manifestAsset.SourceId != expectedAsset.SourceId)
                    {
                        assetDifferences.Add($"Expected manifest SourceId of {expectedAsset.SourceId} but found {manifestAsset.SourceId}.");
                    }
                    if (manifestAsset.ContentRoot != expectedAsset.ContentRoot)
                    {
                        assetDifferences.Add($"Expected manifest ContentRoot of {expectedAsset.ContentRoot} but found {manifestAsset.ContentRoot}.");
                    }
                    if (manifestAsset.BasePath != expectedAsset.BasePath)
                    {
                        assetDifferences.Add($"Expected manifest BasePath of {expectedAsset.BasePath} but found {manifestAsset.BasePath}.");
                    }
                    if (manifestAsset.RelativePath != expectedAsset.RelativePath)
                    {
                        assetDifferences.Add($"Expected manifest RelativePath of {expectedAsset.RelativePath} but found {manifestAsset.RelativePath}.");
                    }
                    if (manifestAsset.AssetKind != expectedAsset.AssetKind)
                    {
                        assetDifferences.Add($"Expected manifest AssetKind of {expectedAsset.AssetKind} but found {manifestAsset.AssetKind}.");
                    }
                    if (manifestAsset.AssetMode != expectedAsset.AssetMode)
                    {
                        assetDifferences.Add($"Expected manifest AssetMode of {expectedAsset.AssetMode} but found {manifestAsset.AssetMode}.");
                    }
                    if (manifestAsset.AssetRole != expectedAsset.AssetRole)
                    {
                        assetDifferences.Add($"Expected manifest AssetRole of {expectedAsset.AssetRole} but found {manifestAsset.AssetRole}.");
                    }
                    if (manifestAsset.RelatedAsset != expectedAsset.RelatedAsset)
                    {
                        assetDifferences.Add($"Expected manifest RelatedAsset of {expectedAsset.RelatedAsset} but found {manifestAsset.RelatedAsset}.");
                    }
                    if (manifestAsset.AssetTraitName != expectedAsset.AssetTraitName)
                    {
                        assetDifferences.Add($"Expected manifest AssetTraitName of {expectedAsset.AssetTraitName} but found {manifestAsset.AssetTraitName}.");
                    }
                    if (manifestAsset.AssetTraitValue != expectedAsset.AssetTraitValue)
                    {
                        assetDifferences.Add($"Expected manifest AssetTraitValue of {expectedAsset.AssetTraitValue} but found {manifestAsset.AssetTraitValue}.");
                    }
                    if (manifestAsset.CopyToOutputDirectory != expectedAsset.CopyToOutputDirectory)
                    {
                        assetDifferences.Add($"Expected manifest CopyToOutputDirectory of {expectedAsset.CopyToOutputDirectory} but found {manifestAsset.CopyToOutputDirectory}.");
                    }
                    if (manifestAsset.CopyToPublishDirectory != expectedAsset.CopyToPublishDirectory)
                    {
                        assetDifferences.Add($"Expected manifest CopyToPublishDirectory of {expectedAsset.CopyToPublishDirectory} but found {manifestAsset.CopyToPublishDirectory}.");
                    }
                    if (manifestAsset.OriginalItemSpec != expectedAsset.OriginalItemSpec)
                    {
                        assetDifferences.Add($"Expected manifest OriginalItemSpec of {expectedAsset.OriginalItemSpec} but found {manifestAsset.OriginalItemSpec}.");
                    }

                    if (assetDifferences.Any())
                    {
                        differences.Add(@$"
==================================================

For {expectedAsset.Identity}:

{string.Join(Environment.NewLine, assetDifferences)}

==================================================");
                    }

                } while (manifestAssetsEnumerator.MoveNext() && expectedAssetsEnumerator.MoveNext());

                differences.Should().BeEmpty(
                    @$" the generated manifest should match the expected baseline.

{BaselineGenerationInstructions}

");

                static void ThrowAssetCountMismatchError(IEnumerable<StaticWebAsset> manifestAssets, IEnumerable<StaticWebAsset> expectedAssets)
                {
                    var missingAssets = expectedAssets.Except(manifestAssets);
                    var unexpectedAssets = manifestAssets.Except(expectedAssets);

                    var differences = new List<string>();

                    if (missingAssets.Any())
                    {
                        differences.Add($@"The following expected assets weren't found in the manifest:
    {string.Join($"{Environment.NewLine}\t", missingAssets.Select(a => a.Identity))}");
                    }

                    if (unexpectedAssets.Any())
                    {
                        differences.Add($@"The following additional unexpected assets were found in the manifest:
    {string.Join($"{Environment.NewLine}\t", unexpectedAssets.Select(a => a.Identity))}");
                    }

                    throw new Exception($@"{string.Join(Environment.NewLine, differences)}

{BaselineGenerationInstructions}");
                }
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
                    .Replace("${Tfm}", DefaultTfm)
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

                asset.RelativePath = Regex.Replace(asset.RelativePath, DotNetJSHashRegexPattern, DotNetJSHashTemplate);
                asset.RelativePath = asset.RelativePath.Replace(RuntimeVersion, "${RuntimeVersion}");

                asset.ContentRoot = asset.ContentRoot.Replace(projectRoot, "${ProjectRoot}");
                asset.ContentRoot = TemplatizeRestorePath(restorePath, asset.ContentRoot) + '\\';

                asset.RelatedAsset = asset.RelatedAsset.Replace(projectRoot, "${ProjectRoot}");
                asset.RelatedAsset = TemplatizeRestorePath(restorePath, asset.RelatedAsset);
            }

            string TemplatizeRestorePath(string restorePath, string property)
            {
                property = property
                    .Replace(DefaultTfm, "${Tfm}")
                    .Replace(restorePath, "${RestorePath}")
                    .Replace(Path.DirectorySeparatorChar, '\\');

                var customPackageVersion = true;
                var segments = property.Split('\\', StringSplitOptions.RemoveEmptyEntries);
                for (var i = 0; i < segments.Length; i++)
                {
                    ref var segment = ref segments[i];
                    segment = Regex.Replace(segment, DotNetJSHashRegexPattern, DotNetJSHashTemplate);
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

