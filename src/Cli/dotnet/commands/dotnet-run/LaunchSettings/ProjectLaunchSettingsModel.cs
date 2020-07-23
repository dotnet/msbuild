// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.Tools.Run.LaunchSettings
{
    public class ProjectLaunchSettingsModel
    {
        public string CommandLineArgs { get; set; }

        public bool LaunchBrowser { get; set; }

        public string LaunchUrl { get; set; }

        public string ApplicationUrl { get; set; }

        public string DotNetRunMessages { get; set; }

        public Dictionary<string, string> EnvironmentVariables { get; } = new Dictionary<string, string>(StringComparer.Ordinal);
    }
}
