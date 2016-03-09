using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Cli.Build.Framework;
using Microsoft.Extensions.PlatformAbstractions;

using static Microsoft.DotNet.Cli.Build.Framework.BuildHelpers;

namespace Microsoft.DotNet.Cli.Build
{
    public class InstallerTargets
    {
        [Target(nameof(MsiTargets.GenerateMsis),
        nameof(MsiTargets.GenerateBundle),
        nameof(InstallerTargets.GeneratePkg),
        nameof(InstallerTargets.GenerateDeb))]
        public static BuildTargetResult GenerateInstaller(BuildTargetContext c)
        {
            return c.Success();
        }



        [Target]
        [BuildPlatforms(BuildPlatform.OSX)]
        public static BuildTargetResult GeneratePkg(BuildTargetContext c)
        {
            var version = c.BuildContext.Get<BuildVersion>("BuildVersion").SimpleVersion;
            var pkg = c.BuildContext.Get<string>("InstallerFile");
            Cmd(Path.Combine(Dirs.RepoRoot, "packaging", "osx", "package-osx.sh"),
                    "-v", version, "-i", Dirs.Stage2, "-o", pkg)
                    .Execute()
                    .EnsureSuccessful();
            return c.Success();
        }

        [Target]
        [BuildPlatforms(BuildPlatform.Ubuntu)]
        public static BuildTargetResult GenerateDeb(BuildTargetContext c)
        {
            var channel = c.BuildContext.Get<string>("Channel").ToLower();
            var packageName = Monikers.GetDebianPackageName(c);
            var version = c.BuildContext.Get<BuildVersion>("BuildVersion").SimpleVersion;
            var debFile = c.BuildContext.Get<string>("InstallerFile");
            var manPagesDir = Path.Combine(Dirs.RepoRoot, "Documentation", "manpages");
            var previousVersionURL = $"https://dotnetcli.blob.core.windows.net/dotnet/{channel}/Installers/Latest/dotnet-ubuntu-x64.latest.deb";

            Cmd(Path.Combine(Dirs.RepoRoot, "scripts", "package", "package-debian.sh"),
                "-v", version, "-i", Dirs.Stage2, "-o", debFile, "-p", packageName, "-m", manPagesDir, "--previous-version-url", previousVersionURL)
                    .Execute()
                    .EnsureSuccessful();
            return c.Success();
        }
    }
}
