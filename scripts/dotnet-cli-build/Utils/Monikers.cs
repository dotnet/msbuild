using Microsoft.DotNet.Cli.Build.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Cli.Build
{
    public class Monikers
    {
        public const string SharedFrameworkName = "Microsoft.NETCore.App";

        public static string GetProductMoniker(BuildTargetContext c, string artifactPrefix, string version)
        {
            string osname = GetOSShortName();
            var arch = CurrentArchitecture.Current.ToString();
            return $"{artifactPrefix}-{osname}-{arch}.{version}";
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

        public static string GetSdkDebianPackageName(BuildTargetContext c)
        {
            var channel = c.BuildContext.Get<string>("Channel").ToLower();
            var nugetVersion = c.BuildContext.Get<BuildVersion>("BuildVersion").NuGetVersion;

            var packagePrefix = "";
            switch (channel)
            {
                case "dev":
                    packagePrefix = "dotnet-nightly";
                    break;
                case "beta":
                case "rc1":
                case "rc2":
                case "rtm":
                    packagePrefix = "dotnet";
                    break;
                default:
                    throw new Exception($"Unknown channel - {channel}");
            }

            return $"{packagePrefix}-dev-{nugetVersion}";
        }

        public static string GetDebianSharedFrameworkPackageName(BuildTargetContext c)
        {
            var sharedFrameworkNugetVersion = c.BuildContext.Get<string>("SharedFrameworkNugetVersion");

            return $"dotnet-sharedframework-{SharedFrameworkName}-{sharedFrameworkNugetVersion}".ToLower();
        }

        public static string GetDebianSharedHostPackageName(BuildTargetContext c)
        {
            var sharedFrameworkNugetVersion = c.BuildContext.Get<string>("SharedFrameworkNugetVersion");

            return $"dotnet-host".ToLower();
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
