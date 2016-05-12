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
        public static readonly string CoreCLRVersion = "1.0.2-rc2-24027";
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

        public static Crossgen CrossgenUtil = new Crossgen(CoreCLRVersion);

        private static string DotnetHostBaseName => $"dotnet{Constants.ExeSuffix}";
        private static string DotnetHostFxrBaseName => $"{Constants.DynamicLibPrefix}hostfxr{Constants.DynamicLibSuffix}";
        private static string HostPolicyBaseName => $"{Constants.DynamicLibPrefix}hostpolicy{Constants.DynamicLibSuffix}";

        // Updates the stage 2 with recent changes.
        [Target(nameof(PrepareTargets.Init), nameof(CompileStage2))]
        public static BuildTargetResult UpdateBuild(BuildTargetContext c)
        {
            return c.Success();
        }

        // Moving PrepareTargets.RestorePackages after PackagePkgProjects because managed code depends on the
        // Microsoft.NETCore.App package that is created during PackagePkgProjects.
        [Target(nameof(PrepareTargets.Init), nameof(CompileCoreHost), nameof(PackagePkgProjects), nameof(RestoreLockedCoreHost), nameof(PrepareTargets.RestorePackages), nameof(CompileStage1), nameof(CompileStage2))]
        public static BuildTargetResult Compile(BuildTargetContext c)
        {
            return c.Success();
        }

        // We need to generate stub host packages so we can restore our standalone test assets against the metapackage 
        // we built earlier in the build
        // https://github.com/dotnet/cli/issues/2438
        [Target]
        public static BuildTargetResult GenerateStubHostPackages(BuildTargetContext c)
        {
            var hostVersion = c.BuildContext.Get<HostVersion>("HostVersion");
            var currentRid = HostPackagePlatformRid;
            PrepareDummyRuntimeNuGetPackage(DotNetCli.Stage0);
            foreach (var hostPackage in hostVersion.LatestHostPackages)
            {
                foreach (var rid in HostPackageSupportedRids.Values.Distinct())
                {
                    if (!rid.Equals(currentRid))
                    {
                        CreateDummyRuntimeNuGetPackage(
                            DotNetCli.Stage0,
                            hostPackage.Key,
                            rid,
                            hostPackage.Value,
                            Dirs.CorehostDummyPackages);
                    }
                }
            }
            return c.Success();
        }

        [Target(nameof(PrepareTargets.Init))]
        public static BuildTargetResult RestoreLockedCoreHost(BuildTargetContext c)
        {
            var hostVersion = c.BuildContext.Get<HostVersion>("HostVersion");
            var lockedHostFxrVersion = hostVersion.LockedHostFxrVersion;
            var currentRid = HostPackagePlatformRid;
            string projectJson = $@"{{
  ""dependencies"": {{
      ""Microsoft.NETCore.DotNetHostResolver"" : ""{lockedHostFxrVersion}""
  }},
  ""frameworks"": {{
      ""netcoreapp1.0"": {{}}
  }},
  ""runtimes"": {{
      ""{currentRid}"": {{}}
  }}
}}";
            var tempPjDirectory = Path.Combine(Dirs.Intermediate, "lockedHostTemp");
            FS.Rmdir(tempPjDirectory);
            Directory.CreateDirectory(tempPjDirectory);
            var tempPjFile = Path.Combine(tempPjDirectory, "project.json");
            File.WriteAllText(tempPjFile, projectJson);

            DotNetCli.Stage0.Restore("--verbosity", "verbose", "--infer-runtimes", 
                    "--fallbacksource", Dirs.CorehostLocalPackages,
                    "--fallbacksource", Dirs.CorehostDummyPackages)
                .WorkingDirectory(tempPjDirectory)
                .Execute()
                .EnsureSuccessful();

            // Clean out before publishing locked binaries
            FS.Rmdir(Dirs.CorehostLocked);

            // Use specific RIDS for non-backward compatible platforms.
            (CurrentPlatform.IsWindows
                ? DotNetCli.Stage0.Publish("--output", Dirs.CorehostLocked, "--no-build")
                : DotNetCli.Stage0.Publish("--output", Dirs.CorehostLocked, "--no-build", "-r", currentRid))
                .WorkingDirectory(tempPjDirectory)
                .Execute()
                .EnsureSuccessful();

            return c.Success();
        }

        [Target(nameof(PrepareTargets.Init))]
        public static BuildTargetResult CompileCoreHost(BuildTargetContext c)
        {
            var hostVersion = c.BuildContext.Get<HostVersion>("HostVersion");

            // Generate build files
            var cmakeOut = Path.Combine(Dirs.CorehostLatest, "cmake");

            Rmdir(cmakeOut);
            Mkdirp(cmakeOut);

            var configuration = c.BuildContext.Get<string>("Configuration");

            // Run the build
            string rid = GetRuntimeId();
            string corehostSrcDir = Path.Combine(c.BuildContext.BuildDirectory, "src", "corehost");
            string commitHash = c.BuildContext.Get<string>("CommitHash");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Why does Windows directly call cmake but Linux/Mac calls "build.sh" in the corehost dir?
                // See the comment in "src/corehost/build.sh" for details. It doesn't work for some reason.
                var visualStudio = IsWinx86 ? "Visual Studio 14 2015" : "Visual Studio 14 2015 Win64";
                var archMacro = IsWinx86 ? "-DCLI_CMAKE_PLATFORM_ARCH_I386=1" : "-DCLI_CMAKE_PLATFORM_ARCH_AMD64=1";
                var ridMacro = $"-DCLI_CMAKE_RUNTIME_ID:STRING={rid}";
                var arch = IsWinx86 ? "x86" : "x64";
                var baseSupportedRid = $"win7-{arch}";
                var cmakeHostPolicyVer = $"-DCLI_CMAKE_HOST_POLICY_VER:STRING={hostVersion.LatestHostPolicyVersion}";
                var cmakeHostFxrVer = $"-DCLI_CMAKE_HOST_FXR_VER:STRING={hostVersion.LatestHostFxrVersion}";
                var cmakeBaseRid = $"-DCLI_CMAKE_PKG_RID:STRING={baseSupportedRid}";
                var cmakeCommitHash = $"-DCLI_CMAKE_COMMIT_HASH:STRING={commitHash}";

                ExecIn(cmakeOut, "cmake",
                    corehostSrcDir,
                    archMacro,
                    ridMacro,
                    cmakeHostFxrVer,
                    cmakeHostPolicyVer,
                    cmakeBaseRid,
                    cmakeCommitHash,
                    "-G",
                    visualStudio);

                var pf32 = RuntimeInformation.OSArchitecture == Architecture.X64 ?
                    Environment.GetEnvironmentVariable("ProgramFiles(x86)") :
                    Environment.GetEnvironmentVariable("ProgramFiles");

                if (configuration.Equals("Release"))
                {
                    // Cmake calls it "RelWithDebInfo" in the generated MSBuild
                    configuration = "RelWithDebInfo";
                }

                Exec(Path.Combine(pf32, "MSBuild", "14.0", "Bin", "MSBuild.exe"),
                    Path.Combine(cmakeOut, "ALL_BUILD.vcxproj"),
                    $"/p:Configuration={configuration}");

                // Copy the output out
                File.Copy(Path.Combine(cmakeOut, "cli", configuration, "dotnet.exe"), Path.Combine(Dirs.CorehostLatest, "dotnet.exe"), overwrite: true);
                File.Copy(Path.Combine(cmakeOut, "cli", configuration, "dotnet.pdb"), Path.Combine(Dirs.CorehostLatest, "dotnet.pdb"), overwrite: true);
                File.Copy(Path.Combine(cmakeOut, "cli", "dll", configuration, "hostpolicy.dll"), Path.Combine(Dirs.CorehostLatest, "hostpolicy.dll"), overwrite: true);
                File.Copy(Path.Combine(cmakeOut, "cli", "dll", configuration, "hostpolicy.pdb"), Path.Combine(Dirs.CorehostLatest, "hostpolicy.pdb"), overwrite: true);
                File.Copy(Path.Combine(cmakeOut, "cli", "fxr", configuration, "hostfxr.dll"), Path.Combine(Dirs.CorehostLatest, "hostfxr.dll"), overwrite: true);
                File.Copy(Path.Combine(cmakeOut, "cli", "fxr", configuration, "hostfxr.pdb"), Path.Combine(Dirs.CorehostLatest, "hostfxr.pdb"), overwrite: true);
            }
            else
            {
                ExecIn(cmakeOut, Path.Combine(c.BuildContext.BuildDirectory, "src", "corehost", "build.sh"),
                        "--arch",
                        "x64",
                        "--fxrver",
                        hostVersion.LatestHostFxrVersion,
                        "--policyver",
                        hostVersion.LatestHostPolicyVersion,
                        "--rid",
                        rid,
                        "--commithash",
                        commitHash);

                // Copy the output out
                File.Copy(Path.Combine(cmakeOut, "cli", "dotnet"), Path.Combine(Dirs.CorehostLatest, "dotnet"), overwrite: true);
                File.Copy(Path.Combine(cmakeOut, "cli", "dll", HostPolicyBaseName), Path.Combine(Dirs.CorehostLatest, HostPolicyBaseName), overwrite: true);
                File.Copy(Path.Combine(cmakeOut, "cli", "fxr", DotnetHostFxrBaseName), Path.Combine(Dirs.CorehostLatest, DotnetHostFxrBaseName), overwrite: true);
            }
            return c.Success();
        }

        [Target(nameof(CompileTargets.GenerateStubHostPackages))]
        public static BuildTargetResult PackagePkgProjects(BuildTargetContext c)
        {
            var arch = IsWinx86 ? "x86" : "x64";
            
            var hostVersion = c.BuildContext.Get<HostVersion>("HostVersion");
            var hostNugetversion = hostVersion.LatestHostVersion;
            var content = $@"{c.BuildContext["CommitHash"]}{Environment.NewLine}{hostNugetversion}{Environment.NewLine}";
            var pkgDir = Path.Combine(c.BuildContext.BuildDirectory, "pkg");
            File.WriteAllText(Path.Combine(pkgDir, "version.txt"), content);

            if (CurrentPlatform.IsWindows)
            {
                Command.Create(Path.Combine(pkgDir, "pack.cmd"))
                    // Workaround to arg escaping adding backslashes for arguments to .cmd scripts.
                    .Environment("__WorkaroundCliCoreHostBuildArch", arch)
                    .Environment("__WorkaroundCliCoreHostBinDir", Dirs.CorehostLatest)
                    .Environment("__WorkaroundCliCoreHostPolicyVer", hostVersion.LatestHostPolicyVersionNoSuffix)
                    .Environment("__WorkaroundCliCoreHostFxrVer", hostVersion.LatestHostFxrVersionNoSuffix)
                    .Environment("__WorkaroundCliCoreHostVer", hostVersion.LatestHostVersionNoSuffix)
                    .Environment("__WorkaroundCliCoreHostBuildMajor", hostVersion.LatestHostBuildMajor)
                    .Environment("__WorkaroundCliCoreHostVersionTag", hostVersion.LatestHostPrerelease)
                    .ForwardStdOut()
                    .ForwardStdErr()
                    .Execute()
                    .EnsureSuccessful();
            }
            else
            {
                Exec(Path.Combine(pkgDir, "pack.sh"),
                    "--arch",
                    "x64",
                    "--hostbindir",
                    Dirs.CorehostLatest,
                    "--policyver",
                    hostVersion.LatestHostPolicyVersionNoSuffix,
                    "--fxrver",
                    hostVersion.LatestHostFxrVersionNoSuffix,
                    "--hostver",
                    hostVersion.LatestHostVersionNoSuffix,
                    "--build",
                    hostVersion.LatestHostBuildMajor,
                    "--vertag",
                    hostVersion.LatestHostPrerelease);
            }
            foreach (var file in Directory.GetFiles(Path.Combine(pkgDir, "bin", "packages"), "*.nupkg"))
            {
                var fileName = Path.GetFileName(file);
                File.Copy(file, Path.Combine(Dirs.CorehostLocalPackages, fileName), true);

                Console.WriteLine($"Copying package {fileName} to artifacts directory {Dirs.CorehostLocalPackages}.");
            }
            foreach (var item in hostVersion.LatestHostPackages)
            {
                var fileFilter = $"runtime.{HostPackagePlatformRid}.{item.Key}.{item.Value}.nupkg";
                if (Directory.GetFiles(Dirs.CorehostLocalPackages, fileFilter).Length == 0)
                {
                    throw new BuildFailureException($"Nupkg for {fileFilter} was not created.");
                }
            }
            return c.Success();
        }

        private static string GetRuntimeId()
        {
            string info = DotNetCli.Stage0.Exec("", "--info").CaptureStdOut().Execute().StdOut;
            string rid = Array.Find<string>(info.Split(Environment.NewLine.ToCharArray()), (e) => e.Contains("RID:"))?.Replace("RID:", "").Trim();

            // TODO: when Stage0 is updated with the new --info, remove this legacy check for --version
            if (string.IsNullOrEmpty(rid))
            {
                string version = DotNetCli.Stage0.Exec("", "--version").CaptureStdOut().Execute().StdOut;
                rid = Array.Find<string>(version.Split(Environment.NewLine.ToCharArray()), (e) => e.Contains("Runtime Id:")).Replace("Runtime Id:", "").Trim();
            }

            if (string.IsNullOrEmpty(rid))
            {
                throw new BuildFailureException("Could not find the Runtime ID from Stage0 --info or --version");
            }

            return rid;
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

            CopySharedHost(Dirs.Stage1);
            PublishSharedFramework(c, Dirs.Stage1, DotNetCli.Stage0);
            var result = CompileCliSdk(c,
                dotnet: DotNetCli.Stage0,
                outputDir: Dirs.Stage1);

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

            PublishSharedFramework(c, Dirs.Stage2, DotNetCli.Stage1);
            CopySharedHost(Dirs.Stage2);
            var result = CompileCliSdk(c,
                dotnet: DotNetCli.Stage1,
                outputDir: Dirs.Stage2);

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


        private static void PrepareDummyRuntimeNuGetPackage(DotNetCli dotnet)
        {
            var projectJson = new StringBuilder();
            projectJson.Append("{");
            projectJson.Append("  \"dependencies\": { \"NETStandard.Library\": \"1.5.0-rc2-24008\" },");
            projectJson.Append("  \"frameworks\": { \"netcoreapp1.0\": { \"imports\": [\"netstandard1.5\", \"dnxcore50\"] } },");
            projectJson.Append("  \"runtimes\": { \"win7-x64\": { } },");
            projectJson.Append("}");

            var programCs = "using System; namespace ConsoleApplication { public class Program { public static void Main(string[] args) { Console.WriteLine(\"Hello World!\"); } } }";

            var tempPjDirectory = Path.Combine(Dirs.Intermediate, "dummyNuGetPackageIntermediate");
            FS.Rmdir(tempPjDirectory);

            Directory.CreateDirectory(tempPjDirectory);

            var tempPjFile = Path.Combine(tempPjDirectory, "project.json");
            var tempSourceFile = Path.Combine(tempPjDirectory, "Program.cs");

            File.WriteAllText(tempPjFile, projectJson.ToString());
            File.WriteAllText(tempSourceFile, programCs.ToString());

            dotnet.Restore("--verbosity", "verbose", "--disable-parallel")
                .WorkingDirectory(tempPjDirectory)
                .Execute()
                .EnsureSuccessful();
            dotnet.Build(tempPjFile, "--runtime", "win7-x64")
                .WorkingDirectory(tempPjDirectory)
                .Execute()
                .EnsureSuccessful();
        }

        private static void CreateDummyRuntimeNuGetPackage(DotNetCli dotnet, string basePackageId, string rid, string version, string outputDir)
        {
            var packageId = $"runtime.{rid}.{basePackageId}";

            var projectJson = new StringBuilder();
            projectJson.Append("{");
            projectJson.Append($"  \"version\": \"{version}\",");
            projectJson.Append($"  \"name\": \"{packageId}\",");
            projectJson.Append("  \"dependencies\": { \"NETStandard.Library\": \"1.5.0-rc2-24008\" },");
            projectJson.Append("  \"frameworks\": { \"netcoreapp1.0\": { \"imports\": [\"netstandard1.5\", \"dnxcore50\"] } },");
            projectJson.Append("}");

            var tempPjDirectory = Path.Combine(Dirs.Intermediate, "dummyNuGetPackageIntermediate");
            var tempPjFile = Path.Combine(tempPjDirectory, "project.json");

            File.WriteAllText(tempPjFile, projectJson.ToString());

            dotnet.Pack(
                tempPjFile, "--no-build",
                "--output", outputDir)
                .WorkingDirectory(tempPjDirectory)
                .Execute()
                .EnsureSuccessful();
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

        private static void CopySharedHost(string outputDir)
        {
            File.Copy(
                Path.Combine(Dirs.CorehostLocked, DotnetHostBaseName),
                Path.Combine(outputDir, DotnetHostBaseName), true);
            File.Copy(
                Path.Combine(Dirs.CorehostLocked, DotnetHostFxrBaseName),
                Path.Combine(outputDir, DotnetHostFxrBaseName), true);
        }

        public static void PublishSharedFramework(BuildTargetContext c, string outputDir, DotNetCli dotnetCli)
        {
            string SharedFrameworkTemplateSourceRoot = Path.Combine(Dirs.RepoRoot, "src", "sharedframework", "framework");
            string SharedFrameworkNugetVersion = c.BuildContext.Get<string>("SharedFrameworkNugetVersion");

            string sharedFrameworkRid;
            if (RuntimeEnvironment.OperatingSystemPlatform == Platform.Windows)
            {
                sharedFrameworkRid = $"win7-{RuntimeEnvironment.RuntimeArchitecture}";
            }
            else
            {
                sharedFrameworkRid = RuntimeEnvironment.GetRuntimeIdentifier();
            }

            string SharedFrameworkSourceRoot = GenerateSharedFrameworkProject(c, SharedFrameworkTemplateSourceRoot, sharedFrameworkRid);

            dotnetCli.Restore(
                "--verbosity", "verbose", 
                "--disable-parallel", 
                "--infer-runtimes", 
                "--fallbacksource", Dirs.CorehostLocalPackages)
                .WorkingDirectory(SharedFrameworkSourceRoot)
                .Execute()
                .EnsureSuccessful();

            // We publish to a sub folder of the PublishRoot so tools like heat and zip can generate folder structures easier.
            string SharedFrameworkNameAndVersionRoot = Path.Combine(outputDir, "shared", SharedFrameworkName, SharedFrameworkNugetVersion);
            c.BuildContext["SharedFrameworkPath"] = SharedFrameworkNameAndVersionRoot;

            if (Directory.Exists(SharedFrameworkNameAndVersionRoot))
            {
                Utils.DeleteDirectory(SharedFrameworkNameAndVersionRoot);
            }

            dotnetCli.Publish(
                "--output", SharedFrameworkNameAndVersionRoot,
                "-r", sharedFrameworkRid,
                SharedFrameworkSourceRoot).Execute().EnsureSuccessful();

            // Clean up artifacts that dotnet-publish generates which we don't need
            DeleteMainPublishOutput(SharedFrameworkNameAndVersionRoot, "framework");
            File.Delete(Path.Combine(SharedFrameworkNameAndVersionRoot, "framework.runtimeconfig.json"));

            // Rename the .deps file
            var destinationDeps = Path.Combine(SharedFrameworkNameAndVersionRoot, $"{SharedFrameworkName}.deps.json");
            File.Move(Path.Combine(SharedFrameworkNameAndVersionRoot, "framework.deps.json"), destinationDeps);
            ChangeEntryPointLibraryName(destinationDeps, null);

            // Generate RID fallback graph
            string runtimeGraphGeneratorRuntime = null;
            switch (RuntimeEnvironment.OperatingSystemPlatform)
            {
                case Platform.Windows:
                    runtimeGraphGeneratorRuntime = "win";
                    break;
                case Platform.Linux:
                    runtimeGraphGeneratorRuntime = "linux";
                    break;
                case Platform.Darwin:
                    runtimeGraphGeneratorRuntime = "osx";
                    break;
            }
            if (!string.IsNullOrEmpty(runtimeGraphGeneratorRuntime))
            {
                var runtimeGraphGeneratorName = "RuntimeGraphGenerator";
                var runtimeGraphGeneratorProject = Path.Combine(Dirs.RepoRoot, "tools", runtimeGraphGeneratorName);
                var runtimeGraphGeneratorOutput = Path.Combine(Dirs.Output, "tools", runtimeGraphGeneratorName);

                dotnetCli.Publish(
                    "--output", runtimeGraphGeneratorOutput,
                    runtimeGraphGeneratorProject).Execute().EnsureSuccessful();
                var runtimeGraphGeneratorExe = Path.Combine(runtimeGraphGeneratorOutput, $"{runtimeGraphGeneratorName}{Constants.ExeSuffix}");

                Cmd(runtimeGraphGeneratorExe, "--project", SharedFrameworkSourceRoot, "--deps", destinationDeps, runtimeGraphGeneratorRuntime)
                    .Execute()
                    .EnsureSuccessful();
            }
            else
            {
                c.Error($"Could not determine rid graph generation runtime for platform {RuntimeEnvironment.OperatingSystemPlatform}");
            }

            File.Copy(
                Path.Combine(Dirs.CorehostLocked, DotnetHostBaseName),
                Path.Combine(SharedFrameworkNameAndVersionRoot, DotnetHostBaseName), true);
             File.Copy(
                Path.Combine(Dirs.CorehostLocked, DotnetHostBaseName),
                Path.Combine(SharedFrameworkNameAndVersionRoot, $"corehost{Constants.ExeSuffix}"), true);
            File.Copy(
                Path.Combine(Dirs.CorehostLocked, DotnetHostFxrBaseName),
                Path.Combine(SharedFrameworkNameAndVersionRoot, DotnetHostFxrBaseName), true);

            // Hostpolicy should be the latest and not the locked version as it is supposed to evolve for
            // the framework and has a tight coupling with coreclr's API in the framework.
            File.Copy(
                Path.Combine(Dirs.CorehostLatest, HostPolicyBaseName),
                Path.Combine(SharedFrameworkNameAndVersionRoot, HostPolicyBaseName), true);

            if (File.Exists(Path.Combine(SharedFrameworkNameAndVersionRoot, "mscorlib.ni.dll")))
            {
                // Publish already places the crossgen'd version of mscorlib into the output, so we can
                // remove the IL version
                File.Delete(Path.Combine(SharedFrameworkNameAndVersionRoot, "mscorlib.dll"));
            }

            CrossgenUtil.CrossgenDirectory(c, SharedFrameworkNameAndVersionRoot);

            // Generate .version file for sharedfx
            var version = SharedFrameworkNugetVersion;
            var content = $@"{c.BuildContext["CommitHash"]}{Environment.NewLine}{version}{Environment.NewLine}";
            File.WriteAllText(Path.Combine(SharedFrameworkNameAndVersionRoot, ".version"), content);
        }

        /// <summary>
        /// Generates the real shared framework project that will get published.
        /// </summary>
        /// <param name="sharedFrameworkTemplatePath">The "sharedFramework" source template folder.</param>
        private static string GenerateSharedFrameworkProject(BuildTargetContext c, string sharedFrameworkTemplatePath, string rid)
        {
            string sharedFrameworkProjectPath = Path.Combine(Dirs.Intermediate, "sharedFramework", "framework");
            Utils.DeleteDirectory(sharedFrameworkProjectPath);
            CopyRecursive(sharedFrameworkTemplatePath, sharedFrameworkProjectPath, true);

            string templateFile = Path.Combine(sharedFrameworkProjectPath, "project.json.template");
            JObject sharedFrameworkProject = JsonUtils.ReadProject(templateFile);
            sharedFrameworkProject["dependencies"]["Microsoft.NETCore.App"] = c.BuildContext.Get<BuildVersion>("BuildVersion").NetCoreAppVersion;
            ((JObject)sharedFrameworkProject["runtimes"]).RemoveAll();
            sharedFrameworkProject["runtimes"][rid] = new JObject();

            string projectJsonPath = Path.Combine(sharedFrameworkProjectPath, "project.json");
            JsonUtils.WriteProject(sharedFrameworkProject, projectJsonPath);

            Rm(templateFile);

            return sharedFrameworkProjectPath;
        }

        private static BuildTargetResult CompileCliSdk(BuildTargetContext c, DotNetCli dotnet, string outputDir)
        {
            var configuration = c.BuildContext.Get<string>("Configuration");
            var buildVersion = c.BuildContext.Get<BuildVersion>("BuildVersion");
            var srcDir = Path.Combine(c.BuildContext.BuildDirectory, "src");
            outputDir = Path.Combine(outputDir, "sdk", buildVersion.NuGetVersion);

            FS.CleanBinObj(c, srcDir);
            Rmdir(outputDir);
            Mkdirp(outputDir);

            foreach (var project in ProjectsToPublish)
            {
                dotnet.Publish(
                    "--native-subdirectory",
                    "--output", outputDir,
                    "--configuration", configuration,
                    "--version-suffix", buildVersion.CommitCountString,
                    Path.Combine(srcDir, project))
                    .Execute()
                    .EnsureSuccessful();
            }

            FixModeFlags(outputDir);

            string compilersProject = Path.Combine(Dirs.RepoRoot, "src", "compilers");
            dotnet.Publish(compilersProject,
                    "--output",
                    outputDir,
                    "--framework",
                    "netstandard1.5")
                    .Execute()
                    .EnsureSuccessful();

            var compilersDeps = Path.Combine(outputDir, "compilers.deps.json");
            var compilersRuntimeConfig = Path.Combine(outputDir, "compilers.runtimeconfig.json");

            File.Copy(Path.Combine(Dirs.CorehostLocked, DotnetHostBaseName), Path.Combine(outputDir, $"corehost{Constants.ExeSuffix}"), overwrite: true);
            File.Copy(Path.Combine(Dirs.CorehostLocked, $"{Constants.DynamicLibPrefix}hostfxr{Constants.DynamicLibSuffix}"), Path.Combine(outputDir, $"{Constants.DynamicLibPrefix}hostfxr{Constants.DynamicLibSuffix}"), overwrite: true);
            File.Copy(Path.Combine(Dirs.CorehostLatest, $"{Constants.DynamicLibPrefix}hostpolicy{Constants.DynamicLibSuffix}"), Path.Combine(outputDir, $"{Constants.DynamicLibPrefix}hostpolicy{Constants.DynamicLibSuffix}"), overwrite: true);

            var binaryToCorehostifyOutDir = Path.Combine(outputDir, "runtimes", "any", "native");
            // Corehostify binaries
            foreach (var binaryToCorehostify in BinariesForCoreHost)
            {
                try
                {
                    // Yes, it is .exe even on Linux. This is the managed exe we're working with
                    File.Copy(Path.Combine(binaryToCorehostifyOutDir, $"{binaryToCorehostify}.exe"), Path.Combine(outputDir, $"{binaryToCorehostify}.dll"));
                    File.Delete(Path.Combine(binaryToCorehostifyOutDir, $"{binaryToCorehostify}.exe"));
                    File.Copy(compilersDeps, Path.Combine(outputDir, binaryToCorehostify + ".deps.json"));
                    File.Copy(compilersRuntimeConfig, Path.Combine(outputDir, binaryToCorehostify + ".runtimeconfig.json"));
                    ChangeEntryPointLibraryName(Path.Combine(outputDir, binaryToCorehostify + ".deps.json"), binaryToCorehostify);
                }
                catch (Exception ex)
                {
                    return c.Failed($"Failed to corehostify '{binaryToCorehostify}': {ex.ToString()}");
                }
            }

            // cleanup compilers project output we don't need
            DeleteMainPublishOutput(outputDir, "compilers");
            File.Delete(compilersDeps);
            File.Delete(compilersRuntimeConfig);

            CrossgenUtil.CrossgenDirectory(c, outputDir);

            // Generate .version file
            var version = buildVersion.NuGetVersion;
            var content = $@"{c.BuildContext["CommitHash"]}{Environment.NewLine}{version}{Environment.NewLine}";
            File.WriteAllText(Path.Combine(outputDir, ".version"), content);

            return c.Success();
        }

        private static void ChangeEntryPointLibraryName(string depsFile, string newName)
        {
            JToken deps;
            using (var file = File.OpenText(depsFile))
            using (JsonTextReader reader = new JsonTextReader(file))
            {
                deps = JObject.ReadFrom(reader);
            }

            string version = null;
            foreach (JProperty target in deps["targets"])
            {
                var targetLibrary = target.Value.Children<JProperty>().FirstOrDefault();
                if (targetLibrary == null)
                {
                    continue;
                }
                version = targetLibrary.Name.Substring(targetLibrary.Name.IndexOf('/') + 1);
                if (newName == null)
                {
                    targetLibrary.Remove();
                }
                else
                {
                    targetLibrary.Replace(new JProperty(newName + '/' + version, targetLibrary.Value));
                }
            }
            if (version != null)
            {
                var library = deps["libraries"].Children<JProperty>().First();
                if (newName == null)
                {
                    library.Remove();
                }
                else
                {
                    library.Replace(new JProperty(newName + '/' + version, library.Value));
                }
                using (var file = File.CreateText(depsFile))
                using (var writer = new JsonTextWriter(file) { Formatting = Formatting.Indented })
                {
                    deps.WriteTo(writer);
                }
            }
        }

        private static void DeleteMainPublishOutput(string path, string name)
        {
            File.Delete(Path.Combine(path, $"{name}{Constants.ExeSuffix}"));
            File.Delete(Path.Combine(path, $"{name}.dll"));
            File.Delete(Path.Combine(path, $"{name}.pdb"));
        }
    }
}
