// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Utils
{
    internal enum Platform
    {
        Unknown = 0,
        Windows = 1,
        Linux = 2,
        Darwin = 3,
        FreeBSD = 4,
        illumos = 5,
        Solaris = 6,
        Haiku = 7
    }

    internal static class RuntimeEnvironment
    {
        private static readonly Lazy<Platform> _platform = new(DetermineOSPlatform);
        private static readonly Lazy<DistroInfo> _distroInfo = new(LoadDistroInfo);

        public static Platform OperatingSystemPlatform { get; } = GetOSPlatform();
        public static string OperatingSystemVersion { get; } = GetOSVersion();
        public static string OperatingSystem { get; } = GetOSName();

        private class DistroInfo
        {
            public string Id;
            public string VersionId;
        }

        private static string GetOSName()
        {
            switch (GetOSPlatform())
            {
                case Platform.Windows:
                    return nameof(Platform.Windows);
                case Platform.Linux:
                    return GetDistroId() ?? nameof(Platform.Linux);
                case Platform.Darwin:
                    return "Mac OS X";
                case Platform.FreeBSD:
                    return nameof(Platform.FreeBSD);
                case Platform.illumos:
                    return GetDistroId() ?? nameof(Platform.illumos);
                case Platform.Solaris:
                    return nameof(Platform.Solaris);
                case Platform.Haiku:
                    return nameof(Platform.Haiku);
                default:
                    return nameof(Platform.Unknown);
            }
        }

        private static string GetOSVersion()
        {
            switch (GetOSPlatform())
            {
                case Platform.Windows:
                    return Environment.OSVersion.Version.ToString(3);
                case Platform.Linux:
                case Platform.illumos:
                    return GetDistroVersionId() ?? string.Empty;
                case Platform.Darwin:
                    return Environment.OSVersion.Version.ToString(2);
                case Platform.FreeBSD:
                    return GetFreeBSDVersion() ?? string.Empty;
                case Platform.Solaris:
                    // RuntimeInformation.OSDescription example on Solaris 11.3:      SunOS 5.11 11.3
                    // we only need the major version; 11
                    return RuntimeInformation.OSDescription.Split(' ')[2].Split('.')[0];
                case Platform.Haiku:
                    return Environment.OSVersion.Version.ToString(1);
                default:
                    return string.Empty;
            }
        }

        private static string GetFreeBSDVersion()
        {
            // This is same as sysctl kern.version
            // FreeBSD 11.0-RELEASE-p1 FreeBSD 11.0-RELEASE-p1 #0 r306420: Thu Sep 29 01:43:23 UTC 2016     root@releng2.nyi.freebsd.org:/usr/obj/usr/src/sys/GENERIC
            // What we want is major release as minor releases should be compatible.
            string version = RuntimeInformation.OSDescription;
            try
            {
                // second token up to first dot
                return RuntimeInformation.OSDescription.Split()[1].Split('.')[0];
            }
            catch
            {
            }
            return string.Empty;
        }

        private static Platform GetOSPlatform()
        {
            return _platform.Value;
        }

        private static string GetDistroId()
        {
            return _distroInfo.Value?.Id;
        }

        private static string GetDistroVersionId()
        {
            return _distroInfo.Value?.VersionId;
        }

        private static DistroInfo LoadDistroInfo()
        {
            switch (GetOSPlatform())
            {
                case Platform.Linux:
                    return LoadDistroInfoFromLinux();
                case Platform.illumos:
                    return LoadDistroInfoFromIllumos();
            }

            return null;
        }

        private static DistroInfo LoadDistroInfoFromLinux()
        {
            DistroInfo result = null;

            // Sample os-release file:
            //   NAME="Ubuntu"
            //   VERSION = "14.04.3 LTS, Trusty Tahr"
            //   ID = ubuntu
            //   ID_LIKE = debian
            //   PRETTY_NAME = "Ubuntu 14.04.3 LTS"
            //   VERSION_ID = "14.04"
            //   HOME_URL = "http://www.ubuntu.com/"
            //   SUPPORT_URL = "http://help.ubuntu.com/"
            //   BUG_REPORT_URL = "http://bugs.launchpad.net/ubuntu/"
            // We use ID and VERSION_ID

            if (File.Exists("/etc/os-release"))
            {
                var lines = File.ReadAllLines("/etc/os-release");
                result = new DistroInfo();
                foreach (var line in lines)
                {
                    if (line.StartsWith("ID=", StringComparison.Ordinal))
                    {
                        result.Id = line.Substring(3).Trim('"', '\'');
                    }
                    else if (line.StartsWith("VERSION_ID=", StringComparison.Ordinal))
                    {
                        result.VersionId = line.Substring(11).Trim('"', '\'');
                    }
                }
            }

            if (result != null)
            {
                result = NormalizeDistroInfo(result);
            }

            return result;
        }

        private static DistroInfo LoadDistroInfoFromIllumos()
        {
            DistroInfo result = null;
            // examples:
            //   on OmniOS
            //       SunOS 5.11 omnios-r151018-95eaa7e
            //   on OpenIndiana Hipster:
            //       SunOS 5.11 illumos-63878f749f
            //   on SmartOS:
            //       SunOS 5.11 joyent_20200408T231825Z
            var versionDescription = RuntimeInformation.OSDescription.Split(' ')[2];
            switch (versionDescription)
            {
                case string version when version.StartsWith("omnios"):
                    result = new DistroInfo
                    {
                        Id = "OmniOS",
                        VersionId = version.Substring("omnios-r".Length, 2) // e.g. 15
                    };
                    break;
                case string version when version.StartsWith("joyent"):
                    result = new DistroInfo
                    {
                        Id = "SmartOS",
                        VersionId = version.Substring("joyent_".Length, 4) // e.g. 2020
                    };
                    break;
                case string version when version.StartsWith("illumos"):
                    result = new DistroInfo
                    {
                        Id = "OpenIndiana"
                        // version-less
                    };
                    break;
            }

            return result;
        }

        // For some distros, we don't want to use the full version from VERSION_ID. One example is
        // Red Hat Enterprise Linux, which includes a minor version in their VERSION_ID but minor
        // versions are backwards compatable.
        //
        // In this case, we'll normalized RIDs like 'rhel.7.2' and 'rhel.7.3' to a generic
        // 'rhel.7'. This brings RHEL in line with other distros like CentOS or Debian which
        // don't put minor version numbers in their VERSION_ID fields because all minor versions
        // are backwards compatible.
        private static DistroInfo NormalizeDistroInfo(DistroInfo distroInfo)
        {
            // Handle if VersionId is null by just setting the index to -1.
            int lastVersionNumberSeparatorIndex = distroInfo.VersionId?.IndexOf('.') ?? -1;

            if (lastVersionNumberSeparatorIndex != -1 && distroInfo.Id == "alpine")
            {
                // For Alpine, the version reported has three components, so we need to find the second version separator
                lastVersionNumberSeparatorIndex = distroInfo.VersionId.IndexOf('.', lastVersionNumberSeparatorIndex + 1);
            }

            if (lastVersionNumberSeparatorIndex != -1 && (distroInfo.Id == "rhel" || distroInfo.Id == "alpine"))
            {
                distroInfo.VersionId = distroInfo.VersionId.Substring(0, lastVersionNumberSeparatorIndex);
            }

            return distroInfo;
        }

        private static Platform DetermineOSPlatform()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Platform.Windows;
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return Platform.Linux;
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return Platform.Darwin;
            }
#if NETCOREAPP
            if (RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
            {
                return Platform.FreeBSD;
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Create("ILLUMOS")))
            {
                return Platform.illumos;
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Create("SOLARIS")))
            {
                return Platform.Solaris;
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Create("HAIKU")))
            {
                return Platform.Haiku;
            }
#endif

            return Platform.Unknown;
        }
    }
}
