// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Cli.Build.Framework;
using Microsoft.DotNet.PlatformAbstractions;

namespace Microsoft.DotNet.Cli.Build
{
    public class GetCurrentRuntimeInformation : Task
    {
        [Output]
        public string Rid { get; set; }

        [Output]
        public string OSName { get; set; }

        [Output]
        public string OSPlatform { get; set; }

        public override bool Execute()
        {
            Rid = RuntimeEnvironment.GetRuntimeIdentifier();
            OSName = GetOSShortName();
            OSPlatform = RuntimeEnvironment.OperatingSystemPlatform.ToString().ToLower();

            return true;
        }

        private static string GetOSShortName()
        {
            string osname = "";
            switch (CurrentPlatform.Current)
            {
                case BuildPlatform.Windows:
                    osname = "win";
                    break;
                default:
                    osname = CurrentPlatform.Current.ToString().ToLower();
                    break;
            }

            return osname;
        }
    }
}
