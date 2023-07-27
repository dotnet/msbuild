// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks
{
    public class ComputeReferenceStaticWebAssetItems : Task
    {
        [Required]
        public ITaskItem[] Assets { get; set; }

        public ITaskItem[] Patterns { get; set; }

        [Required]
        public string AssetKind { get; set; }

        [Required]
        public string ProjectMode { get; set; }

        [Required]
        public string Source { get; set; }

        public bool UpdateSourceType { get; set; } = true;

        [Output]
        public ITaskItem[] StaticWebAssets { get; set; }

        [Output]
        public ITaskItem[] DiscoveryPatterns { get; set; }

        public override bool Execute()
        {
            try
            {
                var existingAssets = Assets
                    .Where(asset => StaticWebAsset.HasSourceId(asset, Source))
                    .Select(StaticWebAsset.FromTaskItem)
                    .GroupBy(
                        a => a.ComputeTargetPath("", '/'),
                        (key, group) => (key, StaticWebAsset.ChooseNearestAssetKind(group, AssetKind)));

                var resultAssets = new List<StaticWebAsset>();
                foreach (var (key, group) in existingAssets)
                {
                    if (!TryGetUniqueAsset(group, out var selected))
                    {
                        if (selected == null)
                        {
                            Log.LogMessage(MessageImportance.Low, "No compatible asset found for '{0}'", key);
                            continue;
                        }
                        else
                        {
                            Log.LogError("More than one compatible asset found for '{0}'.", selected.Identity);
                            return false;
                        }
                    }

                    if (ShouldIncludeAssetAsReference(selected, out var reason))
                    {
                        selected.SourceType = UpdateSourceType ? StaticWebAsset.SourceTypes.Project : selected.SourceType;
                        resultAssets.Add(selected);
                    }
                    Log.LogMessage(MessageImportance.Low, reason);
                }

                var patterns = new List<StaticWebAssetsDiscoveryPattern>();
                if (Patterns != null)
                {
                    foreach (var pattern in Patterns)
                    {
                        if (!StaticWebAssetsDiscoveryPattern.HasSourceId(pattern, Source))
                        {
                            Log.LogMessage(MessageImportance.Low, "Skipping pattern '{0}' because is not defined in the current project.", pattern.ItemSpec);
                        }
                        else
                        {
                            Log.LogMessage(MessageImportance.Low, "Including pattern '{0}' because is defined in the current project.", pattern.ToString());
                            patterns.Add(StaticWebAssetsDiscoveryPattern.FromTaskItem(pattern));
                        }
                    }
                }

                StaticWebAssets = resultAssets.Select(a => a.ToTaskItem()).ToArray();
                DiscoveryPatterns = patterns.Select(p => p.ToTaskItem()).ToArray();
            }
            catch (Exception ex)
            {
                Log.LogErrorFromException(ex, showStackTrace: true, showDetail: true, file: null);
            }

            return !Log.HasLoggedErrors;
        }

        private bool TryGetUniqueAsset(IEnumerable<StaticWebAsset> candidates, out StaticWebAsset selected)
        {
            selected = null;
            foreach (var asset in candidates)
            {
                if (selected != null)
                {
                    return false;
                }

                selected = asset;
            }

            return selected != null;
        }

        private bool ShouldIncludeAssetAsReference(StaticWebAsset candidate, out string reason)
        {
            if (!StaticWebAssetsManifest.ManifestModes.ShouldIncludeAssetAsReference(candidate, ProjectMode))
            {
                reason = string.Format(
                    "Skipping candidate asset '{0}' because project mode is '{1}' and asset mode is '{2}'",
                    candidate.Identity,
                    ProjectMode,
                    candidate.AssetMode);
                return false;
            }

            reason = string.Format(
                "Accepted candidate asset '{0}' because project mode is '{1}' and asset mode is '{2}'",
                candidate.Identity,
                ProjectMode,
                candidate.AssetMode);

            return true;
        }
    }
}
