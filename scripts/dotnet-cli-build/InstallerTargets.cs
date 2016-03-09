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
        nameof(PkgTargets.GeneratePkgs),
        nameof(InstallerTargets.GenerateDebs))]
        public static BuildTargetResult GenerateInstaller(BuildTargetContext c)
        {
            return c.Success();
        }

        [Target(nameof(InstallerTargets.GenerateSdkDeb),
        nameof(InstallerTargets.GenerateSharedFrameworkDeb),
        nameof(InstallerTargets.GenerateSharedHostDeb))]
        [BuildPlatforms(BuildPlatform.Ubuntu)]
        public static BuildTargetResult GenerateDebs(BuildTargetContext c)
        {
            return c.Success();
        }

        [Target]
        [BuildPlatforms(BuildPlatform.Ubuntu)]
        public static BuildTargetResult GenerateSdkDeb(BuildTargetContext c)
        {
            var channel = c.BuildContext.Get<string>("Channel").ToLower();
            var packageName = Monikers.GetDebianPackageName(c);
            var version = c.BuildContext.Get<BuildVersion>("BuildVersion").SimpleVersion;
            var debFile = c.BuildContext.Get<string>("SdkInstallerFile");
            var manPagesDir = Path.Combine(Dirs.RepoRoot, "Documentation", "manpages");
            var previousVersionURL = $"https://dotnetcli.blob.core.windows.net/dotnet/{channel}/Installers/Latest/dotnet-ubuntu-x64.latest.deb";

            var objRoot = Path.Combine(Dirs.Output, "obj", "debian", "sdk");

            if (Directory.Exists(objRoot))
            {
                Directory.Delete(objRoot, true);
            }

            Directory.CreateDirectory(objRoot);

            Cmd(Path.Combine(Dirs.RepoRoot, "scripts", "package", "package-debian.sh"),
                "-v", version, "-i", Dirs.Stage2, "-o", debFile, "-p", packageName, "-m", manPagesDir, "--previous-version-url", previousVersionURL, "--obj-root", objRoot)
                    .Execute()
                    .EnsureSuccessful();
            return c.Success();
        }

        [Target]
        [BuildPlatforms(BuildPlatform.Ubuntu)]
        public static BuildTargetResult GenerateSharedHostDeb(BuildTargetContext c)
        {
            var version = c.BuildContext.Get<BuildVersion>("BuildVersion").SimpleVersion;
            var inputRoot = c.BuildContext.Get<string>("SharedHostPublishRoot");
            var debFile = c.BuildContext.Get<string>("SharedHostInstallerFile");
            var objRoot = Path.Combine(Dirs.Output, "obj", "debian", "sharedhost");

            if (Directory.Exists(objRoot))
            {
                Directory.Delete(objRoot, true);
            }

            Directory.CreateDirectory(objRoot);

            Cmd(Path.Combine(Dirs.RepoRoot, "scripts", "package", "package-sharedhost-debian.sh"),
                    "--input", inputRoot, "--output", debFile, "--obj-root", objRoot, "--version", version)
                    .Execute()
                    .EnsureSuccessful();
            return c.Success();
        }

        [Target]
        [BuildPlatforms(BuildPlatform.Ubuntu)]
        public static BuildTargetResult GenerateSharedFrameworkDeb(BuildTargetContext c)
        {
            var packageName = Monikers.GetDebianSharedFrameworkPackageName(c);
            var version = c.BuildContext.Get<BuildVersion>("BuildVersion").SimpleVersion;
            var inputRoot = c.BuildContext.Get<string>("SharedFrameworkPublishRoot");
            var debFile = c.BuildContext.Get<string>("SharedFrameworkInstallerFile");
            var objRoot = Path.Combine(Dirs.Output, "obj", "debian", "sharedframework");

            if (Directory.Exists(objRoot))
            {
                Directory.Delete(objRoot, true);
            }

            Directory.CreateDirectory(objRoot);

            Cmd(Path.Combine(Dirs.RepoRoot, "scripts", "package", "package-sharedframework-debian.sh"),
                    "--input", inputRoot, "--output", debFile, "--package-name", packageName,
                    "--framework-nuget-name", SharedFrameworkTargets.SharedFrameworkName,
                    "--framework-nuget-version", c.BuildContext.Get<string>("SharedFrameworkNugetVersion"),
                    "--obj-root", objRoot, "--version", version)
                    .Execute()
                    .EnsureSuccessful();
            return c.Success();
        }
    }
}
