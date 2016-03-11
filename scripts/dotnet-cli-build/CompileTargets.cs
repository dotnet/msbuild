using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Cli.Build.Framework;
using Microsoft.Extensions.PlatformAbstractions;

using static Microsoft.DotNet.Cli.Build.FS;
using static Microsoft.DotNet.Cli.Build.Framework.BuildHelpers;

namespace Microsoft.DotNet.Cli.Build
{
    public class CompileTargets
    {
        public static readonly string CoreCLRVersion = "1.0.2-rc2-23901";
        public static readonly string AppDepSdkVersion = "1.0.6-prerelease-00003";
        public static readonly bool IsWinx86 = CurrentPlatform.IsWindows && CurrentArchitecture.Isx86;

        public static readonly List<string> AssembliesToCrossGen = GetAssembliesToCrossGen();

        public static readonly string[] BinariesForCoreHost = new[]
        {
            "csi",
            "csc",
            "vbc"
        };

        public static readonly string[] ProjectsToPublish = new[]
        {
            "dotnet"
        };

        public static readonly string[] FilesToClean = new[]
        {
            "README.md"
        };

        public static readonly string[] ProjectsToPack = new[]
        {
            "Microsoft.DotNet.Cli.Utils",
            "Microsoft.DotNet.ProjectModel",
            "Microsoft.DotNet.ProjectModel.Loader",
            "Microsoft.DotNet.ProjectModel.Workspaces",
            "Microsoft.Extensions.DependencyModel",
            "Microsoft.Extensions.Testing.Abstractions"
        };

        // Updates the stage 2 with recent changes.
        [Target(nameof(PrepareTargets.Init), nameof(CompileStage2))]
        public static BuildTargetResult UpdateBuild(BuildTargetContext c)
        {
            return c.Success();
        }

        [Target(nameof(PrepareTargets.Init), nameof(CompileCoreHost), nameof(CompileStage1), nameof(CompileStage2))]
        public static BuildTargetResult Compile(BuildTargetContext c)
        {
            return c.Success();
        }

        [Target]
        public static BuildTargetResult CompileCoreHost(BuildTargetContext c)
        {
            // Generate build files
            var cmakeOut = Path.Combine(Dirs.Corehost, "cmake");

            Rmdir(cmakeOut);
            Mkdirp(cmakeOut);

            var configuration = c.BuildContext.Get<string>("Configuration");

            // Run the build
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Why does Windows directly call cmake but Linux/Mac calls "build.sh" in the corehost dir?
                // See the comment in "src/corehost/build.sh" for details. It doesn't work for some reason.
                var visualStudio = IsWinx86 ? "Visual Studio 14 2015" : "Visual Studio 14 2015 Win64";
                var archMacro = IsWinx86 ? "-DCLI_CMAKE_PLATFORM_ARCH_I386=1" : "-DCLI_CMAKE_PLATFORM_ARCH_AMD64=1";
                ExecIn(cmakeOut, "cmake",
                    Path.Combine(c.BuildContext.BuildDirectory, "src", "corehost"),
                    archMacro,
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
                File.Copy(Path.Combine(cmakeOut, "cli", configuration, "corehost.exe"), Path.Combine(Dirs.Corehost, "corehost.exe"), overwrite: true);
                File.Copy(Path.Combine(cmakeOut, "cli", configuration, "corehost.pdb"), Path.Combine(Dirs.Corehost, "corehost.pdb"), overwrite: true);
                File.Copy(Path.Combine(cmakeOut, "cli", "dll", configuration, "hostpolicy.dll"), Path.Combine(Dirs.Corehost, "hostpolicy.dll"), overwrite: true);
                File.Copy(Path.Combine(cmakeOut, "cli", "dll", configuration, "hostpolicy.pdb"), Path.Combine(Dirs.Corehost, "hostpolicy.pdb"), overwrite: true);
            }
            else
            {
                ExecIn(cmakeOut, Path.Combine(c.BuildContext.BuildDirectory, "src", "corehost", "build.sh"));

                // Copy the output out
                File.Copy(Path.Combine(cmakeOut, "cli", "corehost"), Path.Combine(Dirs.Corehost, "corehost"), overwrite: true);
                File.Copy(Path.Combine(cmakeOut, "cli", "dll", $"{Constants.DynamicLibPrefix}hostpolicy{Constants.DynamicLibSuffix}"), Path.Combine(Dirs.Corehost, $"{Constants.DynamicLibPrefix}hostpolicy{Constants.DynamicLibSuffix}"), overwrite: true);
            }

            return c.Success();
        }

        [Target]
        public static BuildTargetResult CompileStage1(BuildTargetContext c)
        {
            CleanBinObj(c, Path.Combine(c.BuildContext.BuildDirectory, "src"));
            CleanBinObj(c, Path.Combine(c.BuildContext.BuildDirectory, "test"));
            return CompileStage(c,
                dotnet: DotNetCli.Stage0,
                outputDir: Dirs.Stage1);
        }

