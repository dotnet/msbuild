// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Build.Framework;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks
{
    // The manifest needs to always be case sensitive, since we don't know what the final runtime environment
    // will be. The runtime is responsible for merging the tree nodes in the manifest when the underlying OS
    // is case insensitive.
    public class GenerateStaticWebAssetsDevelopmentManifest : Task
    {
        // Since the manifest is only used at development time, it's ok for it to use the relaxed
        // json escaping (which is also what MVC uses by default) and to produce indented output
        // since that makes it easier to inspect the manifest when necessary.
        private static readonly JsonSerializerOptions ManifestSerializationOptions = new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        [Required]
        public string Source { get; set; }

        [Required]
        public ITaskItem[] DiscoveryPatterns { get; set; }

        [Required]
        public ITaskItem[] Assets { get; set; }

        [Required]
        public string ManifestPath { get; set; }

        public override bool Execute()
        {
            try
            {
                if (Assets.Length == 0 && DiscoveryPatterns.Length == 0)
                {
                    Log.LogMessage(MessageImportance.Low, "Skipping manifest generation because no assets nor discovery patterns were found.");
                    return true;
                }

                var manifest = ComputeDevelopmentManifest(
                    Assets.Select(a => StaticWebAsset.FromTaskItem(a)),
                    DiscoveryPatterns.Select(StaticWebAssetsDiscoveryPattern.FromTaskItem));

                PersistManifest(manifest);
            }
            catch (Exception ex)
            {
                Log.LogErrorFromException(ex, showStackTrace: true, showDetail: true, file: null);
            }
            return !Log.HasLoggedErrors;
        }

        public StaticWebAssetsDevelopmentManifest ComputeDevelopmentManifest(
            IEnumerable<StaticWebAsset> assets,
            IEnumerable<StaticWebAssetsDiscoveryPattern> discoveryPatterns)
        {
            var assetsWithPathSegments = ComputeManifestAssets(assets).ToArray();

            var discoveryPatternsByBasePath = discoveryPatterns
                .GroupBy(p => p.HasSourceId(Source) ? "" : p.BasePath,
                 (key, values) => (key.Split(new[] { '/' }, options: StringSplitOptions.RemoveEmptyEntries), values));

            var manifest = CreateManifest(assetsWithPathSegments, discoveryPatternsByBasePath);
            return manifest;
        }

        private IEnumerable<SegmentsAssetPair> ComputeManifestAssets(IEnumerable<StaticWebAsset> assets)
        {
            var assetsByTargetPath = assets
                .GroupBy(a => a.ComputeTargetPath("", '/'));

            foreach (var group in assetsByTargetPath)
            {
                var asset = StaticWebAsset.ChooseNearestAssetKind(group, StaticWebAsset.AssetKinds.Build).SingleOrDefault();

                if (asset == null)
                {
                    Log.LogMessage(MessageImportance.Low, "Skipping candidate asset '{0}' because it is a 'Publish' asset.", group.Key);
                    continue;
                }

                if (asset.HasSourceId(Source) && !StaticWebAssetsManifest.ManifestModes.ShouldIncludeAssetInCurrentProject(asset, StaticWebAssetsManifest.ManifestModes.Root))
                {
                    Log.LogMessage(MessageImportance.Low, "Skipping candidate asset '{0}' because asset mode is '{1}'",
                        asset.Identity,
                        asset.AssetMode);

                    continue;
                }

                yield return new SegmentsAssetPair(group.Key, asset);
            }
        }

        private void PersistManifest(StaticWebAssetsDevelopmentManifest manifest)
        {
            var data = JsonSerializer.SerializeToUtf8Bytes(manifest, ManifestSerializationOptions);
            using var sha256 = SHA256.Create();
            var currentHash = sha256.ComputeHash(data);

            var fileExists = File.Exists(ManifestPath);
            var existingManifestHash = fileExists ? sha256.ComputeHash(File.ReadAllBytes(ManifestPath)) : Array.Empty<byte>();

            if (!fileExists)
            {
                Log.LogMessage(MessageImportance.Low, "Creating manifest because manifest file '{0}' does not exist.", ManifestPath);
                File.WriteAllBytes(ManifestPath, data);
            }
            else if (!currentHash.SequenceEqual(existingManifestHash))
            {
                Log.LogMessage(
                    MessageImportance.Low,
                    "Updating manifest because manifest version '{0}' is different from existing manifest hash '{1}'.",
                    Convert.ToBase64String(currentHash),
                    Convert.ToBase64String(existingManifestHash));
                File.WriteAllBytes(ManifestPath, data);
            }
            else
            {
                Log.LogMessage(
                    MessageImportance.Low,
                    "Skipping manifest update because manifest version '{0}' has not changed.",
                    Convert.ToBase64String(currentHash));
            }
        }

        private StaticWebAssetsDevelopmentManifest CreateManifest(
            SegmentsAssetPair[] assetsWithPathSegments,
            IEnumerable<(string[], IEnumerable<StaticWebAssetsDiscoveryPattern> values)> discoveryPatternsByBasePath)
        {
            var contentRootIndex = new Dictionary<string, int>();
            var root = new StaticWebAssetNode() { };
            foreach (var (segments, asset) in assetsWithPathSegments)
            {
                var currentNode = root;
                for (var i = 0; i < segments.Length; i++)
                {
                    var segment = segments[i];
                    if (segments.Length - 1 == i)
                    {
                        if (!contentRootIndex.TryGetValue(asset.ContentRoot, out var index))
                        {
                            index = contentRootIndex.Count;
                            contentRootIndex.Add(asset.ContentRoot, contentRootIndex.Count);
                        }
                        var matchingAsset = new StaticWebAssetMatch
                        {
                            SubPath = asset.Identity.StartsWith(asset.ContentRoot) ?
                                StaticWebAsset.Normalize(asset.Identity.Substring(asset.ContentRoot.Length)) :
                                asset.RelativePath,
                            ContentRootIndex = index
                        };
                        currentNode.Children ??= new Dictionary<string, StaticWebAssetNode>(StringComparer.Ordinal);
                        currentNode.Children.Add(segment, new StaticWebAssetNode
                        {
                            Asset = matchingAsset
                        });
                        break;
                    }
                    else
                    {
                        currentNode.Children ??= new Dictionary<string, StaticWebAssetNode>(StringComparer.Ordinal);
                        if (currentNode.Children.TryGetValue(segment, out var existing))
                        {
                            currentNode = existing;
                        }
                        else
                        {
                            var newNode = new StaticWebAssetNode
                            {
                                Children = new Dictionary<string, StaticWebAssetNode>(StringComparer.Ordinal)
                            };
                            currentNode.Children.Add(segment, newNode);
                            currentNode = newNode;
                        }
                    }
                }
            }

            foreach (var (segments, patternGroup) in discoveryPatternsByBasePath)
            {
                var currentNode = root;
                if (segments.Length == 0)
                {
                    var patterns = new List<StaticWebAssetPattern>();
                    foreach (var pattern in patternGroup)
                    {
                        if (!contentRootIndex.TryGetValue(pattern.ContentRoot, out var index))
                        {
                            index = contentRootIndex.Count;
                            contentRootIndex.Add(pattern.ContentRoot, contentRootIndex.Count);
                        }
                        var assetPattern = new StaticWebAssetPattern
                        {
                            Pattern = pattern.Pattern,
                            ContentRootIndex = index
                        };
                        patterns.Add(assetPattern);
                    }
                    currentNode.Patterns = patterns.ToArray();
                }
                else
                {
                    for (var i = 0; i < segments.Length; i++)
                    {
                        var segment = segments[i];
                        if (segments.Length - 1 == i)
                        {
                            var patterns = new List<StaticWebAssetPattern>();
                            foreach (var pattern in patternGroup)
                            {
                                if (!contentRootIndex.TryGetValue(pattern.ContentRoot, out var index))
                                {
                                    index = contentRootIndex.Count;
                                    contentRootIndex.Add(pattern.ContentRoot, contentRootIndex.Count);
                                }
                                var matchingPattern = new StaticWebAssetPattern
                                {
                                    ContentRootIndex = index,
                                    Pattern = pattern.Pattern,
                                    Depth = segments.Length
                                };

                                patterns.Add(matchingPattern);
                            }
                            currentNode.Children ??= new Dictionary<string, StaticWebAssetNode>(StringComparer.Ordinal);
                            if (!currentNode.Children.TryGetValue(segment, out var childNode))
                            {
                                childNode = new StaticWebAssetNode
                                {
                                    Patterns = patterns.ToArray(),
                                };
                                currentNode.Children.Add(segment, childNode);
                            }
                            else
                            {
                                childNode.Patterns = patterns.ToArray();
                            }

                            break;
                        }
                        else
                        {
                            currentNode.Children ??= new Dictionary<string, StaticWebAssetNode>(StringComparer.Ordinal);
                            if (currentNode.Children.TryGetValue(segment, out var existing))
                            {
                                currentNode = existing;
                            }
                            else
                            {
                                var newNode = new StaticWebAssetNode
                                {
                                    Children = new Dictionary<string, StaticWebAssetNode>(StringComparer.Ordinal)
                                };
                                currentNode.Children.Add(segment, newNode);
                                currentNode = newNode;
                            }
                        }
                    }
                }
            }

            return new StaticWebAssetsDevelopmentManifest
            {
                ContentRoots = contentRootIndex.OrderBy(kvp => kvp.Value).Select(kvp => kvp.Key).ToArray(),
                Root = root
            };
        }

        public class StaticWebAssetsDevelopmentManifest
        {
            public string[] ContentRoots { get; set; }

            public StaticWebAssetNode Root { get; set; }
        }

        public class StaticWebAssetPattern
        {
            public int ContentRootIndex { get; set; }
            public string Pattern { get; set; }
            public int Depth { get; set; }
        }

        public class StaticWebAssetMatch
        {
            public int ContentRootIndex { get; set; }
            public string SubPath { get; set; }
        }

        public class StaticWebAssetNode
        {
            public Dictionary<string, StaticWebAssetNode> Children { get; set; }
            public StaticWebAssetMatch Asset { get; set; }
            public StaticWebAssetPattern[] Patterns { get; set; }
        }

        private struct SegmentsAssetPair
        {
            public SegmentsAssetPair(string path, StaticWebAsset asset)
            {
                PathSegments = path.Split(new[] { '/' }, options: StringSplitOptions.RemoveEmptyEntries);
                Asset = asset;
            }

            public string[] PathSegments { get; }

            public StaticWebAsset Asset { get; }

            public void Deconstruct(out string[] segments, out StaticWebAsset asset)
            {
                asset = Asset;
                segments = PathSegments;
            }
        }
    }
}
