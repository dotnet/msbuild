// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.StaticWebAssets.Tasks;

namespace Microsoft.NET.Sdk.Razor.Tests;

public class StaticWebAssetsBaselineComparer
{
    private static readonly string BaselineGenerationInstructions =
    @"If the difference in baselines is expected, please re-generate the baselines.
Start by ensuring you're dogfooding the SDK from the current branch (dotnet --version should be '*.0.0-dev').
    If you're not on the dogfood sdk, from the root of the repository run:
        1. dotnet clean
        2. .\restore.cmd or ./restore.sh
        3. .\build.cmd ./build.sh
        4. .\eng\dogfood.cmd or . ./eng/dogfood.sh

Then, using the dogfood SDK run the .\src\RazorSdk\update-test-baselines.ps1 script.";

    public static StaticWebAssetsBaselineComparer Instance { get; } = new();

    internal void AssertManifest(StaticWebAssetsManifest expected, StaticWebAssetsManifest manifest)
    {
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

        var manifestAssets = manifest.Assets
            .OrderBy(a => a.BasePath)
            .ThenBy(a => a.RelativePath)
            .ThenBy(a => a.AssetKind)
            .GroupBy(a => GetGroup(a))
            .ToDictionary(a => a.Key, a => a.ToArray());

        var expectedAssets = expected.Assets
            .OrderBy(a => a.BasePath)
            .ThenBy(a => a.RelativePath)
            .ThenBy(a => a.AssetKind)
            .GroupBy(a => GetGroup(a))
            .ToDictionary(a => a.Key, a => a.ToArray());

        foreach (var (group, manifestAssetsGroup) in manifestAssets)
        {
            var expectedAssetsGroup = expectedAssets[group];
            CompareGroup(group, manifestAssetsGroup, expectedAssetsGroup);
        }
    }

    protected virtual void CompareGroup(string group, StaticWebAsset[] manifestAssets, StaticWebAsset[] expectedAssets)
    {
        var comparisonMode = CompareAssetCounts(group, manifestAssets, expectedAssets);

        // Otherwise, do a property level comparison of all assets
        switch (comparisonMode)
        {
            case GroupComparisonMode.Exact:
                break;
            case GroupComparisonMode.AllowAdditionalAssets:
                break;
            default:
                break;
        }

        var differences = new List<string>();
        var assetDifferences = new List<string>();
        var groupLength = Math.Min(manifestAssets.Length, expectedAssets.Length);
        for (var i = 0; i < groupLength; i++)
        {
            var manifestAsset = manifestAssets[i];
            var expectedAsset = expectedAssets[i];

            ComputeAssetDifferences(assetDifferences, manifestAsset, expectedAsset);

            if (assetDifferences.Any())
            {
                differences.Add(@$"
==================================================

For {expectedAsset.Identity}:

{string.Join(Environment.NewLine, assetDifferences)}

==================================================");
            }

            assetDifferences.Clear();
        }

        differences.Should().BeEmpty(
            @$" the generated manifest should match the expected baseline.

{BaselineGenerationInstructions}

");
    }

    private GroupComparisonMode CompareAssetCounts(string group, StaticWebAsset[] manifestAssets, StaticWebAsset[] expectedAssets)
    {
        var comparisonMode = GetGroupComparisonMode(group);

        // If there's a mismatch in the number of assets, just print the strict difference in the asset `Identity`
        switch (comparisonMode)
        {
            case GroupComparisonMode.Exact:
                if (manifestAssets.Length != expectedAssets.Length)
                {
                    ThrowAssetCountMismatchError(manifestAssets, expectedAssets);
                }
                break;
            case GroupComparisonMode.AllowAdditionalAssets:
                if (expectedAssets.Except(manifestAssets).Any())
                {
                    ThrowAssetCountMismatchError(manifestAssets, expectedAssets);
                }
                break;
            default:
                break;
        }

        return comparisonMode;

        static void ThrowAssetCountMismatchError(StaticWebAsset[] manifestAssets, StaticWebAsset[] expectedAssets)
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

    protected virtual GroupComparisonMode GetGroupComparisonMode(string group)
    {
        return GroupComparisonMode.Exact;
    }

    private static void ComputeAssetDifferences(List<string> assetDifferences, StaticWebAsset manifestAsset, StaticWebAsset expectedAsset)
    {
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
    }

    protected virtual string GetGroup(StaticWebAsset asset)
    {
        return Path.GetExtension(asset.Identity.TrimEnd(']'));
    }
}

public enum GroupComparisonMode
{
    // We require the same number of assets in a group for the baseline and the template.
    Exact,

    // We won't fail when we check against the baseline if additional assets are present for a group.
    AllowAdditionalAssets
}
