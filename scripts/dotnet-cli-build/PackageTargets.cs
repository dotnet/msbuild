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
    public static class PackageTargets
    {
        [Target(nameof(PackageTargets.CopyCLISDKLayout),
        nameof(SharedFrameworkTargets.PublishSharedHost),
        nameof(SharedFrameworkTargets.PublishSharedFramework))]
        public static BuildTargetResult InitPackage(BuildTargetContext c)
        {
            Directory.CreateDirectory(Dirs.Packages);
            return c.Success();
        }

        [Target(nameof(PrepareTargets.Init),
        nameof(PackageTargets.InitPackage),
        nameof(PackageTargets.GenerateVersionBadge),
        nameof(PackageTargets.GenerateCompressedFile),
        nameof(InstallerTargets.GenerateInstaller),
        nameof(PackageTargets.GenerateNugetPackages))]
        [Environment("DOTNET_BUILD_SKIP_PACKAGING", null, "0", "false")]
        public static BuildTargetResult Package(BuildTargetContext c)
        {
            return c.Success();
        }

        [Target]
        public static BuildTargetResult GenerateVersionBadge(BuildTargetContext c)
        {
            var buildVersion = c.BuildContext.Get<BuildVersion>("BuildVersion");
            var versionSvg = Path.Combine(Dirs.RepoRoot, "resources", "images", "version_badge.svg");
            var outputVersionSvg = c.BuildContext.Get<string>("VersionBadge");

            var versionSvgContent = File.ReadAllText(versionSvg);
            versionSvgContent = versionSvgContent.Replace("ver_number", buildVersion.SimpleVersion);
            File.WriteAllText(outputVersionSvg, versionSvgContent);

            return c.Success();
        }

        [Target]
        public static BuildTargetResult CopyCLISDKLayout(BuildTargetContext c)
        {
            // CLI SDK must be layed out in path which has a Nuget version.
            // But the muxer does not currently support the pre-release CLI SDK without a global.json file.
            // So we are creating a production version.
            // var nugetVersion = c.BuildContext.Get<BuildVersion>("BuildVersion").NuGetVersion;
            var nugetVersion = c.BuildContext.Get<BuildVersion>("BuildVersion").ProductionVersion;

            var cliSdkRoot = Path.Combine(Dirs.Output, "obj", "clisdk");
            var cliSdk = Path.Combine(cliSdkRoot, "sdk", nugetVersion);

            if (Directory.Exists(cliSdkRoot))
            {
                Utils.DeleteDirectory(cliSdkRoot);
            }
            Directory.CreateDirectory(cliSdk);

            var binPath = Path.Combine(Dirs.Stage2, "bin");
            foreach (var file in Directory.GetFiles(binPath, "*", SearchOption.AllDirectories))
            {
                string destFile = file.Replace(binPath, cliSdk);
                Directory.CreateDirectory(Path.GetDirectoryName(destFile));
                File.Copy(file, destFile, true);
            }

            File.Copy(Path.Combine(Dirs.Stage2, ".version"), Path.Combine(cliSdk, ".version"), true);

            // copy stage2 to "cliSdkRoot\bin".
            // this is a temp hack until we fix the build scripts to use the new shared fx and shared host
            // the current build scripts need the CLI sdk to be in the bin folder.

            foreach (var file in Directory.GetFiles(Dirs.Stage2, "*", SearchOption.AllDirectories))
            {
                string destFile = file.Replace(Dirs.Stage2, cliSdkRoot);
                Directory.CreateDirectory(Path.GetDirectoryName(destFile));
                File.Copy(file, destFile, true);
            }

            c.BuildContext["CLISDKRoot"] = cliSdkRoot;
            return c.Success();
        }

        [Target(nameof(PackageTargets.GenerateZip), nameof(PackageTargets.GenerateTarBall))]
        public static BuildTargetResult GenerateCompressedFile(BuildTargetContext c)
        {
            return c.Success();
        }

        [Target(nameof(PackageTargets.InitPackage))]
        [BuildPlatforms(BuildPlatform.Windows)]
        public static BuildTargetResult GenerateZip(BuildTargetContext c)
        {
            CreateZipFromDirectory(c.BuildContext.Get<string>("SharedHostPublishRoot"), c.BuildContext.Get<string>("SharedHostCompressedFile"));
            CreateZipFromDirectory(c.BuildContext.Get<string>("SharedFrameworkPublishRoot"), c.BuildContext.Get<string>("SharedFrameworkCompressedFile"));
            CreateZipFromDirectory(c.BuildContext.Get<string>("CLISDKRoot"), c.BuildContext.Get<string>("SdkCompressedFile"));

            return c.Success();
        }

        [Target(nameof(PackageTargets.InitPackage))]
        [BuildPlatforms(BuildPlatform.Unix)]
        public static BuildTargetResult GenerateTarBall(BuildTargetContext c)
        {
            CreateTarBallFromDirectory(c.BuildContext.Get<string>("SharedHostPublishRoot"), c.BuildContext.Get<string>("SharedHostCompressedFile"));
            CreateTarBallFromDirectory(c.BuildContext.Get<string>("SharedFrameworkPublishRoot"), c.BuildContext.Get<string>("SharedFrameworkCompressedFile"));
            CreateTarBallFromDirectory(c.BuildContext.Get<string>("CLISDKRoot"), c.BuildContext.Get<string>("SdkCompressedFile"));

            return c.Success();
        }

        [Target]
        [BuildPlatforms(BuildPlatform.Windows)]
        public static BuildTargetResult GenerateNugetPackages(BuildTargetContext c)
        {
            var versionSuffix = c.BuildContext.Get<BuildVersion>("BuildVersion").VersionSuffix;
            var env = GetCommonEnvVars(c);
            Cmd("powershell", "-NoProfile", "-NoLogo",
                Path.Combine(Dirs.RepoRoot, "packaging", "nuget", "package.ps1"), Path.Combine(Dirs.Stage2, "bin"), versionSuffix)
                    .Environment(env)
                    .Execute()
                    .EnsureSuccessful();
            return c.Success();
        }

        internal static Dictionary<string, string> GetCommonEnvVars(BuildTargetContext c)
        {
            // Set up the environment variables previously defined by common.sh/ps1
            // This is overkill, but I want to cover all the variables used in all OSes (including where some have the same names)
            var buildVersion = c.BuildContext.Get<BuildVersion>("BuildVersion");
            var configuration = c.BuildContext.Get<string>("Configuration");
            var architecture = PlatformServices.Default.Runtime.RuntimeArchitecture;
            var env = new Dictionary<string, string>()
            {
                { "RID", PlatformServices.Default.Runtime.GetRuntimeIdentifier() },
                { "OSNAME", PlatformServices.Default.Runtime.OperatingSystem },
                { "TFM", "dnxcore50" },
                { "REPOROOT", Dirs.RepoRoot },
                { "OutputDir", Dirs.Output },
                { "Stage1Dir", Dirs.Stage1 },
                { "Stage1CompilationDir", Dirs.Stage1Compilation },
                { "Stage2Dir", Dirs.Stage2 },
                { "STAGE2_DIR", Dirs.Stage2 },
                { "Stage2CompilationDir", Dirs.Stage2Compilation },
                { "HostDir", Dirs.Corehost },
                { "PackageDir", Path.Combine(Dirs.Packages) }, // Legacy name
                { "TestBinRoot", Dirs.TestOutput },
                { "TestPackageDir", Dirs.TestPackages },
                { "MajorVersion", buildVersion.Major.ToString() },
                { "MinorVersion", buildVersion.Minor.ToString() },
                { "PatchVersion", buildVersion.Patch.ToString() },
                { "CommitCountVersion", buildVersion.CommitCountString },
                { "COMMIT_COUNT_VERSION", buildVersion.CommitCountString },
                { "DOTNET_CLI_VERSION", buildVersion.SimpleVersion },
                { "DOTNET_MSI_VERSION", buildVersion.GenerateMsiVersion() },
                { "VersionSuffix", buildVersion.VersionSuffix },
                { "CONFIGURATION", configuration },
                { "ARCHITECTURE", architecture }
            };

            return env;
        }

        private static void CreateZipFromDirectory(string directory, string artifactPath)
        {
            if (File.Exists(artifactPath))
            {
                File.Delete(artifactPath);
            }

            ZipFile.CreateFromDirectory(directory, artifactPath, CompressionLevel.Optimal, false);
        }

        private static void CreateTarBallFromDirectory(string directory, string artifactPath)
        {
            if (File.Exists(artifactPath))
            {
                File.Delete(artifactPath);
            }

            Cmd("tar", "-czf", artifactPath, "-C", directory, ".")
                .Execute()
                .EnsureSuccessful();
        }
    }
}
