// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.Razor.Tasks
{
    public class UpdatePackageStaticWebAssets : Task
    {
        [Required]
        public ITaskItem[] Assets { get; set; }

        [Output]
        public ITaskItem[] UpdatedAssets { get; set; }

        [Output]
        public ITaskItem[] OriginalAssets { get; set; }

        public override bool Execute()
        {
            try
            {
                var originalAssets = new List<ITaskItem>();
                var updatedAssets = new List<ITaskItem>();
                for (var i = 0; i < Assets.Length; i++)
                {
                    var candidate = Assets[i];
                    if (!StaticWebAsset.SourceTypes.IsPackage(candidate.GetMetadata(nameof(StaticWebAsset.SourceType))))
                    {
                        continue;
                    }

                    originalAssets.Add(candidate);
                    updatedAssets.Add(StaticWebAsset.FromV1TaskItem(candidate).ToTaskItem());
                }

                OriginalAssets = originalAssets.ToArray();
                UpdatedAssets = updatedAssets.ToArray();
            }
            catch (Exception ex)
            {
                Log.LogError(ex.ToString());
            }

            return !Log.HasLoggedErrors;
        }
    }
}
