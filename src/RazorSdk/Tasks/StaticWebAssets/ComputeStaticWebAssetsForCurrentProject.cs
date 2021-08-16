// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.Razor.Tasks
{
    public class ComputeStaticWebAssetsForCurrentProject : Task
    {
        [Required]
        public ITaskItem[] Assets { get; set; }

        [Required]
        public string ProjectMode { get; set; }

        [Required]
        public string AssetKind { get; set; }

        [Required]
        public string Source { get; set; }

        [Output]
        public ITaskItem[] StaticWebAssets { get; set; }

        public override bool Execute()
        {
            try
            {
                var currentProjectAssets = Assets
                    .Where(asset => StaticWebAsset.HasSourceId(asset, Source))
                    .Select(StaticWebAsset.FromTaskItem)
                    .GroupBy(
                        a => a.ComputeTargetPath("", '/'),
                        (key, group) => (key, StaticWebAsset.ChooseNearestAssetKind(group, AssetKind)));

                var resultAssets = new List<StaticWebAsset>();
                foreach (var (key, group) in currentProjectAssets)
                {
                    if (!TryGetUniqueAsset(group, out var selected))
                    {
                        if (selected == null)
                        {
                            Log.LogMessage("No compatible asset found for '{0}'", key);
                            continue;
                        }
                        else
                        {
                            Log.LogError("More than one compatible asset found for '{0}'.", selected.Identity);
                            return false;
                        }
                    }

                    if (!selected.IsForReferencedProjectsOnly())
                    {
                        resultAssets.Add(selected);
                    }
                    else
                    {
                        Log.LogMessage("Skipping asset '{0}' because it is for referenced projects only.", selected.Identity);
                    }
                }

                StaticWebAssets = resultAssets
                    .Select(a => a.ToTaskItem())
                    .Concat(Assets.Where(asset => !StaticWebAsset.HasSourceId(asset, Source)))
                    .ToArray();
            }
            catch (Exception ex)
            {
                Log.LogError(ex.ToString());
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
    }
}
