using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Cli.Build.Framework;
using Microsoft.DotNet.InternalAbstractions;

using static Microsoft.DotNet.Cli.Build.Framework.BuildHelpers;

namespace Microsoft.DotNet.Cli.Build
{
    public class DebTargets
    {
        [Target(nameof(GenerateSdkDeb))]
        [BuildPlatforms(BuildPlatform.Ubuntu, "14.04")]
        public static BuildTargetResult GenerateDebs(BuildTargetContext c)
        {
            return c.Success();
        }

        [Target(nameof(InstallSharedFramework))]
        [BuildPlatforms(BuildPlatform.Ubuntu, "14.04")]
        public static BuildTargetResult GenerateSdkDeb(BuildTargetContext c)
        {
            var channel = c.BuildContext.Get<string>("Channel").ToLower();
            var packageName = CliMonikers.GetSdkDebianPackageName(c);
            var version = c.BuildContext.Get<BuildVersion>("BuildVersion").NuGetVersion;
            var debFile = c.BuildContext.Get<string>("SdkInstallerFile");
            var manPagesDir = Path.Combine(Dirs.RepoRoot, "Documentation", "manpages");
            var previousVersionURL = $"https://dotnetcli.blob.core.windows.net/dotnet/{channel}/Installers/Latest/dotnet-ubuntu-x64.latest.deb";
            var sdkPublishRoot = c.BuildContext.Get<string>("CLISDKRoot");
            var sharedFxDebianPackageName = Monikers.GetDebianSharedFrameworkPackageName(CliDependencyVersions.SharedFrameworkVersion);

            var objRoot = Path.Combine(Dirs.Output, "obj", "debian", "sdk");

            if (Directory.Exists(objRoot))
            {
                Directory.Delete(objRoot, true);
            }

            Directory.CreateDirectory(objRoot);

            Cmd(Path.Combine(Dirs.RepoRoot, "scripts", "package", "package-debian.sh"),
                "-v", version, 
                "-i", sdkPublishRoot, 
                "-o", debFile, 
                "-p", packageName,
                "-b", Monikers.CLISdkBrandName,
                "-m", manPagesDir, 
                "--framework-debian-package-name", sharedFxDebianPackageName,
                "--framework-nuget-name", Monikers.SharedFrameworkName,
                "--framework-nuget-version", CliDependencyVersions.SharedFrameworkVersion,
                "--previous-version-url", previousVersionURL, 
                "--obj-root", objRoot)
                    .Execute()
                    .EnsureSuccessful();
            return c.Success();
        }

        [Target(nameof(InstallSDK),
                nameof(RunE2ETest),
                nameof(RemovePackages))]
        [BuildPlatforms(BuildPlatform.Ubuntu, "14.04")]
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
        
        [Target(nameof(InstallSharedHost))]
        public static BuildTargetResult InstallSharedFramework(BuildTargetContext c)
        {
            InstallPackage(c.BuildContext.Get<string>("SharedFrameworkInstallerFile"));
            
            return c.Success();
        }
        
        [Target(nameof(InstallSharedFramework))]
        public static BuildTargetResult InstallSDK(BuildTargetContext c)
        {
            InstallPackage(c.BuildContext.Get<string>("SdkInstallerFile"));
            
            return c.Success();
        }
        
        [Target]
        [BuildPlatforms(BuildPlatform.Ubuntu, "14.04")]
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
        [BuildPlatforms(BuildPlatform.Ubuntu, "14.04")]
        public static BuildTargetResult RemovePackages(BuildTargetContext c)
        {
            IEnumerable<string> orderedPackageNames = new List<string>()
            {
                CliMonikers.GetSdkDebianPackageName(c),
                Monikers.GetDebianSharedFrameworkPackageName(CliDependencyVersions.SharedFrameworkVersion),
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
