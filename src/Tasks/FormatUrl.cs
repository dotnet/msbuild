// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks.Deployment.ManifestUtilities;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Formats a url by canonicalizing it (i.e. " " -> "%20") and transforming "localhost" to "machinename".
    /// </summary>
    public sealed class FormatUrl : TaskExtension
    {
        public string InputUrl { get; set; }

        [Output]
        public string OutputUrl { get; set; }

        public override bool Execute()
        {
            OutputUrl = InputUrl != null ? PathUtil.Format(InputUrl) : String.Empty;
            return true;
        }
    }
}
