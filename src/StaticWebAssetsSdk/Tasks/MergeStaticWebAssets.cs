// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

public class MergeStaticWebAssets : Task
{
    [Required]
    public ITaskItem[] CandidateAssets { get; set; }

    [Required]
    public ITaskItem[] CandidateDiscoveryPatterns { get; set; }

    public string MergeTarget { get; set; } = "";

    [Output]
    public ITaskItem[] MergedAssets { get; set; }

    [Output]
    public ITaskItem[] MergedDiscoveryPatterns { get; set; }

    public override bool Execute()
    {

        var assets = CandidateAssets.OrderBy(a => a.GetMetadata("FullPath")).Select(StaticWebAsset.FromTaskItem);

        var assetsByTargetPath = assets
            .GroupBy(a => a.ComputeTargetPath("", '/'), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var kvp in assetsByTargetPath)
        {
            var group = kvp.Value;
            if (group.Count > 1)
            {
                Log.LogMessage(MessageImportance.Normal, $"Merging '{group.Count}' assets for {kvp.Key}.");
                ApplyMergeRules(group, MergeTarget);
            }
        }

        MergedAssets = assetsByTargetPath.Values.SelectMany(g => g).Select(a => a.ToTaskItem()).ToArray();

        // We always want to merge the discovery patterns, we just need to remove duplicates if any.           
        var candidates = CandidateDiscoveryPatterns.Select(StaticWebAssetsDiscoveryPattern.FromTaskItem).ToList();
        for (var i = candidates.Count - 1; i > 0; i--)
        {
            var candidate = candidates[i];
            for (var j = i - 1; j >= 0; j--)
            {
                var other = candidates[j];
                if (candidate.Equals(other))
                {
                    Log.LogMessage(MessageImportance.Normal, $"Removing '{candidate.ContentRoot}' because it is a duplicate of '{other.ContentRoot}'.");
                    candidates.RemoveAt(i);
                    break;
                }
            }
        }

        MergedDiscoveryPatterns = candidates.Select(a => a.ToTaskItem()).ToArray();

        return !Log.HasLoggedErrors;
    }

    internal void ApplyMergeRules(List<StaticWebAsset> group, string source)
    {
        // All the assets in the group target the same relative path. The normal outcome for that is a conflict,
        // except when we are merging assets across targets, in which case there are rules to determine what happens.
        StaticWebAsset prototypeItem = null;
        StaticWebAsset build = null;
        StaticWebAsset publish = null;
        StaticWebAsset all = null;

        var assetsToRemove = new List<StaticWebAsset>();
        foreach (var item in group)
        {
            prototypeItem ??= item;
            if (!ReferenceEquals(prototypeItem, item) && string.Equals(prototypeItem.Identity, item.Identity, OSPath.PathComparison))
            {
                var assetToRemove = SelectAssetToRemove(prototypeItem, item, source);
                if (assetToRemove != null)
                {
                    Log.LogMessage(MessageImportance.Normal, $"Removing '{assetToRemove.Identity}' because merge behavior is {assetToRemove.AssetMergeBehavior}.");
                    assetsToRemove.Add(assetToRemove);
                    continue;
                }
            }

            if (!prototypeItem.HasSourceId(item.SourceId))
            {
                var assetToRemove = SelectAssetToRemove(prototypeItem, item, source);
                if (assetToRemove != null)
                {
                    Log.LogMessage(MessageImportance.Normal, $"Removing '{assetToRemove.Identity}' because merge behavior is {assetToRemove.AssetMergeBehavior}.");
                    assetsToRemove.Add(assetToRemove);
                    continue;
                }
            }

            build ??= item.IsBuildOnly() ? item : build;
            if (build != null && item.IsBuildOnly() && !ReferenceEquals(build, item))
            {
                var assetToRemove = SelectAssetToRemove(prototypeItem, item, source);
                if (assetToRemove != null)
                {
                    Log.LogMessage(MessageImportance.Normal, $"Removing '{assetToRemove.Identity}' because merge behavior is {assetToRemove.AssetMergeBehavior}.");
                    assetsToRemove.Add(assetToRemove);
                    continue;
                }
            }

            publish ??= item.IsPublishOnly() ? item : publish;
            if (publish != null && item.IsPublishOnly() && !ReferenceEquals(publish, item))
            {
                var assetToRemove = SelectAssetToRemove(prototypeItem, item, source);
                if (assetToRemove != null)
                {
                    Log.LogMessage(MessageImportance.Normal, $"Removing '{assetToRemove.Identity}' because merge behavior is {assetToRemove.AssetMergeBehavior}.");
                    assetsToRemove.Add(assetToRemove);
                    continue;
                }
            }

            all ??= item.IsBuildAndPublish() ? item : all;
            if (all != null && item.IsBuildAndPublish() && !ReferenceEquals(all, item))
            {
                var assetToRemove = SelectAssetToRemove(prototypeItem, item, source);
                if (assetToRemove != null)
                {
                    Log.LogMessage(MessageImportance.Normal, $"Removing '{assetToRemove.Identity}' because merge behavior is {assetToRemove.AssetMergeBehavior}.");
                    assetsToRemove.Add(assetToRemove);
                    continue;
                }
            }
        }

        foreach (var asset in assetsToRemove)
        {
            group.Remove(asset);
        }

        StaticWebAsset SelectAssetToRemove(StaticWebAsset left, StaticWebAsset right, string mergeTarget)
        {
            var leftMergeSource = left.AssetMergeSource;
            var rightMergeSource = right.AssetMergeSource;
            if (string.Equals(leftMergeSource, rightMergeSource, StringComparison.Ordinal))
            {
                Log.LogMessage(MessageImportance.Normal, $"Skipping '{right.Identity}' because it is a duplicate of '{left.Identity}'.");
                return null;
            }

            var (targetAsset, sourceAsset) = string.Equals(leftMergeSource, mergeTarget) ? (left, right) : (right, left);
            if (!string.Equals(targetAsset.AssetMergeBehavior, sourceAsset.AssetMergeBehavior, StringComparison.Ordinal))
            {
                Log.LogMessage(MessageImportance.Normal, $"Skipping '{sourceAsset.Identity}' because merge behavior '{sourceAsset.AssetMergeBehavior}' is different from '{targetAsset.AssetMergeBehavior}'.");
                return null;
            }

            var behavior = targetAsset.AssetMergeBehavior;
            // PreferTarget: The target asset wins.
            // PreferSource: The source asset wins.
            // Exclude: The assets are not merged, and a failure happens later on during validation.
            return string.Equals(behavior, "PreferTarget", StringComparison.Ordinal) ? targetAsset :
                (string.Equals(behavior, "PreferSource", StringComparison.Ordinal) ? sourceAsset : null);
        }
    }
}
