// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.Razor.Tasks
{
    public class ComputeReferencedProjectAssets : Task
    {
        [Required]
        public ITaskItem[] Manifests { get; set; }

        [Required]
        public ITaskItem[] ExistingAssets { get; set; }

        [Required]
        public string AssetKind { get; set; }

        [Output]
        public ITaskItem[] StaticWebAssets { get; set; }

        [Output]
        public ITaskItem[] DiscoveryPatterns { get; set; }

        public override bool Execute()
        {
            try
            {
                var manifests = new List<StaticWebAssetsManifest>();
                ReadCandidateManifests(manifests);

                if (Log.HasLoggedErrors)
                {
                    return false;
                }

                var existingAssets = ExistingAssets
                    .ToDictionary(a => (a.GetMetadata("AssetKind"), a.ItemSpec), a => StaticWebAsset.FromTaskItem(a));

                var staticWebAssets = new Dictionary<(string, string), StaticWebAsset>();
                var discoveryPatterns = new Dictionary<string, StaticWebAssetsManifest.DiscoveryPattern>();
                foreach (var manifest in manifests)
                {
                    MergeDiscoveryPatterns(discoveryPatterns, manifest);
                    if (Log.HasLoggedErrors)
                    {
                        break;
                    }

                    MergeStaticWebAssets(staticWebAssets, existingAssets, manifest);

                    if (Log.HasLoggedErrors)
                    {
                        break;
                    }
                }

                StaticWebAssets = staticWebAssets.Select(a => a.Value.ToTaskItem()).ToArray();
                DiscoveryPatterns = discoveryPatterns.Select(d => d.Value.ToTaskItem()).ToArray();
            }
            catch (Exception ex)
            {
                Log.LogError(ex.ToString());
            }

            return !Log.HasLoggedErrors;
        }

        private void ReadCandidateManifests(List<StaticWebAssetsManifest> manifests)
        {
            for (var i = 0; i < Manifests.Length; i++)
            {
                var manifest = Manifests[i];
                var manifestType = manifest.GetMetadata("ManifestType");
                if (!StaticWebAssetsManifest.ManifestTypes.IsType(manifestType, AssetKind))
                {
                    Log.LogMessage(
                        "Skipping manifest '{0}' because manifest type '{1}' is different from asset kind '{2}'",
                        manifest.ItemSpec,
                        manifestType,
                        AssetKind);

                    continue;
                }

                if (!File.Exists(manifest.ItemSpec))
                {
                    Log.LogError($"Manifest file '{manifest.ItemSpec}' does not exist.");
                    return;
                }

                manifests.Add(StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(manifest.ItemSpec)));
            }
        }

        private void MergeStaticWebAssets(
            Dictionary<(string, string), StaticWebAsset> staticWebAssets,
            Dictionary<(string, string), StaticWebAsset> existingStaticWebAssets,
            StaticWebAssetsManifest manifest)
        {
            foreach (var asset in manifest.Assets)
            {
                // Discovered and computed assets only matter for the current project. When they are being
                // used from a referenced project they get transformed into `Project` assets.
                if (asset.IsDiscovered() || asset.IsComputed())
                {
                    asset.SourceType = StaticWebAsset.SourceTypes.Project;
                }

                // When we look at the asset mode, this can take three values: All, CurrentProject, Reference.
                // In conjunction with the manifest mode (Default, Root, Isolated), it determines what we need to do with the
                // asset.
                // In default mode, assets with a CurrentProject mode, are not considered when being consumed from a referencing
                // project. Only assets with Reference or All modes are considered.
                // In root mode, the project specifies that it wants to be treated as the "transitive" closure root when it comes
                // to the assets it handles. In this case, the behavior is reversed and assets with CurrentProject mode are considered
                // when consumed from a reference, while assets with Reference mode are ignored. A good example for this functionality
                // is Blazor Webassembly and CSS isolation. We produce two bundles at compile time, one for the project and one for the
                // "transitive closure". On the hosting server, we want to reference the Wasm.styles.css bundle and not the project bundle
                // or some additional bundle file we've created on the server.
                // The selfcontained mode is only used at the top level project and its meant to signify that the consuming project doesn't
                // necessarily understands/participates from static web assets and as a result, several things need to happen:
                // * Static web assets are converted to regular publish items during the publish process as part of GetCopyToPublishDirectoryItems.
                // * Assets from the current project are considered to be at the root and properties like the BasePath are ignored.
                // None of the two aspects above, affects how we compute assets here.
                if (!asset.IsForCurrentAndReferencedProjects())
                {
                    switch (manifest.Mode)
                    {
                        case StaticWebAssetsManifest.ManifestModes.Default:
                            if (asset.IsForCurrentProjectOnly())
                            {
                                continue;
                            }
                            break;
                        case StaticWebAssetsManifest.ManifestModes.Root:
                            if (asset.IsForReferencedProjectsOnly())
                            {
                                continue;
                            }
                            break;
                        default:
                            break;
                    }
                }

                if (StaticWebAssetsManifest.ManifestTypes.IsPublish(AssetKind) && asset.IsBuildOnly())
                {
                    // If we are evaluating references for publish assets, we filter down assets that are build only.
                    // Build manifests are allowed to contain publish assets to make it easy for apps to declare them
                    // statically on the project (via CopyToOutputDirectory and CopyToPublishDirectory), however on publish
                    // manifests we want to avoid having the build only assets listed.
                    continue;
                }

                if (staticWebAssets.TryGetValue((asset.AssetKind, asset.Identity), out var defined))
                {
                    if (!asset.Equals(defined))
                    {
                        Log.LogError(
                            "Found conflicting definitions for the same asset '{0}' and '{1}'",
                            asset.ToString(),
                            defined.ToString());
                        break;
                    }
                    else
                    {
                        // There's already an asset with a compatible definition, so continue.
                        continue;
                    }
                }

                if (existingStaticWebAssets.TryGetValue((asset.AssetKind, asset.Identity), out var existing))
                {
                    if (!asset.Equals(existing))
                    {
                        Log.LogError(
                            "Found conflicting definitions for the same asset '{0}' and '{1}'",
                            asset.ToString(),
                            existing.ToString());
                        break;
                    }
                    else
                    {
                        // There's already an asset with a compatible definition, so continue.
                        continue;
                    }
                }

                staticWebAssets.Add((asset.AssetKind, asset.Identity), asset);
            }
        }

        private IEnumerable<StaticWebAsset> FindExistingAssetsWithIdentity(Dictionary<string, Dictionary<string, StaticWebAsset>> existingStaticWebAssets, StaticWebAsset asset)
        {
            foreach (var assetGroup in existingStaticWebAssets)
            {
                var kind = assetGroup.Key;
                var group = assetGroup.Value;
                if (group.TryGetValue(asset.Identity, out var existing))
                {
                    yield return existing;
                }
            }
        }

        private void MergeDiscoveryPatterns(Dictionary<string, StaticWebAssetsManifest.DiscoveryPattern> discoveryPatterns, StaticWebAssetsManifest manifest)
        {
            foreach (var pattern in manifest.DiscoveryPatterns)
            {
                if (discoveryPatterns.TryGetValue(pattern.Name, out var existingPattern))
                {
                    if (!pattern.Equals(existingPattern))
                    {
                        Log.LogError(
                            "Found conflicting definitions for the same asset '{0}' and '{1}'",
                            pattern.ToString(),
                            existingPattern.ToString());

                        break;
                    }
                    else
                    {
                        // The pattern is already defined
                        continue;
                    }
                }

                discoveryPatterns.Add(pattern.Name, pattern);
            }
        }
    }
}
