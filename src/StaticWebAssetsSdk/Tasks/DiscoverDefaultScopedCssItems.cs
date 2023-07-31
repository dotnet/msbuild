// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks
{
    public class DiscoverDefaultScopedCssItems : Task
    {
        [Required]
        public ITaskItem[] Content { get; set; }

        [Required]
        /// <remarks>
        /// <c>.cshtml.css</c> is only supported for .NET 6 and newer apps. Since this task is used in older apps
        /// too, this property determines if we should consider .cshtml.css files.
        /// </remarks>
        public bool SupportsScopedCshtmlCss { get; set; }

        [Output]
        public ITaskItem[] DiscoveredScopedCssInputs { get; set; }

        public override bool Execute()
        {
            var discoveredInputs = new List<ITaskItem>();

            foreach (var candidate in Content)
            {
                var fullPath = candidate.GetMetadata("FullPath");
                if (string.Equals(candidate.GetMetadata("Scoped"), "false", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (fullPath.EndsWith(".razor.css", StringComparison.OrdinalIgnoreCase))
                {
                    discoveredInputs.Add(candidate);
                }
                else if (SupportsScopedCshtmlCss && fullPath.EndsWith(".cshtml.css", StringComparison.OrdinalIgnoreCase))
                {
                    discoveredInputs.Add(candidate);
                }
            }

            DiscoveredScopedCssInputs = discoveredInputs.ToArray();

            return !Log.HasLoggedErrors;
        }
    }
}
