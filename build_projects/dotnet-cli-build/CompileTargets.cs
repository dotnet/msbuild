using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.DotNet.Cli.Build.Framework;
using Microsoft.DotNet.InternalAbstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static Microsoft.DotNet.Cli.Build.Framework.BuildHelpers;
using static Microsoft.DotNet.Cli.Build.FS;

namespace Microsoft.DotNet.Cli.Build
{
    public class CompileTargets
    {
        public static readonly bool IsWinx86 = CurrentPlatform.IsWindows && CurrentArchitecture.Isx86;

        public static readonly string[] BinariesForCoreHost = new[]
        {
            "csc"
        };

        public static readonly string[] ProjectsToPublish = new[]
        {
            "dotnet"
        };

        public static readonly string[] FilesToClean = new[]
        {
            "vbc.exe"
        };

        public static string HostPackagePlatformRid => HostPackageSupportedRids[
                             (RuntimeEnvironment.OperatingSystemPlatform == Platform.Windows)
                             ? $"win7-{RuntimeEnvironment.RuntimeArchitecture}"
                             : RuntimeEnvironment.GetRuntimeIdentifier()];

        public static readonly Dictionary<string, string> HostPackageSupportedRids = new Dictionary<string, string>()
        {
            // Key: Current platform RID. Value: The actual publishable (non-dummy) package name produced by the build system for this RID.
            { "win7-x64", "win7-x64" },
            { "win7-x86", "win7-x86" },
            { "osx.10.10-x64", "osx.10.10-x64" },
            { "osx.10.11-x64", "osx.10.10-x64" },
            { "ubuntu.14.04-x64", "ubuntu.14.04-x64" },
            { "centos.7-x64", "rhel.7-x64" },
            { "rhel.7-x64", "rhel.7-x64" },
            { "rhel.7.2-x64", "rhel.7-x64" },
            { "debian.8-x64", "debian.8-x64" }
        };

        public const string SharedFrameworkName = "Microsoft.NETCore.App";

        public static Crossgen CrossgenUtil = new Crossgen(DependencyVersions.CoreCLRVersion);

        // Updates the stage 2 with recent changes.
        [Target(nameof(PrepareTargets.Init), nameof(CompileStage2))]
        public static BuildTargetResult UpdateBuild(BuildTargetContext c)
        {
            return c.Success();
        }

        [Target(nameof(PrepareTargets.Init), nameof(CompileStage1), nameof(CompileStage2))]
        public static BuildTargetResult Compile(BuildTargetContext c)
        {
            return c.Success();
        }

        [Target(nameof(PrepareTargets.Init))]
        public static BuildTargetResult CompileStage1(BuildTargetContext c)
        {
            CleanBinObj(c, Path.Combine(c.BuildContext.BuildDirectory, "src"));

            if (Directory.Exists(Dirs.Stage1))
            {
                Utils.DeleteDirectory(Dirs.Stage1);
            }
            Directory.CreateDirectory(Dirs.Stage1);

            var result = CompileCliSdk(c,
                dotnet: DotNetCli.Stage0,
                rootOutputDirectory: Dirs.Stage1);

            CleanOutputDir(Path.Combine(Dirs.Stage1, "sdk"));
            FS.CopyRecursive(Dirs.Stage1, Dirs.Stage1Symbols);

            RemovePdbsFromDir(Path.Combine(Dirs.Stage1, "sdk"));

            return result;
        }

