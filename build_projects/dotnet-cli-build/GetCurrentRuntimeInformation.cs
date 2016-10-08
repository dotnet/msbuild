// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.PlatformAbstractions;

namespace Microsoft.DotNet.Cli.Build
{
    public class GetCurrentRuntimeInformation : Task
    {
        public string OverrideRid { get; set; }

        [Output]
        public string Rid { get; set; }

        [Output]
        public string Architecture { get; set; }

        [Output]
        public string OSName { get; set; }

        public override bool Execute()
        {
            Rid = string.IsNullOrEmpty(OverrideRid) ? RuntimeEnvironment.GetRuntimeIdentifier() : OverrideRid;
            Architecture = RuntimeEnvironment.RuntimeArchitecture;
            OSName = Monikers.GetOSShortName();

            return true;
        }
    }
}