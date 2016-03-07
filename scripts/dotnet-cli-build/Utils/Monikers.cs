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