        [Target]
        public static BuildTargetResult CompileStage2(BuildTargetContext c)
        {
            var configuration = c.BuildContext.Get<string>("Configuration");

            CleanBinObj(c, Path.Combine(c.BuildContext.BuildDirectory, "src"));
            CleanBinObj(c, Path.Combine(c.BuildContext.BuildDirectory, "test"));
            var result = CompileStage(c,
                dotnet: DotNetCli.Stage1,
                outputDir: Dirs.Stage2);

            if (!result.Success)
            {
                return result;
            }

            // Build projects that are packed in NuGet packages, but only on Windows
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var packagingOutputDir = Path.Combine(Dirs.Stage2Compilation, "forPackaging");
                Mkdirp(packagingOutputDir);
                foreach (var project in ProjectsToPack)
                {
                    // Just build them, we'll pack later
                    DotNetCli.Stage1.Build(
                        "--build-base-path",
                        packagingOutputDir,
                        "--configuration",
                        configuration,
                        Path.Combine(c.BuildContext.BuildDirectory, "src", project))
                        .Execute()
                        .EnsureSuccessful();
                }
            }

            return c.Success();
        }

        private static BuildTargetResult CompileStage(BuildTargetContext c, DotNetCli dotnet, string outputDir)
        {
            Rmdir(outputDir);

            var configuration = c.BuildContext.Get<string>("Configuration");
            var binDir = Path.Combine(outputDir, "bin");
            var buildVesion = c.BuildContext.Get<BuildVersion>("BuildVersion");

            Mkdirp(binDir);

            foreach (var project in ProjectsToPublish)
            {
                // TODO: Use the flag once we get a full build round tripped
                // --version-suffix buildVesion.VersionSuffix
                dotnet.Publish(
                    "--native-subdirectory",
                    "--output",
                    binDir,
                    "--configuration",
                    configuration,
                    Path.Combine(c.BuildContext.BuildDirectory, "src", project))
                    .Environment("DOTNET_BUILD_VERSION", buildVesion.VersionSuffix)
                    .Execute()
                    .EnsureSuccessful();
            }

            FixModeFlags(outputDir);

            // Copy corehost
            File.Copy(Path.Combine(Dirs.Corehost, $"corehost{Constants.ExeSuffix}"), Path.Combine(binDir, $"corehost{Constants.ExeSuffix}"), overwrite: true);
            File.Copy(Path.Combine(Dirs.Corehost, $"{Constants.DynamicLibPrefix}hostpolicy{Constants.DynamicLibSuffix}"), Path.Combine(binDir, $"{Constants.DynamicLibPrefix}hostpolicy{Constants.DynamicLibSuffix}"), overwrite: true);

            // Corehostify binaries
            foreach (var binaryToCorehostify in BinariesForCoreHost)
            {
                try
                {
                    // Yes, it is .exe even on Linux. This is the managed exe we're working with
                    File.Copy(Path.Combine(binDir, $"{binaryToCorehostify}.exe"), Path.Combine(binDir, $"{binaryToCorehostify}.dll"));
                    File.Delete(Path.Combine(binDir, $"{binaryToCorehostify}.exe"));
                    File.Copy(Path.Combine(binDir, $"corehost{Constants.ExeSuffix}"), Path.Combine(binDir, binaryToCorehostify + Constants.ExeSuffix));
                }
                catch (Exception ex)
                {
                    return c.Failed($"Failed to corehostify '{binaryToCorehostify}': {ex.ToString()}");
                }
            }

            // dotnet.exe is from stage0. But we must be using the newly built corehost in stage1
            File.Delete(Path.Combine(binDir, $"dotnet{Constants.ExeSuffix}"));
            File.Copy(Path.Combine(binDir, $"corehost{Constants.ExeSuffix}"), Path.Combine(binDir, $"dotnet{Constants.ExeSuffix}"));

            // Crossgen Roslyn
            var result = Crossgen(c, binDir);
            if (!result.Success)
            {
                return result;
            }

            // Copy AppDeps
            result = CopyAppDeps(c, binDir);
            if (!result.Success)
            {
                return result;
            }

            // Generate .version file
            var version = buildVesion.SimpleVersion;
            var content = $@"{c.BuildContext["CommitHash"]}{Environment.NewLine}{version}{Environment.NewLine}";
            File.WriteAllText(Path.Combine(outputDir, ".version"), content);

            return c.Success();
        }

