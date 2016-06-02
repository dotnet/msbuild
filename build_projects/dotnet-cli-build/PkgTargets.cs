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
    public class PkgTargets
    {
        public static string PkgsIntermediateDir { get; set; }
        public static string SharedHostComponentId { get; set; }
        public static string SharedFxComponentId { get; set; }
        public static string SharedFxPkgId { get; set; }
        public static string SharedFrameworkNugetVersion { get; set; }
        public static string CLISdkComponentId { get; set; }
        public static string CLISdkPkgId { get; set; }
        public static string CLISdkNugetVersion { get; set; }

        [Target]
        [BuildPlatforms(BuildPlatform.OSX)]
        public static BuildTargetResult InitPkg(BuildTargetContext c)
        {
            PkgsIntermediateDir = Path.Combine(Dirs.Packages, "intermediate");
            Directory.CreateDirectory(PkgsIntermediateDir);

            SharedHostComponentId = $"com.microsoft.dotnet.sharedhost.component.osx.x64";

            string sharedFrameworkNugetName = Monikers.SharedFrameworkName;
            SharedFrameworkNugetVersion = CliDependencyVersions.SharedFrameworkVersion;
            SharedFxComponentId = $"com.microsoft.dotnet.sharedframework.{sharedFrameworkNugetName}.{SharedFrameworkNugetVersion}.component.osx.x64";
            SharedFxPkgId = $"com.microsoft.dotnet.{sharedFrameworkNugetName}.{SharedFrameworkNugetVersion}.osx.x64";

            CLISdkNugetVersion = c.BuildContext.Get<BuildVersion>("BuildVersion").NuGetVersion;
            CLISdkComponentId = $"com.microsoft.dotnet.dev.{CLISdkNugetVersion}.component.osx.x64";
            CLISdkPkgId = $"com.microsoft.dotnet.dev.{CLISdkNugetVersion}.osx.x64";

            return c.Success();
        }

        [Target(nameof(InitPkg), nameof(GenerateCLISdkProductArchive))]
        [BuildPlatforms(BuildPlatform.OSX)]
        public static BuildTargetResult GeneratePkgs(BuildTargetContext c)
        {
            return c.Success();
        }

        [Target(nameof(GenerateCLISdkPkg))]
        [BuildPlatforms(BuildPlatform.OSX)]
        public static BuildTargetResult GenerateCLISdkProductArchive(BuildTargetContext c)
        {
            string resourcePath = Path.Combine(Dirs.RepoRoot, "packaging", "osx", "clisdk", "resources");
            string outFilePath = Path.Combine(Dirs.Packages, c.BuildContext.Get<string>("CombinedFrameworkSDKHostInstallerFile"));

            // Copy SharedFX and host installers in the correct place
            var sharedFrameworkPkgIntermediatePath = Path.Combine(PkgsIntermediateDir, $"{SharedFxComponentId}.pkg");
            var sharedHostPkgIntermediatePath = Path.Combine(PkgsIntermediateDir, $"{SharedHostComponentId}.pkg");

            File.Copy(c.BuildContext.Get<string>("SharedFrameworkInstallerFile"), sharedFrameworkPkgIntermediatePath, true);
            File.Copy(c.BuildContext.Get<string>("SharedHostInstallerFile"), sharedHostPkgIntermediatePath, true);


            string inputDistTemplatePath = Path.Combine(
                Dirs.RepoRoot,
                "packaging",
                "osx",
                "clisdk",
                "Distribution-Template");
            string distTemplate = File.ReadAllText(inputDistTemplatePath);
            string distributionPath = Path.Combine(PkgsIntermediateDir, "CLI-SDK-Formatted-Distribution-Template.xml");
            string formattedDistContents =
                distTemplate.Replace("{SharedFxComponentId}", SharedFxComponentId)
                .Replace("{SharedHostComponentId}", SharedHostComponentId)
                .Replace("{CLISdkComponentId}", CLISdkComponentId)
                .Replace("{CLISdkNugetVersion}", CLISdkNugetVersion)
                .Replace("{CLISdkBrandName}", Monikers.CLISdkBrandName)
                .Replace("{SharedFxBrandName}", Monikers.SharedFxBrandName)
                .Replace("{SharedHostBrandName}", Monikers.SharedHostBrandName);
            File.WriteAllText(distributionPath, formattedDistContents);

            Cmd("productbuild",
                "--version", CLISdkNugetVersion,
                "--identifier", CLISdkPkgId,
                "--package-path", PkgsIntermediateDir,
                "--resources", resourcePath,
                "--distribution", distributionPath,
                outFilePath)
            .Execute()
            .EnsureSuccessful();

            return c.Success();
        }

        [Target]
        [BuildPlatforms(BuildPlatform.OSX)]
        public static BuildTargetResult GenerateCLISdkPkg(BuildTargetContext c)
        {
            string outFilePath = Path.Combine(PkgsIntermediateDir, CLISdkComponentId + ".pkg");
            string installLocation = "/usr/local/share/dotnet";
            string scriptsLocation = Path.Combine(Dirs.RepoRoot, "packaging", "osx", "clisdk", "scripts");

            Cmd("pkgbuild",
                "--root", c.BuildContext.Get<string>("CLISDKRoot"),
                "--identifier", CLISdkComponentId,
                "--version", CLISdkNugetVersion,
                "--install-location", installLocation,
                "--scripts", scriptsLocation,
                outFilePath)
                .Execute()
                .EnsureSuccessful();

            return c.Success();
        }
    }
}