        [Target(nameof(PrepareTargets.Init))]
        public static BuildTargetResult CompileStage2(BuildTargetContext c)
        {
            var configuration = c.BuildContext.Get<string>("Configuration");

            CleanBinObj(c, Path.Combine(c.BuildContext.BuildDirectory, "src"));

            if (Directory.Exists(Dirs.Stage2))
            {
                Utils.DeleteDirectory(Dirs.Stage2);
            }
            Directory.CreateDirectory(Dirs.Stage2);

            var result = CompileCliSdk(c,
                dotnet: DotNetCli.Stage1,
                rootOutputDirectory: Dirs.Stage2);

            if (!result.Success)
            {
                return result;
            }

            if (CurrentPlatform.IsWindows)
            {
                // build projects for nuget packages
                var packagingOutputDir = Path.Combine(Dirs.Stage2Compilation, "forPackaging");
                Mkdirp(packagingOutputDir);
                foreach (var project in PackageTargets.ProjectsToPack)
                {
                    // Just build them, we'll pack later
                    var packBuildResult = DotNetCli.Stage1.Build(
                        "--build-base-path",
                        packagingOutputDir,
                        "--configuration",
                        configuration,
                        Path.Combine(c.BuildContext.BuildDirectory, "src", project))
                        .Execute();

                    packBuildResult.EnsureSuccessful();
                }
            }

            CleanOutputDir(Path.Combine(Dirs.Stage2, "sdk"));
            FS.CopyRecursive(Dirs.Stage2, Dirs.Stage2Symbols);

            RemovePdbsFromDir(Path.Combine(Dirs.Stage2, "sdk"));

            return c.Success();
        }

        private static void CleanOutputDir(string directory)
        {
            foreach (var file in FilesToClean)
            {
                FS.RmFilesInDirRecursive(directory, file);
            }
        }

        private static void RemovePdbsFromDir(string directory)
        {
            FS.RmFilesInDirRecursive(directory, "*.pdb");
        }

