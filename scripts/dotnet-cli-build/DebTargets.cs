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
    public class DebTargets
    {
        [Target(nameof(GenerateSharedHostDeb),
                nameof(GenerateSharedFrameworkDeb),
                nameof(GenerateSdkDeb))]
        [BuildPlatforms(BuildPlatform.Ubuntu)]
        public static BuildTargetResult GenerateDebs(BuildTargetContext c)
        {
            return c.Success();
        }

        [Target(nameof(InstallSharedFramework))]
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
            var packageName = Monikers.GetDebianSharedHostPackageName(c);
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

        [Target(nameof(InstallSharedHost))]
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
                    "--framework-nuget-name", Monikers.SharedFrameworkName,
                    "--framework-nuget-version", c.BuildContext.Get<string>("SharedFrameworkNugetVersion"),
                    "--obj-root", objRoot, "--version", version)
                    .Execute()
                    .EnsureSuccessful();
            return c.Success();
        }

        [Target(nameof(InstallSDK),
                nameof(RunE2ETest),
                nameof(RemovePackages))]
        [BuildPlatforms(BuildPlatform.Ubuntu)]
        public static BuildTargetResult TestDebInstaller(BuildTargetContext c)
        {
            return c.Success();
        }
        
        [Target]
        public static BuildTargetResult InstallSharedHost(BuildTargetContext c)
        {
            InstallPackage(c.BuildContext.Get<string>("SharedHostInstallerFile"));
            
            return c.Success();
        }
        
        [Target]
        public static BuildTargetResult InstallSharedFramework(BuildTargetContext c)
        {
            InstallPackage(c.BuildContext.Get<string>("SharedFrameworkInstallerFile"));
            
            return c.Success();
        }
        
        [Target]
        public static BuildTargetResult InstallSDK(BuildTargetContext c)
        {
            InstallPackage(c.BuildContext.Get<string>("SdkInstallerFile"));
            
            return c.Success();
        }
        
        [Target]
        [BuildPlatforms(BuildPlatform.Ubuntu)]
        public static BuildTargetResult RunE2ETest(BuildTargetContext c)
        {
            Directory.SetCurrentDirectory(Path.Combine(Dirs.RepoRoot, "test", "EndToEnd"));
            
            Cmd("dotnet", "build")
                .Execute()
                .EnsureSuccessful();
            
            var testResultsPath = Path.Combine(Dirs.Output, "obj", "debian", "test", "debian-endtoend-testResults.xml");
            
            Cmd("dotnet", "test", "-xml", testResultsPath)
                .Execute()
                .EnsureSuccessful();
            
            return c.Success();
        }
        
        [Target]
        [BuildPlatforms(BuildPlatform.Ubuntu)]
        public static BuildTargetResult RemovePackages(BuildTargetContext c)
        {
            IEnumerable<string> orderedPackageNames = new List<string>()
            {
                Monikers.GetDebianPackageName(c),
                Monikers.GetDebianSharedFrameworkPackageName(c),
                Monikers.GetDebianSharedHostPackageName(c)
            };
            
            foreach(var packageName in orderedPackageNames)
            {
                RemovePackage(packageName);
            }
            
            return c.Success();
        }
        
        private static void InstallPackage(string packagePath)
        {
            Cmd("sudo", "dpkg", "-i", packagePath)
                .Execute()
                .EnsureSuccessful();
        }
        
        private static void RemovePackage(string packageName)
        {
            Cmd("sudo", "dpkg", "-r", packageName)
                .Execute()
                .EnsureSuccessful();
        }
    }
}
