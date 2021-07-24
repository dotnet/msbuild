// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.Razor.Tasks
{
    public class GenerateStaticWebAssetsDevelopmentManifest : Task
    {
        // Since the manifest is only used at development time, it's ok for it to use the relaxed
        // json escaping (which is also what MVC uses by default) and to produce indented output
        // since that makes it easier to inspect the manifest when necessary.
        private static readonly JsonSerializerOptions ManifestSerializationOptions = new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true
        };

        [Required]
        public string BasePath { get; set; }

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
                var manifest = ComputeDevelopmentManifest(
                    Assets.Select(a => StaticWebAsset.FromTaskItem(a)),
                    DiscoveryPatterns.Select(ComputeDiscoveryPattern));

                PersistManifest(manifest);
            }
            catch (Exception ex)
            {
                Log.LogError(ex.ToString());
                Log.LogErrorFromException(ex);
            }
            return !Log.HasLoggedErrors;
        }

        public StaticWebAssetsDevelopmentManifest ComputeDevelopmentManifest(
            IEnumerable<StaticWebAsset> assets,
            IEnumerable<StaticWebAssetsManifest.DiscoveryPattern> discoveryPatterns)
        {
            var assetsWithPathSegments = assets
                .GroupBy(
                a => a.ComputeTargetPath("", '/'),
                ChooseAsset)
                .Where(pair => pair.Item2 != null)
                .ToArray();

            var discoveryPatternsByBasePath = DiscoveryPatterns
                .Select(ComputeDiscoveryPattern)
                .GroupBy(p => p.BasePath == BasePath ? "" : p.BasePath,
                 (key, values) => (key.Split(new[] { '/' }, options: StringSplitOptions.RemoveEmptyEntries), values));

            var manifest = CreateManifest(assetsWithPathSegments, discoveryPatternsByBasePath);
            return manifest;
        }

        private void PersistManifest(StaticWebAssetsDevelopmentManifest manifest)
        {
            var data = JsonSerializer.SerializeToUtf8Bytes(manifest, ManifestSerializationOptions);
            using var sha256 = SHA256.Create();
            var currentHash = sha256.ComputeHash(data);

            var fileExists = File.Exists(ManifestPath);
            var existingManifestHash = fileExists ?  sha256.ComputeHash(File.ReadAllBytes(ManifestPath)) : Array.Empty<byte>();

            if (!fileExists)
            {
                Log.LogMessage($"Creating manifest because manifest file '{ManifestPath}' does not exist.");
                File.WriteAllBytes(ManifestPath, data);
            }
            else if (!currentHash.SequenceEqual(existingManifestHash))
            {
                Log.LogMessage($"Updating manifest because manifest version '{Convert.ToBase64String(currentHash)}' is different from existing manifest hash '{Convert.ToBase64String(existingManifestHash)}'.");
                File.WriteAllBytes(ManifestPath, data);
            }
            else
            {
                Log.LogMessage($"Skipping manifest updated because manifest version '{Convert.ToBase64String(currentHash)}' has not changed.");
            }
        }

        private StaticWebAssetsDevelopmentManifest CreateManifest(
            (string[], StaticWebAsset) [] assetsWithPathSegments,
            IEnumerable<(string[], IEnumerable<StaticWebAssetsManifest.DiscoveryPattern> values)> discoveryPatternsByBasePath)
        {
            var contentRootIndex = new Dictionary<string, int>();
            var root = new StaticWebAssetNode() { };
            foreach (var (segments, asset) in assetsWithPathSegments)
            {
                var currentNode = root;
                for (var i = 0; i < segments.Length; i++)
                {
                    var segment = segments[i];
                    if (segments.Length -1 == i)
                    {
                        if (!contentRootIndex.TryGetValue(asset.ContentRoot, out var index))
                        {
                            index = contentRootIndex.Count;
                            contentRootIndex.Add(asset.ContentRoot, contentRootIndex.Count);
                        }
                        var matchingAsset = new StaticWebAssetMatch
                        {
                            SubPath = asset.Identity.StartsWith(asset.ContentRoot) ?
                                asset.Identity.Substring(asset.ContentRoot.Length) :
                                asset.RelativePath,
                            ContentRootIndex = index
                        };
                        currentNode.Children ??= new Dictionary<string, StaticWebAssetNode>();
                        currentNode.Children.Add(segment, new StaticWebAssetNode
                        {
                            Asset = matchingAsset
                        });
                        break;
                    }
                    else
                    {
                        currentNode.Children ??= new Dictionary<string,StaticWebAssetNode>();
                        if (currentNode.Children.TryGetValue(segment, out var existing))
                        {
                            currentNode = existing;
                        }
                        else
                        {
                            var newNode = new StaticWebAssetNode
                            {
                                Children = new Dictionary<string, StaticWebAssetNode>()
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
                            currentNode.Children ??= new Dictionary<string, StaticWebAssetNode>();
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
                            currentNode.Children ??= new Dictionary<string, StaticWebAssetNode>();
                            if (currentNode.Children.TryGetValue(segment, out var existing))
                            {
                                currentNode = existing;
                            }
                            else
                            {
                                var newNode = new StaticWebAssetNode
                                {
                                    Children = new Dictionary<string, StaticWebAssetNode>()
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
            public IDictionary<string, StaticWebAssetNode> Children { get; set; }
            public StaticWebAssetMatch Asset { get; set; }
            public StaticWebAssetPattern[] Patterns { get; set; }
        }

        // We at most get three assets here since we assume a valid manifest (Build, All, Publish)
        // We will ignore publish assets since they don't matter for development and we will prefer
        // build specific assets over 'All' (which means it can be used for build and publish).
        // We've already validated that assets targeting the same path come from the same project, so
        // we don't need to worry about that here.
        private static (string[], StaticWebAsset) ChooseAsset(string key, IEnumerable<StaticWebAsset> candidates)
        {
            StaticWebAsset buildSpecificAsset = null;
            StaticWebAsset buildAndPublishAsset = null;
            foreach (var candidate in candidates)
            {
                // Todo, perform filtering based on project mode.

                if (candidate.IsBuildOnly())
                {
                    buildSpecificAsset = candidate;
                }
                if (candidate.IsBuildAndPublish())
                {
                    buildSpecificAsset = candidate;
                }
            }

            return (key.Split(new[] { '/' }, options: StringSplitOptions.RemoveEmptyEntries), buildSpecificAsset ?? buildAndPublishAsset);
        }

        private StaticWebAssetsManifest.DiscoveryPattern ComputeDiscoveryPattern(ITaskItem pattern)
        {
            var name = pattern.ItemSpec;
            var contentRoot = pattern.GetMetadata(nameof(StaticWebAssetsManifest.DiscoveryPattern.ContentRoot));
            var basePath = pattern.GetMetadata(nameof(StaticWebAssetsManifest.DiscoveryPattern.BasePath));
            var glob = pattern.GetMetadata(nameof(StaticWebAssetsManifest.DiscoveryPattern.Pattern));

            return StaticWebAssetsManifest.DiscoveryPattern.Create(name, contentRoot, basePath, glob);
        }

        private StaticWebAssetsManifest.ManifestReference ComputeManifestReference(ITaskItem reference)
        {
            var identity = reference.GetMetadata("FullPath");
            var source = reference.GetMetadata(nameof(StaticWebAssetsManifest.ManifestReference.Source));
            var manifestType = reference.GetMetadata(nameof(StaticWebAssetsManifest.ManifestReference.ManifestType));
            var projectFile = reference.GetMetadata(nameof(StaticWebAssetsManifest.ManifestReference.ProjectFile));
            var publishTarget = reference.GetMetadata(nameof(StaticWebAssetsManifest.ManifestReference.PublishTarget));
            var additionalPublishProperties = reference.GetMetadata(nameof(StaticWebAssetsManifest.ManifestReference.AdditionalPublishProperties));
            var additionalPublishPropertiesToRemove = reference.GetMetadata(nameof(StaticWebAssetsManifest.ManifestReference.AdditionalPublishPropertiesToRemove));

            if (!File.Exists(identity))
            {
                if (!StaticWebAssetsManifest.ManifestTypes.IsPublish(manifestType))
                {
                    Log.LogError("Manifest '{0}' for project '{1}' with type '{2}' does not exist.", identity, source, manifestType);
                    return null;
                }

                var publishManifest = StaticWebAssetsManifest.ManifestReference.Create(identity, source, manifestType, projectFile, "");
                publishManifest.PublishTarget = publishTarget;
                publishManifest.AdditionalPublishProperties = additionalPublishProperties;
                publishManifest.AdditionalPublishPropertiesToRemove = additionalPublishPropertiesToRemove;

                return publishManifest;
            }

            var relatedManifest = StaticWebAssetsManifest.FromJsonBytes(File.ReadAllBytes(identity));

            var result = StaticWebAssetsManifest.ManifestReference.Create(identity, source, manifestType, projectFile, relatedManifest.Hash);
            result.PublishTarget = publishTarget;
            result.AdditionalPublishProperties = additionalPublishProperties;
            result.AdditionalPublishPropertiesToRemove = additionalPublishPropertiesToRemove;

            return result;
        }
    }
}