        private static BuildTargetResult CompileCliSdk(BuildTargetContext c, DotNetCli dotnet, string rootOutputDirectory)
        {
            var configuration = c.BuildContext.Get<string>("Configuration");
            var buildVersion = c.BuildContext.Get<BuildVersion>("BuildVersion");
            var srcDir = Path.Combine(c.BuildContext.BuildDirectory, "src");
            var sdkOutputDirectory = Path.Combine(rootOutputDirectory, "sdk", buildVersion.NuGetVersion);

            CopySharedFramework(Dirs.SharedFrameworkPublish, rootOutputDirectory);

            FS.CleanBinObj(c, srcDir);
            Rmdir(sdkOutputDirectory);
            Mkdirp(sdkOutputDirectory);

            foreach (var project in ProjectsToPublish)
            {
                dotnet.Publish(
                    "--native-subdirectory",
                    "--output", sdkOutputDirectory,
                    "--configuration", configuration,
                    "--version-suffix", buildVersion.CommitCountString,
                    Path.Combine(srcDir, project))
                    .Execute()
                    .EnsureSuccessful();
            }

            FixModeFlags(sdkOutputDirectory);

            string compilersProject = Path.Combine(Dirs.RepoRoot, "src", "compilers");
            dotnet.Publish(compilersProject,
                    "--output",
                    sdkOutputDirectory,
                    "--framework",
                    "netstandard1.5")
                    .Execute()
                    .EnsureSuccessful();

            var compilersDeps = Path.Combine(sdkOutputDirectory, "compilers.deps.json");
            var compilersRuntimeConfig = Path.Combine(sdkOutputDirectory, "compilers.runtimeconfig.json");


            var binaryToCorehostifyRelDir = Path.Combine("runtimes", "any", "native");
            var binaryToCorehostifyOutDir = Path.Combine(sdkOutputDirectory, binaryToCorehostifyRelDir);
            // Corehostify binaries
            foreach (var binaryToCorehostify in BinariesForCoreHost)
            {
                try
                {
                    // Yes, it is .exe even on Linux. This is the managed exe we're working with
                    File.Copy(Path.Combine(binaryToCorehostifyOutDir, $"{binaryToCorehostify}.exe"), Path.Combine(sdkOutputDirectory, $"{binaryToCorehostify}.dll"));
                    File.Delete(Path.Combine(binaryToCorehostifyOutDir, $"{binaryToCorehostify}.exe"));
                    var binaryToCoreHostifyDeps = Path.Combine(sdkOutputDirectory, binaryToCorehostify + ".deps.json");

                    File.Copy(compilersDeps, Path.Combine(sdkOutputDirectory, binaryToCorehostify + ".deps.json"));
                    File.Copy(compilersRuntimeConfig, Path.Combine(sdkOutputDirectory, binaryToCorehostify + ".runtimeconfig.json"));
                    PublishMutationUtilties.ChangeEntryPointLibraryName(binaryToCoreHostifyDeps, binaryToCorehostify);
                    foreach (var binaryToRemove in new string[] { "csc", "vbc" })
                    {
                        var assetPath = Path.Combine(binaryToCorehostifyRelDir, $"{binaryToRemove}.exe").Replace(Path.DirectorySeparatorChar, '/');
                        RemoveAssetFromDepsPackages(binaryToCoreHostifyDeps, "runtimeTargets", assetPath);
                    }
                }
                catch (Exception ex)
                {
                    return c.Failed($"Failed to corehostify '{binaryToCorehostify}': {ex.ToString()}");
                }
            }

            // cleanup compilers project output we don't need
            PublishMutationUtilties.CleanPublishOutput(
                sdkOutputDirectory, 
                "compilers", 
                deleteRuntimeConfigJson: true, 
                deleteDepsJson: true);

            // Crossgen SDK directory
            var sharedFrameworkNugetVersion = DependencyVersions.SharedFrameworkVersion;
            var sharedFrameworkNameVersionPath = SharedFrameworkPublisher.GetSharedFrameworkPublishPath(
                rootOutputDirectory,
                sharedFrameworkNugetVersion);
            
            // Copy Host to SDK Directory
            File.Copy(
                Path.Combine(sharedFrameworkNameVersionPath, HostArtifactNames.DotnetHostBaseName), 
                Path.Combine(sdkOutputDirectory, $"corehost{Constants.ExeSuffix}"),
                overwrite: true);
            File.Copy(
                Path.Combine(sharedFrameworkNameVersionPath, HostArtifactNames.DotnetHostFxrBaseName),
                Path.Combine(sdkOutputDirectory, HostArtifactNames.DotnetHostFxrBaseName), 
                overwrite: true);
            File.Copy(
                Path.Combine(sharedFrameworkNameVersionPath, HostArtifactNames.HostPolicyBaseName), 
                Path.Combine(sdkOutputDirectory, HostArtifactNames.HostPolicyBaseName), 
                overwrite: true);
            
            CrossgenUtil.CrossgenDirectory(
                sharedFrameworkNameVersionPath,
                sdkOutputDirectory);

            // Generate .version file
            var version = buildVersion.NuGetVersion;
            var content = $@"{c.BuildContext["CommitHash"]}{Environment.NewLine}{version}{Environment.NewLine}";
            File.WriteAllText(Path.Combine(sdkOutputDirectory, ".version"), content);

            return c.Success();
        }

        private static void RemoveAssetFromDepsPackages(string depsFile, string sectionName, string assetPath)
        {
            JToken deps;
            using (var file = File.OpenText(depsFile))
            using (JsonTextReader reader = new JsonTextReader(file))
            {
                deps = JObject.ReadFrom(reader);
            }

            foreach (JProperty target in deps["targets"])
            {
                foreach (JProperty pv in target.Value.Children<JProperty>())
                {
                    var section = pv.Value[sectionName];
                    if (section != null)
                    {
                        foreach (JProperty relPath in section)
                        {
                            if (assetPath.Equals(relPath.Name))
                            {
                                relPath.Remove();
                                break;
                            }
                        }
                    }
                }
            }
            using (var file = File.CreateText(depsFile))
            using (var writer = new JsonTextWriter(file) { Formatting = Formatting.Indented })
            {
                deps.WriteTo(writer);
            }
        }

        private static void CopySharedFramework(string sharedFrameworkPublish, string rootOutputDirectory)
        {
            CopyRecursive(sharedFrameworkPublish, rootOutputDirectory);
        }
    }
}
