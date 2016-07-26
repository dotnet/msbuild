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
        [BuildPlatforms(BuildPlatform.Ubuntu)]
        public static BuildTargetResult GenerateDebs(BuildTargetContext c)
        {
            return c.Success();
        }

        [Target(nameof(InstallSharedFramework))]
        [BuildPlatforms(BuildPlatform.Ubuntu)]
        public static BuildTargetResult GenerateSdkDeb(BuildTargetContext c)
        {
            // Ubuntu 16.04 Jenkins Machines don't have docker or debian package build tools
            // So we need to skip this target if the tools aren't present.
            // https://github.com/dotnet/core-setup/issues/167
            if (DebuildNotPresent())
            {
                c.Info("Debuild not present, skipping target: {nameof(RemovePackages)}");
                return c.Success();
            }
            
            var channel = c.BuildContext.Get<string>("Channel").ToLower();
            var packageName = CliMonikers.GetSdkDebianPackageName(c);
            var version = c.BuildContext.Get<BuildVersion>("BuildVersion").NuGetVersion;
            var debFile = c.BuildContext.Get<string>("SdkInstallerFile");
            var manPagesDir = Path.Combine(Dirs.RepoRoot, "Documentation", "manpages");
            var sdkPublishRoot = c.BuildContext.Get<string>("CLISDKRoot");
            var sharedFxDebianPackageName = Monikers.GetDebianSharedFrameworkPackageName(CliDependencyVersions.SharedFrameworkVersion);
            var debianConfigFile = Path.Combine(Dirs.DebPackagingConfig, "dotnet-debian_config.json");
            var postinstallFile = Path.Combine(Dirs.DebPackagingConfig, "postinst");

            var debianConfigVariables = new Dictionary<string, string>()
            {
                { "SHARED_FRAMEWORK_DEBIAN_PACKAGE_NAME", sharedFxDebianPackageName },
                { "SHARED_FRAMEWORK_NUGET_NAME", Monikers.SharedFrameworkName },
                { "SHARED_FRAMEWORK_NUGET_VERSION",  CliDependencyVersions.SharedFrameworkVersion },
                { "SHARED_FRAMEWORK_BRAND_NAME", Monikers.SharedFxBrandName },
                { "SDK_NUGET_VERSION", version },
                { "CLI_SDK_BRAND_NAME", Monikers.CLISdkBrandName }
            };

            var debCreator = new DebPackageCreator(
                DotNetCli.Stage2,
                Dirs.Intermediate);

            debCreator.CreateDeb(
                debianConfigFile,
                packageName,
                version,
                sdkPublishRoot,
                debianConfigVariables,
                debFile,
                manpagesDirectory: manPagesDir,
                versionManpages: true,
                debianFiles: new string[] { postinstallFile });

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
            // Ubuntu 16.04 Jenkins Machines don't have docker or debian package build tools
            // So we need to skip this target if the tools aren't present.
            // https://github.com/dotnet/core-setup/issues/167
            if (DebuildNotPresent())
            {
                c.Info("Debuild not present, skipping target: {nameof(RemovePackages)}");
                return c.Success();
            }

            InstallPackage(c.BuildContext.Get<string>("SharedHostInstallerFile"));
            
            return c.Success();
        }

        [Target(nameof(InstallSharedHost))]
        public static BuildTargetResult InstallHostFxr(BuildTargetContext c)
        {
            // Ubuntu 16.04 Jenkins Machines don't have docker or debian package build tools
            // So we need to skip this target if the tools aren't present.
            // https://github.com/dotnet/core-setup/issues/167
            if (DebuildNotPresent())
            {
                c.Info("Debuild not present, skipping target: {nameof(RemovePackages)}");
                return c.Success();
            }

            InstallPackage(c.BuildContext.Get<string>("HostFxrInstallerFile"));
            
            return c.Success();
        }
        
        [Target(nameof(InstallHostFxr))]
        public static BuildTargetResult InstallSharedFramework(BuildTargetContext c)
        {
            // Ubuntu 16.04 Jenkins Machines don't have docker or debian package build tools
            // So we need to skip this target if the tools aren't present.
            // https://github.com/dotnet/core-setup/issues/167
            if (DebuildNotPresent())
            {
                c.Info("Debuild not present, skipping target: {nameof(RemovePackages)}");
                return c.Success();
            }

            InstallPackage(c.BuildContext.Get<string>("SharedFrameworkInstallerFile"));
            
            return c.Success();
        }
        
        [Target(nameof(InstallSharedFramework))]
        public static BuildTargetResult InstallSDK(BuildTargetContext c)
        {
            // Ubuntu 16.04 Jenkins Machines don't have docker or debian package build tools
            // So we need to skip this target if the tools aren't present.
            // https://github.com/dotnet/core-setup/issues/167
            if (DebuildNotPresent())
            {
                c.Info("Debuild not present, skipping target: {nameof(RemovePackages)}");
                return c.Success();
            }

            InstallPackage(c.BuildContext.Get<string>("SdkInstallerFile"));
            
            return c.Success();
        }
        
        [Target]
        [BuildPlatforms(BuildPlatform.Ubuntu)]
        public static BuildTargetResult RunE2ETest(BuildTargetContext c)
        {
            // Ubuntu 16.04 Jenkins Machines don't have docker or debian package build tools
            // So we need to skip this target if the tools aren't present.
            // https://github.com/dotnet/core-setup/issues/167
            if (DebuildNotPresent())
            {
                c.Info("Debuild not present, skipping target: {nameof(RemovePackages)}");
                return c.Success();
            }

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
            // Ubuntu 16.04 Jenkins Machines don't have docker or debian package build tools
            // So we need to skip this target if the tools aren't present.
            // https://github.com/dotnet/core-setup/issues/167
            if (DebuildNotPresent())
            {
                c.Info("Debuild not present, skipping target: {nameof(RemovePackages)}");
                return c.Success();
            }

            IEnumerable<string> orderedPackageNames = new List<string>()
            {
                CliMonikers.GetSdkDebianPackageName(c),
                Monikers.GetDebianSharedFrameworkPackageName(CliDependencyVersions.SharedFrameworkVersion),
                Monikers.GetDebianHostFxrPackageName(CliDependencyVersions.HostFxrVersion),
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

        private static bool DebuildNotPresent()
        {
            return Cmd("/usr/bin/env", "debuild", "-h").Execute().ExitCode != 0;
        }
    }
}
