using System.Collections.Generic;

namespace Microsoft.DotNet.Cli.Build
{
    public class BuildVersion
    {
        public int Major { get; set; }
        public int Minor { get; set; }
        public int Patch { get; set; }
        public int CommitCount { get; set; }
        public string CommitCountString => CommitCount.ToString("000000");
        public string ReleaseSuffix { get; set; }

        public string SimpleVersion => $"{Major}.{Minor}.{Patch}.{CommitCountString}";
        public string VersionSuffix => $"{ReleaseSuffix}-{CommitCountString}";
        public string NuGetVersion => $"{Major}.{Minor}.{Patch}-{VersionSuffix}";
        public string NetCoreAppVersion => $"{Major}.{Minor}.{Patch}-rc2-3{CommitCountString}";
        public string ProductionVersion => $"{Major}.{Minor}.{Patch}";

        // ------------------------------------------HOST-VERSIONING-------------------------------------------
        //
        // Host versions are independent of CLI versions. Moreover, these version numbers
        // are baked into the binary and is used to look up a serviced binary replacement.
        //

        //
        // Latest hosts for production of nupkgs.
        //

        // Version constants without suffix
        public string LatestHostVersionNoSuffix => "1.0.1";
        public string LatestHostFxrVersionNoSuffix => "1.0.1";
        public string LatestHostPolicyVersionNoSuffix => "1.0.1";
        public string LatestHostPrerelease => "rc2";
        public string LatestHostBuildMajor => $"{CommitCountString}";
        public string LatestHostSuffix => $"{LatestHostPrerelease}-{LatestHostBuildMajor}-00";

        // Full versions and package information.
        private string LatestHostVersion => $"{LatestHostVersionNoSuffix}-{LatestHostSuffix}";
        private string LatestHostFxrVersion => $"{LatestHostFxrVersionNoSuffix}-{LatestHostSuffix}";
        public  string LatestHostPolicyVersion => $"{LatestHostPolicyVersionNoSuffix}-{LatestHostSuffix}";
        public Dictionary<string, string> LatestHostPackages => new Dictionary<string, string>()
        {
            { "Microsoft.NETCore.DotNetHost", LatestHostVersion },
            { "Microsoft.NETCore.DotNetHostResolver", LatestHostFxrVersion },
            { "Microsoft.NETCore.DotNetHostPolicy", LatestHostPolicyVersion }
        };

        //
        // Locked muxer for consumption in CLI.
        //
        public bool IsLocked = false; // Set this variable to toggle muxer locking.
        public string LockedHostFxrVersion => IsLocked ? "1.0.1-rc2-002468-00" : LatestHostFxrVersion;

        //
        // -----------------------------------------END-OF-HOST-VERSIONING-------------------------------------

        public string GenerateMsiVersion()
        {
            // MSI versioning
            // Encode the CLI version to fit into the MSI versioning scheme - https://msdn.microsoft.com/en-us/library/windows/desktop/aa370859(v=vs.85).aspx
            // MSI versions are 3 part
            //                           major.minor.build
            // Size(bits) of each part     8     8    16
            // So we have 32 bits to encode the CLI version
            // Starting with most significant bit this how the CLI version is going to be encoded as MSI Version
            // CLI major  -> 6 bits
            // CLI minor  -> 6 bits
            // CLI patch  -> 6 bits
            // CLI commitcount -> 14 bits
            var major = Major << 26;
            var minor = Minor << 20;
            var patch = Patch << 14;
            var msiVersionNumber = major | minor | patch | CommitCount;

            var msiMajor = (msiVersionNumber >> 24) & 0xFF;
            var msiMinor = (msiVersionNumber >> 16) & 0xFF;
            var msiBuild = msiVersionNumber & 0xFFFF;

            return $"{msiMajor}.{msiMinor}.{msiBuild}";
        }
    }
}
