// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

public class ResolveCompressedAssets : Task
{
    private const string GzipAssetTraitValue = "gzip";
    private const string BrotliAssetTraitValue = "br";

    private const string GzipFormatName = "gzip";
    private const string BrotliFormatName = "brotli";

    public ITaskItem[] CandidateAssets { get; set; }

    public string Formats { get; set; }

    public string IncludePatterns { get; set; }

    public string ExcludePatterns { get; set; }

    public ITaskItem[] ExplicitAssets { get; set; }

    [Required]
    public string OutputPath { get; set; }

    [Output]
    public ITaskItem[] AssetsToCompress { get; set; }

    public override bool Execute()
    {
        if (CandidateAssets is null)
        {
            Log.LogMessage(
                MessageImportance.Low,
                "Skipping task '{0}' because no candidate assets for compression were specified.",
                nameof(ResolveCompressedAssets));
            return true;
        }

        if (string.IsNullOrEmpty(Formats))
        {
            Log.LogMessage(
                MessageImportance.Low,
                "Skipping task '{0}' because no compression formats were specified.",
                nameof(ResolveCompressedAssets));
            return true;
        }

        // Scan the provided candidate assets and determine which ones have already been detected for compression and in which formats.
        var existingCompressionFormatsByAssetItemSpec = new Dictionary<string, HashSet<string>>();
        foreach (var asset in CandidateAssets)
        {
            if (IsCompressedAsset(asset))
            {
                var relatedAssetItemSpec = asset.GetMetadata("RelatedAsset");

                if (string.IsNullOrEmpty(relatedAssetItemSpec))
                {
                    Log.LogError(
                        "The asset '{0}' was detected as compressed but didn't specify a related asset.",
                        asset.ItemSpec);
                    continue;
                }

                if (!existingCompressionFormatsByAssetItemSpec.TryGetValue(relatedAssetItemSpec, out var existingFormats))
                {
                    existingFormats = new();
                    existingCompressionFormatsByAssetItemSpec.Add(relatedAssetItemSpec, existingFormats);
                }

                var assetTraitValue = asset.GetMetadata("AssetTraitValue");
                string assetFormat;

                if (string.Equals(assetTraitValue, GzipAssetTraitValue, StringComparison.OrdinalIgnoreCase))
                {
                    assetFormat = GzipFormatName;
                }
                else if (string.Equals(assetTraitValue, BrotliAssetTraitValue, StringComparison.OrdinalIgnoreCase))
                {
                    assetFormat = BrotliFormatName;
                }
                else
                {
                    Log.LogError(
                        "The asset '{0}' has an unknown compression format '{1}'.",
                        asset.ItemSpec,
                        assetTraitValue);
                    continue;
                }

                Log.LogMessage(
                    "The asset '{0}' with related asset '{1}' was detected as already compressed with format '{2}'.",
                    asset.ItemSpec,
                    relatedAssetItemSpec,
                    assetFormat);
                existingFormats.Add(assetFormat);
            }
        }

        var includePatterns = SplitPattern(IncludePatterns);
        var excludePatterns = SplitPattern(ExcludePatterns);

        var matcher = new Matcher();
        matcher.AddIncludePatterns(includePatterns);
        matcher.AddExcludePatterns(excludePatterns);

        var matchingCandidateAssets = new List<ITaskItem>();

        // Add each candidate asset to each compression configuration with a matching pattern.
        foreach (var asset in CandidateAssets)
        {
            if (IsCompressedAsset(asset))
            {
                Log.LogMessage(
                    MessageImportance.Low,
                    "Ignoring asset '{0}' for compression because it is already compressed.",
                    asset.ItemSpec);
                continue;
            }

            var relativePath = asset.GetMetadata("RelativePath");
            var match = matcher.Match(relativePath);

            if (!match.HasMatches)
            {
                Log.LogMessage(
                    MessageImportance.Low,
                    "Asset '{0}' with relative path '{1}' did not match include pattern '{2}' or matched exclude pattern '{3}'.",
                    asset.ItemSpec,
                    relativePath,
                    IncludePatterns,
                    ExcludePatterns);
                continue;
            }

            Log.LogMessage(
                MessageImportance.Low,
                "Asset '{0}' with relative path '{1}' matched include pattern '{2}' and did not match exclude pattern '{3}'.",
                asset.ItemSpec,
                relativePath,
                IncludePatterns,
                ExcludePatterns);
            matchingCandidateAssets.Add(asset);
        }

        // Consider each explicitly-provided asset to be a matching asset.
        if (ExplicitAssets is not null)
        {
            matchingCandidateAssets.AddRange(ExplicitAssets);
        }

        // Process the final set of candidate assets, deduplicating assets to be compressed in the same format multiple times and
        // generating new a static web asset definition for each compressed item.
        var formats = SplitPattern(Formats);
        var assetsToCompress = new List<ITaskItem>();
        foreach (var format in formats)
        {
            foreach (var asset in matchingCandidateAssets)
            {
                var itemSpec = asset.ItemSpec;
                if (!existingCompressionFormatsByAssetItemSpec.TryGetValue(itemSpec, out var existingFormats))
                {
                    existingFormats = new();
                    existingCompressionFormatsByAssetItemSpec.Add(itemSpec, existingFormats);
                }

                if (existingFormats.Contains(format))
                {
                    Log.LogMessage(
                        "Ignoring asset '{0}' because it was already resolved with format '{1}'.",
                        itemSpec,
                        format);
                    continue;
                }

                if (TryCreateCompressedAsset(asset, format, out var compressedAsset))
                {
                    assetsToCompress.Add(compressedAsset);
                    existingFormats.Add(format);

                    Log.LogMessage(
                        "Created compressed asset '{0}' for '{1}'.",
                        compressedAsset.ItemSpec,
                        itemSpec);
                }
                else
                {
                    Log.LogError(
                        "Could not create compressed asset for original asset '{0}'.",
                        itemSpec);
                }
            }
        }

        AssetsToCompress = assetsToCompress.ToArray();

        return !Log.HasLoggedErrors;
    }

