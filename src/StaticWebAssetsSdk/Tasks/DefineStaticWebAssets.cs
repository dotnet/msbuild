// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks
{
    // Define static web asset builds a normalized static web asset out of a set of candidate assets.
    // The BaseAssets might already define all the properties the assets care about and in that case,
    // those properties are preserved and reused. If some other properties are not present, default
    // values are used when possible. Otherwise, the properties need to be specified explicitly on the task or
    // else an error is generated. The property overrides allows the caller to override the existing value for a property
    // with the provided value.
    // There is an asset pattern filter that can be used to apply the pattern to a subset of the candidate assets
    // which allows for applying a different set of values to a subset of the candidates without having to previously
    // filter them. The asset pattern filter is applied after we've determined the RelativePath for the candidate asset.
    // There is also a RelativePathPattern that is used to automatically transform the relative path of the candidates to match
    // the expected path of the final asset. This is typically use to remove a common path prefix, like `wwwroot` from the target
    // path of the assets and so on.
    public class DefineStaticWebAssets : Task
    {
        [Required]
        public ITaskItem[] CandidateAssets { get; set; }

        public ITaskItem[] PropertyOverrides { get; set; }

        public string SourceId { get; set; }

        public string SourceType { get; set; }

        public string BasePath { get; set; }

        public string ContentRoot { get; set; }

        public string RelativePathPattern { get; set; }

        public string RelativePathFilter { get; set; }

        public string AssetKind { get; set; } = StaticWebAsset.AssetKinds.All;

        public string AssetMode { get; set; } = StaticWebAsset.AssetModes.All;

        public string AssetRole { get; set; } = StaticWebAsset.AssetRoles.Primary;

        public string AssetMergeSource { get; set; } = "";

        public string AssetMergeBehavior { get; set; } = StaticWebAsset.MergeBehaviors.None;

        public string RelatedAsset { get; set; }

        public string AssetTraitName { get; set; }

        public string AssetTraitValue { get; set; }

        public string CopyToOutputDirectory { get; set; } = StaticWebAsset.AssetCopyOptions.Never;

        public string CopyToPublishDirectory { get; set; } = StaticWebAsset.AssetCopyOptions.PreserveNewest;

        [Output]
        public ITaskItem[] Assets { get; set; }

        [Output]
        public ITaskItem[] CopyCandidates { get; set; }

        public override bool Execute()
        {
            try
            {
                var results = new List<ITaskItem>();
                var copyCandidates = new List<ITaskItem>();

                var matcher = !string.IsNullOrEmpty(RelativePathPattern) ? new Matcher().AddInclude(RelativePathPattern) : null;
                var filter = !string.IsNullOrEmpty(RelativePathFilter) ? new Matcher().AddInclude(RelativePathFilter) : null;
                for (var i = 0; i < CandidateAssets.Length; i++)
                {
                    var candidate = CandidateAssets[i];
                    var relativePathCandidate = GetCandidateMatchPath(candidate);
                    if (matcher != null)
                    {
                        var match = matcher.Match(relativePathCandidate);
                        if (match.HasMatches)
                        {
                            var newRelativePathCandidate = match.Files.Single().Stem;
                            Log.LogMessage(
                                MessageImportance.Low,
                                "The relative path '{0}' matched the pattern '{1}'. Replacing relative path with '{2}'.",
                                relativePathCandidate,
                                RelativePathPattern,
                                newRelativePathCandidate);

                            relativePathCandidate = newRelativePathCandidate;
                        }
                    }

                    if (filter != null && !filter.Match(relativePathCandidate).HasMatches)
                    {
                        Log.LogMessage(
                            MessageImportance.Low,
                            "Skipping '{0}' because the relative path '{1}' did not match the filter '{2}'.",
                            candidate.ItemSpec,
                            relativePathCandidate,
                            RelativePathFilter);

                        continue;
                    }

                    var sourceId = ComputePropertyValue(candidate, nameof(StaticWebAsset.SourceId), SourceId);
                    var sourceType = ComputePropertyValue(candidate, nameof(StaticWebAsset.SourceType), SourceType);
                    var basePath = ComputePropertyValue(candidate, nameof(StaticWebAsset.BasePath), BasePath);
                    var contentRoot = ComputePropertyValue(candidate, nameof(StaticWebAsset.ContentRoot), ContentRoot);
                    var assetKind = ComputePropertyValue(candidate, nameof(StaticWebAsset.AssetKind), AssetKind, isRequired: false);
                    var assetMode = ComputePropertyValue(candidate, nameof(StaticWebAsset.AssetMode), AssetMode);
                    var assetRole = ComputePropertyValue(candidate, nameof(StaticWebAsset.AssetRole), AssetRole);
                    var assetMergeSource = ComputePropertyValue(candidate, nameof(StaticWebAsset.AssetMergeSource), AssetMergeSource);
                    var relatedAsset = ComputePropertyValue(candidate, nameof(StaticWebAsset.RelatedAsset), RelatedAsset, !StaticWebAsset.AssetRoles.IsPrimary(assetRole));
                    var assetTraitName = ComputePropertyValue(candidate, nameof(StaticWebAsset.AssetTraitName), AssetTraitName, !StaticWebAsset.AssetRoles.IsPrimary(assetRole));
                    var assetTraitValue = ComputePropertyValue(candidate, nameof(StaticWebAsset.AssetTraitValue), AssetTraitValue, !StaticWebAsset.AssetRoles.IsPrimary(assetRole));
                    var copyToOutputDirectory = ComputePropertyValue(candidate, nameof(StaticWebAsset.CopyToOutputDirectory), CopyToOutputDirectory);
                    var copyToPublishDirectory = ComputePropertyValue(candidate, nameof(StaticWebAsset.CopyToPublishDirectory), CopyToPublishDirectory);
                    var originalItemSpec = ComputePropertyValue(
                        candidate,
                        nameof(StaticWebAsset.OriginalItemSpec),
                        PropertyOverrides == null || PropertyOverrides.Length == 0 ? candidate.ItemSpec : candidate.GetMetadata("OriginalItemSpec"));

                    // If we are not able to compute the value based on an existing value or a default, we produce an error and stop.
                    if (Log.HasLoggedErrors)
                    {
                        break;
                    }

                    // We ignore the content root for publish only assets since it doesn't matter.
                    var contentRootPrefix = StaticWebAsset.AssetKinds.IsPublish(assetKind) ? null : contentRoot;
                    var (identity, computed) = ComputeCandidateIdentity(candidate, contentRootPrefix, relativePathCandidate, matcher);

                    if (computed)
                    {
                        copyCandidates.Add(new TaskItem(candidate.ItemSpec, new Dictionary<string, string>
                        {
                            ["TargetPath"] = identity
                        }));
                    }

                    var asset = StaticWebAsset.FromProperties(
                        identity,
                        sourceId,
                        sourceType,
                        basePath,
                        relativePathCandidate,
                        contentRoot,
                        assetKind,
                        assetMode,
                        assetRole,
                        assetMergeSource,
                        relatedAsset,
                        assetTraitName,
                        assetTraitValue,
                        copyToOutputDirectory,
                        copyToPublishDirectory,
                        originalItemSpec);

                    var item = asset.ToTaskItem();

                    results.Add(item);
                }

                Assets = results.ToArray();
                CopyCandidates = copyCandidates.ToArray();
            }
            catch (Exception ex)
            {
                Log.LogError(ex.ToString());
            }

            return !Log.HasLoggedErrors;
        }

        private (string identity, bool computed) ComputeCandidateIdentity(ITaskItem candidate, string contentRoot, string relativePath, Matcher matcher)
        {
            var candidateFullPath = Path.GetFullPath(candidate.GetMetadata("FullPath"));
            if (contentRoot == null)
            {
                Log.LogMessage(MessageImportance.Low, "Identity for candidate '{0}' is '{1}' because content root is not defined.", candidate.ItemSpec, candidateFullPath);
                return (candidateFullPath, false);
            }

            var normalizedContentRoot = StaticWebAsset.NormalizeContentRootPath(contentRoot);
            if (candidateFullPath.StartsWith(normalizedContentRoot))
            {
                Log.LogMessage(MessageImportance.Low, "Identity for candidate '{0}' is '{1}' because it starts with content root '{2}'.", candidate.ItemSpec, candidateFullPath, normalizedContentRoot);
                return (candidateFullPath, false);
            }
            else
            {
                // We want to support assets that are part of the source codebase but that might get transformed during the build or
                // publish processes, so we want to allow defining these assets by setting up a different content root path from their
                // original location in the project. For example the asset can be wwwroot\my-prod-asset.js, the content root can be
                // obj\transform and the final asset identity can be <<FullPathTo>>\obj\transform\my-prod-asset.js
                var matchResult = matcher?.Match(candidate.ItemSpec);
                if (matcher == null)
                {
                    // If no relative path pattern was specified, we are going to suggest that the identity is `%(ContentRoot)\RelativePath\OriginalFileName`
                    // We don't want to use the relative path file name since multiple assets might map to that and conflicts might arise.
                    // Alternatively, we could be explicit here and support ContentRootSubPath to indicate where it needs to go.
                    var identitySubPath = Path.GetDirectoryName(relativePath);
                    var itemSpecFileName = Path.GetFileName(candidateFullPath);
                    var finalIdentity = Path.Combine(normalizedContentRoot, identitySubPath, itemSpecFileName);
                    Log.LogMessage(MessageImportance.Low, "Identity for candidate '{0}' is '{1}' because it did not start with the content root '{2}'", candidate.ItemSpec, finalIdentity, normalizedContentRoot);
                    return (finalIdentity, true);
                }
                else if (!matchResult.HasMatches)
                {
                    Log.LogMessage(MessageImportance.Low, "Identity for candidate '{0}' is '{1}' because it didn't match the relative path pattern", candidate.ItemSpec, candidateFullPath);
                    return (candidateFullPath, false);
                }
                else
                {
                    var stem = matchResult.Files.Single().Stem;
                    var assetIdentity = Path.GetFullPath(Path.Combine(normalizedContentRoot, stem));
                    Log.LogMessage(MessageImportance.Low, "Computed identity '{0}' for candidate '{1}'", assetIdentity, candidate.ItemSpec);

                    return (assetIdentity, true);
                }
            }
        }

        private string ComputePropertyValue(ITaskItem element, string metadataName, string propertyValue, bool isRequired = true)
        {
            if (PropertyOverrides != null && PropertyOverrides.Any(a => string.Equals(a.ItemSpec, metadataName, StringComparison.OrdinalIgnoreCase)))
            {
                return propertyValue;
            }

            var value = element.GetMetadata(metadataName);
            if (string.IsNullOrEmpty(value))
            {
                if (propertyValue == null && isRequired)
                {
                    Log.LogError("No metadata '{0}' was present for item '{1}' and no default value was provided.",
                        metadataName,
                        element.ItemSpec);

                    return null;
                }
                else
                {
                    return propertyValue;
                }
            }
            else
            {
                return value;
            }
        }

        private string GetCandidateMatchPath(ITaskItem candidate)
        {
            var relativePath = candidate.GetMetadata("RelativePath");
            if (!string.IsNullOrEmpty(relativePath))
            {
                var normalizedPath = StaticWebAsset.Normalize(relativePath, allowEmpyPath: true);
                Log.LogMessage(MessageImportance.Low, "RelativePath '{0}' normalized to '{1}' found for candidate '{2}' and will be used for matching.", relativePath, normalizedPath, candidate.ItemSpec);
                return normalizedPath;
            }

            var targetPath = candidate.GetMetadata("TargetPath");
            if (!string.IsNullOrEmpty(targetPath))
            {
                var normalizedPath = StaticWebAsset.Normalize(targetPath, allowEmpyPath: true);
                Log.LogMessage(MessageImportance.Low, "TargetPath '{0}' normalized to '{1}' found for candidate '{2}' and will be used for matching.", targetPath, normalizedPath, candidate.ItemSpec);
                return normalizedPath;
            }

            var linkPath = candidate.GetMetadata("Link");
            if (!string.IsNullOrEmpty(linkPath))
            {
                var normalizedPath = StaticWebAsset.Normalize(linkPath, allowEmpyPath: true);
                Log.LogMessage(MessageImportance.Low, "Link '{0}'  normalized to '{1}' found for candidate '{2}' and will be used for matching.", linkPath, normalizedPath, candidate.ItemSpec);

                return linkPath;
            }

            var normalizedContentRoot = StaticWebAsset.NormalizeContentRootPath(string.IsNullOrEmpty(candidate.GetMetadata(nameof(StaticWebAsset.ContentRoot))) ?
                ContentRoot :
                candidate.GetMetadata(nameof(StaticWebAsset.ContentRoot)));

            var normalizedAssetPath = Path.GetFullPath(candidate.GetMetadata("FullPath"));
            if (normalizedAssetPath.StartsWith(normalizedContentRoot))
            {
                return normalizedAssetPath.Substring(normalizedContentRoot.Length);
            }
            else
            {
                return candidate.ItemSpec;
            }
        }
    }
}
