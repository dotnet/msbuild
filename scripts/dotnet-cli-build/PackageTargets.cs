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
        public static readonly string[] ProjectsToPack  = new string[]
        {
            "Microsoft.DotNet.Cli.Utils",
            "Microsoft.DotNet.ProjectModel",
            "Microsoft.DotNet.ProjectModel.Loader",
            "Microsoft.DotNet.ProjectModel.Workspaces",
            "Microsoft.DotNet.InternalAbstractions",
            "Microsoft.Extensions.DependencyModel",
            "Microsoft.Extensions.Testing.Abstractions",
            "Microsoft.DotNet.Compiler.Common",
            "Microsoft.DotNet.Files",
            "dotnet-compile-fsc"
        };

        [Target(nameof(PackageTargets.CopyCLISDKLayout),
        nameof(PackageTargets.CopySharedHostLayout),
        nameof(PackageTargets.CopySharedFxLayout),
        nameof(PackageTargets.CopyCombinedFrameworkSDKHostLayout),
        nameof(PackageTargets.CopyCombinedFrameworkHostLayout),
        nameof(PackageTargets.CopyCombinedFrameworkSDKLayout))]
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
        nameof(PackageTargets.GenerateNugetPackages),
        nameof(InstallerTargets.TestInstaller))]
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
            versionSvgContent = versionSvgContent.Replace("ver_number", buildVersion.NuGetVersion);
            File.WriteAllText(outputVersionSvg, versionSvgContent);

            return c.Success();
        }

        [Target]
        public static BuildTargetResult CopyCLISDKLayout(BuildTargetContext c)
        {
            var cliSdkRoot = Path.Combine(Dirs.Output, "obj", "clisdk");
            if (Directory.Exists(cliSdkRoot))
            {
                Utils.DeleteDirectory(cliSdkRoot);
            }

            Directory.CreateDirectory(cliSdkRoot);
            Utils.CopyDirectoryRecursively(Path.Combine(Dirs.Stage2, "sdk"), cliSdkRoot, true);
            FixPermissions(cliSdkRoot);

            c.BuildContext["CLISDKRoot"] = cliSdkRoot;
            return c.Success();
        }

        [Target]
        public static BuildTargetResult CopySharedHostLayout(BuildTargetContext c)
        {
            var sharedHostRoot = Path.Combine(Dirs.Output, "obj", "sharedHost");
            if (Directory.Exists(sharedHostRoot))
            {
                Utils.DeleteDirectory(sharedHostRoot);
            }

            Directory.CreateDirectory(sharedHostRoot);

            foreach (var file in Directory.GetFiles(Dirs.Stage2, "*", SearchOption.TopDirectoryOnly))
            {
                var destFile = file.Replace(Dirs.Stage2, sharedHostRoot);
                File.Copy(file, destFile, true);
            }
            FixPermissions(sharedHostRoot);

            c.BuildContext["SharedHostPublishRoot"] = sharedHostRoot;
            return c.Success();
        }

        [Target]
        public static BuildTargetResult CopySharedFxLayout(BuildTargetContext c)
        {
            var sharedFxRoot = Path.Combine(Dirs.Output, "obj", "sharedFx");
            if (Directory.Exists(sharedFxRoot))
            {
                Utils.DeleteDirectory(sharedFxRoot);
            }

            Directory.CreateDirectory(sharedFxRoot);
            Utils.CopyDirectoryRecursively(Path.Combine(Dirs.Stage2, "shared"), sharedFxRoot, true);
            FixPermissions(sharedFxRoot);

            c.BuildContext["SharedFrameworkPublishRoot"] = sharedFxRoot;
            return c.Success();
        }

        [Target]
        public static BuildTargetResult CopyCombinedFrameworkSDKHostLayout(BuildTargetContext c)
        {
            var combinedRoot = Path.Combine(Dirs.Output, "obj", "combined-framework-sdk-host");
            if (Directory.Exists(combinedRoot))
            {
                Utils.DeleteDirectory(combinedRoot);
            }

            string sdkPublishRoot = c.BuildContext.Get<string>("CLISDKRoot");
            Utils.CopyDirectoryRecursively(sdkPublishRoot, combinedRoot);

            string sharedFrameworkPublishRoot = c.BuildContext.Get<string>("SharedFrameworkPublishRoot");
            Utils.CopyDirectoryRecursively(sharedFrameworkPublishRoot, combinedRoot);

            string sharedHostPublishRoot = c.BuildContext.Get<string>("SharedHostPublishRoot");
            Utils.CopyDirectoryRecursively(sharedHostPublishRoot, combinedRoot);

            c.BuildContext["CombinedFrameworkSDKHostRoot"] = combinedRoot;
            return c.Success();
        }

        [Target]
        public static BuildTargetResult CopyCombinedFrameworkHostLayout(BuildTargetContext c)
        {
            var combinedRoot = Path.Combine(Dirs.Output, "obj", "combined-framework-host");
            if (Directory.Exists(combinedRoot))
            {
                Utils.DeleteDirectory(combinedRoot);
            }


            string sharedFrameworkPublishRoot = c.BuildContext.Get<string>("SharedFrameworkPublishRoot");
            Utils.CopyDirectoryRecursively(sharedFrameworkPublishRoot, combinedRoot);

            string sharedHostPublishRoot = c.BuildContext.Get<string>("SharedHostPublishRoot");
            Utils.CopyDirectoryRecursively(sharedHostPublishRoot, combinedRoot);

            c.BuildContext["CombinedFrameworkHostRoot"] = combinedRoot;
            return c.Success();
        }

        [Target]
        public static BuildTargetResult CopyCombinedFrameworkSDKLayout(BuildTargetContext c)
        {
            var combinedRoot = Path.Combine(Dirs.Output, "obj", "combined-framework-sdk");
            if (Directory.Exists(combinedRoot))
            {
                Utils.DeleteDirectory(combinedRoot);
            }

            string sdkPublishRoot = c.BuildContext.Get<string>("CLISDKRoot");
            Utils.CopyDirectoryRecursively(sdkPublishRoot, combinedRoot);

            string sharedFrameworkPublishRoot = c.BuildContext.Get<string>("SharedFrameworkPublishRoot");
            Utils.CopyDirectoryRecursively(sharedFrameworkPublishRoot, combinedRoot);

            c.BuildContext["CombinedFrameworkSDKRoot"] = combinedRoot;
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
            CreateZipFromDirectory(c.BuildContext.Get<string>("CombinedFrameworkSDKHostRoot"), c.BuildContext.Get<string>("CombinedFrameworkSDKHostCompressedFile"));
            CreateZipFromDirectory(c.BuildContext.Get<string>("CombinedFrameworkHostRoot"), c.BuildContext.Get<string>("CombinedFrameworkHostCompressedFile"));
            CreateZipFromDirectory(c.BuildContext.Get<string>("CombinedFrameworkSDKRoot"), c.BuildContext.Get<string>("CombinedFrameworkSDKCompressedFile"));
            CreateZipFromDirectory(Path.Combine(Dirs.Stage2Symbols, "sdk"), c.BuildContext.Get<string>("SdkSymbolsCompressedFile"));

            return c.Success();
        }

        [Target(nameof(PackageTargets.InitPackage))]
        [BuildPlatforms(BuildPlatform.Unix)]
        public static BuildTargetResult GenerateTarBall(BuildTargetContext c)
        {
            CreateTarBallFromDirectory(c.BuildContext.Get<string>("CombinedFrameworkSDKHostRoot"), c.BuildContext.Get<string>("CombinedFrameworkSDKHostCompressedFile"));
            CreateTarBallFromDirectory(c.BuildContext.Get<string>("CombinedFrameworkHostRoot"), c.BuildContext.Get<string>("CombinedFrameworkHostCompressedFile"));

            CreateTarBallFromDirectory(Path.Combine(Dirs.Stage2Symbols, "sdk"), c.BuildContext.Get<string>("SdkSymbolsCompressedFile"));

            return c.Success();
        }

        [Target]
        public static BuildTargetResult GenerateNugetPackages(BuildTargetContext c)
        {
            var versionSuffix = c.BuildContext.Get<BuildVersion>("BuildVersion").VersionSuffix;
            var configuration = c.BuildContext.Get<string>("Configuration");

            var env = GetCommonEnvVars(c);
            var dotnet = DotNetCli.Stage2;

            var packagingBuildBasePath = Path.Combine(Dirs.Stage2Compilation, "forPackaging");

            FS.Mkdirp(Dirs.PackagesIntermediate);
            FS.Mkdirp(Dirs.Packages);

            foreach (var projectName in ProjectsToPack)
            {
                var projectFile = Path.Combine(Dirs.RepoRoot, "src", projectName, "project.json");

                dotnet.Pack(
                    projectFile, 
                    "--no-build", 
                    "--build-base-path", packagingBuildBasePath, 
                    "--output", Dirs.PackagesIntermediate, 
                    "--configuration", configuration, 
                    "--version-suffix", versionSuffix)
                    .Execute()
                    .EnsureSuccessful();
            }

            var packageFiles = Directory.EnumerateFiles(Dirs.PackagesIntermediate, "*.nupkg");

            foreach (var packageFile in packageFiles)
            {
                if (!packageFile.EndsWith(".symbols.nupkg"))
                {
                    var destinationPath = Path.Combine(Dirs.Packages, Path.GetFileName(packageFile));
                    File.Copy(packageFile, destinationPath, overwrite: true);
                }
            }

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

        private static void FixPermissions(string directory)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Reset everything to user readable/writeable and group and world readable.
                FS.ChmodAll(directory, "*", "644");

                // Now make things that should be executable, executable.
                FS.FixModeFlags(directory);
            }
        }
    }
}
