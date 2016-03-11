using Microsoft.DotNet.Cli.Build.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Cli.Build
{
    public class Monikers
    {
        public static string GetProductMoniker(BuildTargetContext c)
        {
            string osname = GetOSShortName();
            var arch = CurrentArchitecture.Current.ToString();
            var version = c.BuildContext.Get<BuildVersion>("BuildVersion").SimpleVersion;
            return $"dotnet-{osname}-{arch}.{version}";
        }

        public static string GetDebianPackageName(BuildTargetContext c)
        {
            var channel = c.BuildContext.Get<string>("Channel").ToLower();
            var packageName = "";
            switch (channel)
            {
                case "dev":
                    packageName = "dotnet-nightly";
                    break;
                case "beta":
                case "rc1":
                case "rc2":
                case "rtm":
                    packageName = "dotnet";
                    break;
               default:
                    throw new Exception($"Unknown channel - {channel}");
            }

            return packageName;
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
