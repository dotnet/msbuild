// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.Razor.Tasks
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
