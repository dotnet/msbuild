// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Microsoft.AspNetCore.Razor.Tasks
{
    public class DiscoverStaticWebAssets : Task
    {
        [Required]
        public ITaskItem[] Candidates { get; set; }

        [Required]
        public string Pattern { get; set; }

        [Required]
        public string SourceId { get; set; }

        [Required]
        public string ContentRoot { get; set; }

        [Required]
        public string BasePath { get; set; }

        [Output]
        public ITaskItem[] DiscoveredStaticWebAssets { get; set; }

        public override bool Execute()
        {
            try
            {
                var matcher = new Matcher().AddInclude(Pattern);
                var assets = new List<ITaskItem>();
                var assetsByRelativePath = new Dictionary<string, List<ITaskItem>>();

                for (var i = 0; i < Candidates.Length; i++)
                {
                    var candidate = Candidates[i];
                    var candidateMatchPath = GetCandidateMatchPath(candidate);
                    var candidateRelativePath = candidateMatchPath;
                    if (string.IsNullOrEmpty(candidate.GetMetadata("RelativePath")))
                    {
                        var match = matcher.Match(candidateMatchPath);
                        if (!match.HasMatches)
                        {
                            Log.LogMessage("Rejected asset '{0}' for pattern '{1}'", candidateMatchPath, Pattern);
                            continue;
                        }

                        Log.LogMessage("Accepted asset '{0}' for pattern '{1}' with relative path '{2}'", candidateMatchPath, Pattern, match.Files.Single().Stem);

                        candidateRelativePath = StaticWebAsset.Normalize(match.Files.Single().Stem);
                    }

                    var asset = new StaticWebAsset
                    {
                        Identity = candidate.GetMetadata("FullPath"),
                        SourceId = SourceId,
                        SourceType = StaticWebAsset.SourceTypes.Discovered,
                        ContentRoot = ContentRoot,
                        BasePath = BasePath,
                        RelativePath = candidateRelativePath,
                        AssetMode = StaticWebAsset.AssetModes.All,
                        CopyToOutputDirectory = candidate.GetMetadata(nameof(StaticWebAsset.CopyToOutputDirectory)),
                        CopyToPublishDirectory = candidate.GetMetadata(nameof(StaticWebAsset.CopyToPublishDirectory))
                    };

                    asset.ApplyDefaults();
                    asset.Normalize();

                    var assetItem = asset.ToTaskItem();

                    assetItem.SetMetadata("OriginalItemSpec", candidate.ItemSpec);
                    assets.Add(assetItem);

                    UpdateAssetKindIfNecessary(assetsByRelativePath, candidateRelativePath, assetItem);
                    if (Log.HasLoggedErrors)
                    {
                        return false;
                    }
                }

                DiscoveredStaticWebAssets = assets.ToArray();
            }
            catch (Exception ex)
            {
                Log.LogErrorFromException(ex, showStackTrace: true, showDetail: true, file: null);
            }

            return !Log.HasLoggedErrors;
        }

        private string GetCandidateMatchPath(ITaskItem candidate)
        {
            var computedPath = StaticWebAsset.ComputeAssetRelativePath(candidate, out var property);
            if (property != null)
            {
                Log.LogMessage(
                    "{0} '{1}' found for candidate '{2}' and will be used for matching.",
                    property,
                    computedPath,
                    candidate.ItemSpec);
            }

            return computedPath;
        }

        private void UpdateAssetKindIfNecessary(Dictionary<string, List<ITaskItem>> assetsByRelativePath, string candidateRelativePath, ITaskItem asset)
        {
            // We want to support content items in the form of
            // <Content Include="service-worker.development.js CopyToPublishDirectory="Never" TargetPath="wwwroot\service-worker.js" />
            // <Content Include="service-worker.js />
            // where the first item is used during development and the second item is used when the app is published.
            // To that matter, we keep track of the assets relative paths and make sure that when two assets target the same relative paths, at least one
            // of them is marked with CopyToPublishDirectory="Never" to identify it as a "development/build" time asset as opposed to the other asset.
            // As a result, assets by default have an asset kind 'All' when there is only one asset for the target path and 'Build' or 'Publish' when there are two of them.
            if (!assetsByRelativePath.TryGetValue(candidateRelativePath, out var existing))
            {
                assetsByRelativePath.Add(candidateRelativePath, new List<ITaskItem> { asset });
            }
            else
            {
                if (existing.Count == 2)
                {
                    var first = existing[0];
                    var second = existing[1];
                    var errorMessage = "More than two assets are targeting the same path: " + Environment.NewLine +
                        "'{0}' with kind '{1}'" + Environment.NewLine +
                        "'{2}' with kind '{3}'" + Environment.NewLine +
                        "for path '{4}'";

                    Log.LogError(
                        errorMessage,
                        first.GetMetadata("FullPath"),
                        first.GetMetadata(nameof(StaticWebAsset.AssetKind)),
                        second.GetMetadata("FullPath"),
                        second.GetMetadata(nameof(StaticWebAsset.AssetKind)),
                        candidateRelativePath);

                    return;
                }
                else if (existing.Count == 1)
                {
                    var existingAsset = existing[0];
                    switch ((asset.GetMetadata(nameof(StaticWebAsset.CopyToPublishDirectory)), existingAsset.GetMetadata(nameof(StaticWebAsset.CopyToPublishDirectory))))
                    {
                        case (StaticWebAsset.AssetCopyOptions.Never, StaticWebAsset.AssetCopyOptions.Never):
                        case (not StaticWebAsset.AssetCopyOptions.Never, not StaticWebAsset.AssetCopyOptions.Never):
                            var errorMessage = "Two assets found targeting the same path with incompatible asset kinds: " + Environment.NewLine +
                                "'{0}' with kind '{1}'" + Environment.NewLine +
                                "'{2}' with kind '{3}'" + Environment.NewLine +
                                "for path '{4}'";
                            Log.LogError(
                                errorMessage,
                                existingAsset.GetMetadata("FullPath"),
                                existingAsset.GetMetadata(nameof(StaticWebAsset.AssetKind)),
                                asset.GetMetadata("FullPath"),
                                asset.GetMetadata(nameof(StaticWebAsset.AssetKind)),
                                candidateRelativePath);

                            break;

                        case (StaticWebAsset.AssetCopyOptions.Never, not StaticWebAsset.AssetCopyOptions.Never):
                            existing.Add(asset);
                            asset.SetMetadata(nameof(StaticWebAsset.AssetKind), StaticWebAsset.AssetKinds.Build);
                            existingAsset.SetMetadata(nameof(StaticWebAsset.AssetKind), StaticWebAsset.AssetKinds.Publish);
                            break;

                        case (not StaticWebAsset.AssetCopyOptions.Never, StaticWebAsset.AssetCopyOptions.Never):
                            existing.Add(asset);
                            asset.SetMetadata(nameof(StaticWebAsset.AssetKind), StaticWebAsset.AssetKinds.Publish);
                            existingAsset.SetMetadata(nameof(StaticWebAsset.AssetKind), StaticWebAsset.AssetKinds.Build);
                            break;
                    }
                }
            }
        }
    }
}
