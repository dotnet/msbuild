using Microsoft.DotNet.Cli.Build.Framework;
using Microsoft.DotNet.PlatformAbstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Cli.Build
{
    public class Monikers
    {
        public static string GetBadgeMoniker()
        {
            switch (RuntimeEnvironment.GetRuntimeIdentifier())
            {
                case "ubuntu.16.04-x64":
                    return "Ubuntu_16_04_x64";
                case "fedora.23-x64":
                    return "Fedora_23_x64";
                case "opensuse.13.2-x64":
                    return "openSUSE_13_2_x64";
            }

            return $"{CurrentPlatform.Current}_{CurrentArchitecture.Current}";
        }
        
        public static string GetOSShortName()
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
