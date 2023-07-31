// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks
{
    public class CollectStaticWebAssetsToCopy : Task
    {
        [Required]
        public ITaskItem[] Assets { get; set; }

        [Required]
        public string OutputPath { get; set; }

        [Output]
        public ITaskItem[] AssetsToCopy { get; set; }

        public override bool Execute()
        {
            var copyToOutputFolder = new List<ITaskItem>();
            var normalizedOutputPath = StaticWebAsset.NormalizeContentRootPath(Path.GetFullPath(OutputPath));
            try
            {
                foreach (var asset in Assets.Select(StaticWebAsset.FromTaskItem))
                {
                    string fileOutputPath = null;
                    if (!(asset.IsDiscovered() || asset.IsComputed()))
                    {
                        Log.LogMessage(MessageImportance.Low, "Skipping asset '{0}' since source type is '{1}'", asset.Identity, asset.SourceType);
                        continue;
                    }

                    if (asset.IsForReferencedProjectsOnly())
                    {
                        Log.LogMessage(MessageImportance.Low, "Skipping asset '{0}' since asset mode is '{1}'", asset.Identity, asset.AssetMode);
                    }

                    if (asset.ShouldCopyToOutputDirectory())
                    {
                        // We have an asset we want to copy to the output folder.
                        fileOutputPath = Path.Combine(normalizedOutputPath, asset.RelativePath);
                        string source = null;
                        if (asset.IsComputed())
                        {
                            if (asset.Identity.StartsWith(normalizedOutputPath, StringComparison.Ordinal))
                            {
                                Log.LogMessage(MessageImportance.Low, "Source for asset '{0}' is '{1}' since the identity points to the output path.", asset.Identity, asset.OriginalItemSpec);
                                source = asset.OriginalItemSpec;
                            }
                            else if (File.Exists(asset.Identity))
                            {
                                Log.LogMessage(MessageImportance.Low, "Source for asset '{0}' is '{0}' since the asset exists.", asset.Identity);
                                source = asset.Identity;
                            }
                            else
                            {
                                Log.LogMessage(MessageImportance.Low, "Source for asset '{0}' is '{1}' since the asset does not exist.", asset.Identity, asset.OriginalItemSpec);
                                source = asset.OriginalItemSpec;
                            }
                        }
                        else
                        {
                            source = asset.Identity;
                        }

                        copyToOutputFolder.Add(new TaskItem(source, new Dictionary<string, string>
                        {
                            ["OriginalItemSpec"] = asset.Identity,
                            ["TargetPath"] = fileOutputPath,
                            ["CopyToOutputDirectory"] = asset.CopyToOutputDirectory
                        }));
                    }
                    else
                    {
                        Log.LogMessage(MessageImportance.Low, "Skipping asset '{0}' since copy to output directory option is '{1}'", asset.Identity, asset.CopyToOutputDirectory);
                    }
                }

                AssetsToCopy = copyToOutputFolder.ToArray();
            }
            catch (Exception ex)
            {
                Log.LogError(ex.ToString());
            }

            return !Log.HasLoggedErrors;
        }
    }
}
