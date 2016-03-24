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
    public class PkgTargets
    {
        public static string PkgsIntermediateDir { get; set; }
        [Target]
        [BuildPlatforms(BuildPlatform.OSX)]
        public static BuildTargetResult InitPkg(BuildTargetContext c)
        {
            PkgsIntermediateDir = Path.Combine(Dirs.Packages, "intermediate");
            Directory.CreateDirectory(PkgsIntermediateDir);
            return c.Success();
        }

        [Target(nameof(InitPkg), nameof(GenerateSharedFrameworkProductArchive), nameof(GenerateCLISdkProductArchive))]
        [BuildPlatforms(BuildPlatform.OSX)]
        public static BuildTargetResult GeneratePkgs(BuildTargetContext c)
        {
            return c.Success();
        }

        [Target(nameof(GenerateCLISdkPkg))]
        [BuildPlatforms(BuildPlatform.OSX)]
        public static BuildTargetResult GenerateCLISdkProductArchive(BuildTargetContext c)
        {
            string sharedFrameworkNugetVersion = c.BuildContext.Get<string>("SharedFrameworkNugetVersion");
            string version = c.BuildContext.Get<BuildVersion>("BuildVersion").SimpleVersion;
            string id = $"com.microsoft.dotnet.dev.{version}.osx.x64";
            string resourcePath = Path.Combine(Dirs.RepoRoot, "packaging", "osx", "clisdk", "resources");
            string outFilePath = Path.Combine(Dirs.Packages, c.BuildContext.Get<string>("CombinedFrameworkSDKHostInstallerFile"));

            string inputDistTemplatePath = Path.Combine(
                Dirs.RepoRoot,
                "packaging",
                "osx",
                "clisdk",
                "Distribution-Template");
            string distTemplate = File.ReadAllText(inputDistTemplatePath);
            string distributionPath = Path.Combine(PkgsIntermediateDir, "CLI-SDK-Formatted-Distribution-Template.xml");
            string formattedDistContents =
                distTemplate.Replace("{SharedFrameworkNugetVersion}", sharedFrameworkNugetVersion)
                .Replace("{SharedFrameworkNugetName}", Monikers.SharedFrameworkName)
                .Replace("{VERSION}", version);
            File.WriteAllText(distributionPath, formattedDistContents);

            Cmd("productbuild",
                "--version", version,
                "--identifier", id,
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
            string version = c.BuildContext.Get<BuildVersion>("BuildVersion").SimpleVersion;
            string id = $"com.microsoft.dotnet.sdk.osx.x64";
            string outFilePath = Path.Combine(PkgsIntermediateDir, id + ".pkg");
            string installLocation = "/usr/local/share/dotnet";
            string scriptsLocation = Path.Combine(Dirs.RepoRoot, "packaging", "osx", "clisdk", "scripts");

            Cmd("pkgbuild",
                "--root", c.BuildContext.Get<string>("CLISDKRoot"),
                "--identifier", id,
                "--version", version,
                "--install-location", installLocation,
                "--scripts", scriptsLocation,
                outFilePath)
                .Execute()
                .EnsureSuccessful();

            return c.Success();
        }

        [Target(nameof(GenerateSharedFrameworkPkg), nameof(GenerateSharedHostPkg))]
        [BuildPlatforms(BuildPlatform.OSX)]
        public static BuildTargetResult GenerateSharedFrameworkProductArchive(BuildTargetContext c)
        {
            string sharedFrameworkNugetName = Monikers.SharedFrameworkName;
            string sharedFrameworkNugetVersion = c.BuildContext.Get<string>("SharedFrameworkNugetVersion");
            string version = c.BuildContext.Get<BuildVersion>("BuildVersion").SimpleVersion;
            string id = $"com.microsoft.dotnet.{sharedFrameworkNugetName}.{sharedFrameworkNugetVersion}.osx.x64";
            string resourcePath = Path.Combine(Dirs.RepoRoot, "packaging", "osx", "sharedframework", "resources");
            string outFilePath = Path.Combine(PkgsIntermediateDir, c.BuildContext.Get<string>("CombinedFrameworkHostInstallerFile"));

            string inputDistTemplatePath = Path.Combine(
                Dirs.RepoRoot,
                "packaging",
                "osx",
                "sharedframework",
                "shared-framework-distribution-template.xml");
            string distTemplate = File.ReadAllText(inputDistTemplatePath);
            string distributionPath = Path.Combine(PkgsIntermediateDir, "shared-framework-formatted-distribution.xml");
            string formattedDistContents =
                distTemplate.Replace("{SharedFrameworkNugetVersion}", sharedFrameworkNugetVersion)
                .Replace("{SharedFrameworkNugetName}", Monikers.SharedFrameworkName)
                .Replace("{VERSION}", version);
            File.WriteAllText(distributionPath, formattedDistContents);

            Cmd("productbuild",
                "--version", version,
                "--identifier", id,
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
        public static BuildTargetResult GenerateSharedFrameworkPkg(BuildTargetContext c)
        {
            string sharedFrameworkNugetName = Monikers.SharedFrameworkName;
            string sharedFrameworkNugetVersion = c.BuildContext.Get<string>("SharedFrameworkNugetVersion");
            string version = c.BuildContext.Get<BuildVersion>("BuildVersion").SimpleVersion;
            string id = $"com.microsoft.dotnet.sharedframework.{sharedFrameworkNugetName}.{sharedFrameworkNugetVersion}.component.osx.x64";
            string outFilePath = Path.Combine(PkgsIntermediateDir, id + ".pkg");
            string installLocation = "/usr/local/share/dotnet";
            string scriptsLocation = Path.Combine(Dirs.RepoRoot, "packaging", "osx", "sharedframework", "scripts");

            Cmd("pkgbuild",
                "--root", c.BuildContext.Get<string>("SharedFrameworkPublishRoot"),
                "--identifier", id,
                "--version", version,
                "--install-location", installLocation,
                "--scripts", scriptsLocation,
                outFilePath)
                .Execute()
                .EnsureSuccessful();

            return c.Success();
        }

        [Target]
        [BuildPlatforms(BuildPlatform.OSX)]
        public static BuildTargetResult GenerateSharedHostPkg(BuildTargetContext c)
        {
            string version = c.BuildContext.Get<BuildVersion>("BuildVersion").SimpleVersion;
            string id = $"com.microsoft.dotnet.sharedhost.osx.x64";
            string outFilePath = Path.Combine(PkgsIntermediateDir, id + ".pkg");
            string installLocation = "/usr/local/share/dotnet";
            string scriptsLocation = Path.Combine(Dirs.RepoRoot, "packaging", "osx", "sharedhost", "scripts");

            Cmd("pkgbuild",
                "--root", c.BuildContext.Get<string>("SharedHostPublishRoot"),
                "--identifier", id,
                "--version", version,
                "--install-location", installLocation,
                "--scripts", scriptsLocation,
                outFilePath)
                .Execute()
                .EnsureSuccessful();

            return c.Success();
        }
    }
}