    private static bool IsCompressedAsset(ITaskItem asset)
        => string.Equals("Content-Encoding", asset.GetMetadata("AssetTraitName"));

    private static string[] SplitPattern(string pattern)
        => string.IsNullOrEmpty(pattern) ? Array.Empty<string>() : pattern
            .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .ToArray();

    private bool TryCreateCompressedAsset(ITaskItem asset, string format, out TaskItem result)
    {
        result = null;

        string fileExtension;
        string assetTraitValue;

        if (string.Equals(GzipFormatName, format, StringComparison.OrdinalIgnoreCase))
        {
            fileExtension = ".gz";
            assetTraitValue = GzipAssetTraitValue;
        }
        else if (string.Equals(BrotliFormatName, format, StringComparison.OrdinalIgnoreCase))
        {
            fileExtension = ".br";
            assetTraitValue = BrotliAssetTraitValue;
        }
        else
        {
            Log.LogError(
                "Unknown compression format '{0}' for '{1}'.",
                format,
                asset.ItemSpec);
            return false;
        }

        var originalItemSpec = asset.GetMetadata("OriginalItemSpec");
        var relativePath = asset.GetMetadata("RelativePath");

        var fileName = FileHasher.GetFileHash(originalItemSpec) + fileExtension;
        var outputRelativePath = Path.Combine(OutputPath, fileName);

        result = new TaskItem(outputRelativePath, asset.CloneCustomMetadata());

        result.SetMetadata("RelativePath", relativePath + fileExtension);
        result.SetMetadata("OriginalItemSpec", asset.ItemSpec);
        result.SetMetadata("RelatedAsset", asset.ItemSpec);
        result.SetMetadata("RelatedAssetOriginalItemSpec", originalItemSpec);
        result.SetMetadata("AssetRole", "Alternative");
        result.SetMetadata("AssetTraitName", "Content-Encoding");
        result.SetMetadata("AssetTraitValue", assetTraitValue);

        return true;
    }
}
