// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks
{
    public class ResolveAllScopedCssAssets : Task
    {
        [Required]
        public ITaskItem[] StaticWebAssets { get; set; }

        [Output]
        public ITaskItem[] ScopedCssAssets { get; set; }

        [Output]
        public ITaskItem[] ScopedCssProjectBundles { get; set; }

        public override bool Execute()
        {
            var scopedCssAssets = new List<ITaskItem>();
            var scopedCssProjectBundles = new List<ITaskItem>();

            for (var i = 0; i < StaticWebAssets.Length; i++)
            {
                var swa = StaticWebAssets[i];
                var path = swa.GetMetadata("RelativePath");
                if (path.EndsWith(".rz.scp.css", StringComparison.OrdinalIgnoreCase))
                {
                    scopedCssAssets.Add(swa);
                }
                else if (path.EndsWith(".bundle.scp.css", StringComparison.OrdinalIgnoreCase))
                {
                    scopedCssProjectBundles.Add(swa);
                }
            }

            ScopedCssAssets = scopedCssAssets.ToArray();
            ScopedCssProjectBundles = scopedCssProjectBundles.ToArray();

            return !Log.HasLoggedErrors;
        }
    }
}