        private static BuildTargetResult CopyAppDeps(BuildTargetContext c, string outputDir)
        {
            var appDepOutputDir = Path.Combine(outputDir, "appdepsdk");
            Rmdir(appDepOutputDir);
            Mkdirp(appDepOutputDir);

            // Find toolchain package
            string packageId;

            if (CurrentPlatform.IsWindows)
            {
                if (CurrentArchitecture.Isx86)
                {
                    // https://github.com/dotnet/cli/issues/1550
                    c.Warn("Native compilation is not yet working on Windows x86");
                    return c.Success();
                }

                packageId = "toolchain.win7-x64.Microsoft.DotNet.AppDep";
            }
            else if (CurrentPlatform.IsUbuntu)
            {
                packageId = "toolchain.ubuntu.14.04-x64.Microsoft.DotNet.AppDep";
            }
            else if (CurrentPlatform.IsCentOS || CurrentPlatform.IsRHEL || CurrentPlatform.IsDebian)
            {
                c.Warn($"Native compilation is not yet working on {CurrentPlatform.Current}");
                return c.Success();
            }
            else if (CurrentPlatform.IsOSX)
            {
                packageId = "toolchain.osx.10.10-x64.Microsoft.DotNet.AppDep";
            }
            else
            {
                return c.Failed("Unsupported OS Platform");
            }

            var appDepPath = Path.Combine(
                Dirs.NuGetPackages,
                packageId,
                AppDepSdkVersion);
            CopyRecursive(appDepPath, appDepOutputDir, overwrite: true);

            return c.Success();
        }

        private static BuildTargetResult Crossgen(BuildTargetContext c, string outputDir)
        {
            // Check if we need to skip crossgen
            if (string.Equals(Environment.GetEnvironmentVariable("DOTNET_BUILD_SKIP_CROSSGEN"), "1"))
            {
                c.Warn("Skipping crossgen because DOTNET_BUILD_SKIP_CROSSGEN is set");
                return c.Success();
            }

            // Find crossgen
            string arch = PlatformServices.Default.Runtime.RuntimeArchitecture;
            string packageId;
            if (CurrentPlatform.IsWindows)
            {
                packageId = $"runtime.win7-{arch}.Microsoft.NETCore.Runtime.CoreCLR";
            }
            else if (CurrentPlatform.IsUbuntu)
            {
                packageId = "runtime.ubuntu.14.04-x64.Microsoft.NETCore.Runtime.CoreCLR";
            }
            else if (CurrentPlatform.IsCentOS || CurrentPlatform.IsRHEL)
            {
                // CentOS runtime is in the runtime.rhel.7-x64... package.
                packageId = "runtime.rhel.7-x64.Microsoft.NETCore.Runtime.CoreCLR";
            }
            else if (CurrentPlatform.IsDebian)
            {
                packageId = "runtime.debian.8.2-x64.Microsoft.NETCore.Runtime.CoreCLR";
            }
            else if (CurrentPlatform.IsOSX)
            {
                packageId = "runtime.osx.10.10-x64.Microsoft.NETCore.Runtime.CoreCLR";
            }
            else
            {
                return c.Failed("Unsupported OS Platform");
            }

            var crossGenExePath = Path.Combine(
                Dirs.NuGetPackages,
                packageId,
                CoreCLRVersion,
                "tools",
                $"crossgen{Constants.ExeSuffix}");

            // We have to copy crossgen next to mscorlib
            var crossgen = Path.Combine(outputDir, $"crossgen{Constants.ExeSuffix}");
            File.Copy(crossGenExePath, crossgen, overwrite: true);
            Chmod(crossgen, "a+x");

            // And if we have mscorlib.ni.dll, we need to rename it to mscorlib.dll
            if (File.Exists(Path.Combine(outputDir, "mscorlib.ni.dll")))
            {
                File.Copy(Path.Combine(outputDir, "mscorlib.ni.dll"), Path.Combine(outputDir, "mscorlib.dll"), overwrite: true);
            }

            foreach (var assemblyToCrossgen in AssembliesToCrossGen)
            {
                c.Info($"Crossgenning {assemblyToCrossgen}");
                ExecInSilent(outputDir, crossgen, "-FragileNonVersionable", "-nologo", "-platform_assemblies_paths", outputDir, assemblyToCrossgen);
            }

            c.Info("Crossgen complete");

            // Check if csc/vbc.ni.exe exists, and overwrite the dll with it just in case
            if (File.Exists(Path.Combine(outputDir, "csc.ni.exe")) && !File.Exists(Path.Combine(outputDir, "csc.ni.dll")))
            {
                File.Move(Path.Combine(outputDir, "csc.ni.exe"), Path.Combine(outputDir, "csc.ni.dll"));
            }

            if (File.Exists(Path.Combine(outputDir, "vbc.ni.exe")) && !File.Exists(Path.Combine(outputDir, "vbc.ni.dll")))
            {
                File.Move(Path.Combine(outputDir, "vbc.ni.exe"), Path.Combine(outputDir, "vbc.ni.dll"));
            }

            return c.Success();
        }

        private static List<string> GetAssembliesToCrossGen()
        {
            var list = new List<string>
            {
                "System.Collections.Immutable.dll",
                "System.Reflection.Metadata.dll",
                "Microsoft.CodeAnalysis.dll",
                "Microsoft.CodeAnalysis.CSharp.dll",
                "Microsoft.CodeAnalysis.VisualBasic.dll",
                "csc.dll",
                "vbc.dll"
            };

            // mscorlib is already crossgenned on Windows
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // mscorlib has to be crossgenned first
                list.Insert(0, "mscorlib.dll");
            }

            return list;
        }
    }
}
