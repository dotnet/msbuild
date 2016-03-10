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
        [Target(nameof(GenerateSdkProductArchive), nameof(GenerateSharedFrameworkProductArchive))]
        [BuildPlatforms(BuildPlatform.OSX)]
        public static BuildTargetResult GeneratePkgs(BuildTargetContext c)
        {
            return c.Success();
        }

        [Target]
        [BuildPlatforms(BuildPlatform.OSX)]
        public static BuildTargetResult GenerateSdkProductArchive(BuildTargetContext c)
        {
            var version = c.BuildContext.Get<BuildVersion>("BuildVersion").SimpleVersion;
            var pkg = c.BuildContext.Get<string>("SdkInstallerFile");

            Cmd(Path.Combine(Dirs.RepoRoot, "packaging", "osx", "package-osx.sh"),
                    "-v", version, "-i", Dirs.Stage2, "-o", pkg)
                    .Execute()
                    .EnsureSuccessful();
            return c.Success();
        }

        [Target(nameof(GenerateSharedFrameworkPkg), nameof(GenerateSharedHostPkg))]
        [BuildPlatforms(BuildPlatform.OSX)]
        public static BuildTargetResult GenerateSharedFrameworkProductArchive(BuildTargetContext c)
        {
            string sharedFrameworkNugetName = SharedFrameworkTargets.SharedFrameworkName;
            string sharedFrameworkNugetVersion = c.BuildContext.Get<string>("SharedFrameworkNugetVersion");
            string version = c.BuildContext.Get<BuildVersion>("BuildVersion").SimpleVersion;
            string id = $"com.microsoft.dotnet.sharedframework.{sharedFrameworkNugetName}.{sharedFrameworkNugetVersion}.osx.x64";
            string packageIntermediatesPath = Path.Combine(Dirs.Output, "obj", "pkg");
            string resourcePath = Path.Combine(Dirs.RepoRoot, "packaging", "osx", "resources");
            string outFilePath = Path.Combine(packageIntermediatesPath, id + ".pkg");

            string inputDistTemplatePath = Path.Combine(
                Dirs.RepoRoot,
                "packaging",
                "osx",
                "sharedframework",
                "shared-framework-distribution-template.xml");
            string distTemplate = File.ReadAllText(inputDistTemplatePath);
            string distributionPath = Path.Combine(packageIntermediatesPath, "shared-framework-formatted-distribution.xml");
            string formattedDistContents = 
                distTemplate.Replace("{SharedFrameworkNugetVersion}", sharedFrameworkNugetVersion)
                .Replace("{SharedFrameworkNugetName}", SharedFrameworkTargets.SharedFrameworkName)
                .Replace("{VERSION}", version);
            File.WriteAllText(distributionPath, formattedDistContents);

            Cmd("productbuild",
                "--version", version,
                "--identifier", id,
                "--package-path", packageIntermediatesPath,
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
            string sharedFrameworkNugetName = SharedFrameworkTargets.SharedFrameworkName;
            string sharedFrameworkNugetVersion = c.BuildContext.Get<string>("SharedFrameworkNugetVersion");
            Directory.CreateDirectory(Path.Combine(Dirs.Output, "obj", "pkg"));
            string version = c.BuildContext.Get<BuildVersion>("BuildVersion").SimpleVersion;
            string id = $"com.microsoft.dotnet.sharedframework.{sharedFrameworkNugetName}.{sharedFrameworkNugetVersion}.component.osx.x64";
            string outFilePath = Path.Combine(Dirs.Output, "obj", "pkg", id + ".pkg");
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
            Directory.CreateDirectory(Path.Combine(Dirs.Output, "obj", "pkg"));
            string version = c.BuildContext.Get<BuildVersion>("BuildVersion").SimpleVersion;
            string id = $"com.microsoft.dotnet.sharedhost.osx.x64";
            string outFilePath = Path.Combine(Dirs.Output, "obj", "pkg", id + ".pkg");
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