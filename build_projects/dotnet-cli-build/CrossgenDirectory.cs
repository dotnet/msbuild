// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using static Microsoft.DotNet.Cli.Build.Framework.BuildHelpers;

namespace Microsoft.DotNet.Cli.Build
{
    public class CrossgenDirectory : Task
    {
        [Required]
        public string CoreCLRVersion { get; set; }

        [Required]
        public string JitVersion { get; set; }

        [Required]
        public string SharedFrameworkNameVersionPath { get; set; }

        [Required]
        public string SdkOutputDirectory { get; set; }        

        public override bool Execute()
        {
            var crossgenUtil = new Crossgen(CoreCLRVersion, JitVersion);

            crossgenUtil.CrossgenDirectory(SharedFrameworkNameVersionPath, SdkOutputDirectory);

            return true;
        }
    }
}
