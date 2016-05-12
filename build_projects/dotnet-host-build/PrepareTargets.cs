using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.DotNet.Cli.Build.Framework;
using Microsoft.DotNet.InternalAbstractions;
using Newtonsoft.Json.Linq;
using Microsoft.DotNet.Cli.Build;
using static Microsoft.DotNet.Cli.Build.Framework.BuildHelpers;
using static Microsoft.DotNet.Cli.Build.FS;
using static Microsoft.DotNet.Cli.Build.Utils;

namespace Microsoft.DotNet.Host.Build
{
    public class PrepareTargets
    {
        [Target(nameof(Init))]
        public static BuildTargetResult Prepare(BuildTargetContext c) => c.Success();

        [Target(nameof(CheckPrereqCmakePresent), nameof(CheckPlatformDependencies))]
        public static BuildTargetResult CheckPrereqs(BuildTargetContext c) => c.Success();

        [Target(nameof(CheckCoreclrPlatformDependencies))]
        public static BuildTargetResult CheckPlatformDependencies(BuildTargetContext c) => c.Success();

        [Target(nameof(CheckUbuntuCoreclrAndCoreFxDependencies), nameof(CheckCentOSCoreclrAndCoreFxDependencies))]
        public static BuildTargetResult CheckCoreclrPlatformDependencies(BuildTargetContext c) => c.Success();

        // All major targets will depend on this in order to ensure variables are set up right if they are run independently
        [Target(nameof(GenerateVersions), nameof(CheckPrereqs), nameof(LocateStage0), nameof(ExpectedBuildArtifacts))]
        public static BuildTargetResult Init(BuildTargetContext c)
        {
            var configEnv = Environment.GetEnvironmentVariable("CONFIGURATION");

            if (string.IsNullOrEmpty(configEnv))
            {
                configEnv = "Debug";
            }

            c.BuildContext["Configuration"] = configEnv;
            c.BuildContext["Channel"] = Environment.GetEnvironmentVariable("CHANNEL");

            c.Info($"Building {c.BuildContext["Configuration"]} to: {Dirs.Output}");
            c.Info("Build Environment:");
            c.Info($" Operating System: {RuntimeEnvironment.OperatingSystem} {RuntimeEnvironment.OperatingSystemVersion}");
            c.Info($" Platform: {RuntimeEnvironment.OperatingSystemPlatform}");

            return c.Success();
        }

        [Target]
        public static BuildTargetResult GenerateVersions(BuildTargetContext c)
        {
            var gitResult = Cmd("git", "rev-list", "--count", "HEAD")
                .CaptureStdOut()
                .Execute();
            gitResult.EnsureSuccessful();
            var commitCount = int.Parse(gitResult.StdOut);

            gitResult = Cmd("git", "rev-parse", "HEAD")
                .CaptureStdOut()
                .Execute();
            gitResult.EnsureSuccessful();
            var commitHash = gitResult.StdOut.Trim();

            var hostVersion = new HostVersion()
            {
                CommitCount = commitCount
            };

            c.BuildContext["HostVersion"] = hostVersion;
            c.BuildContext["CommitHash"] = commitHash;

            c.Info($"Building Version: {hostVersion.LatestHostVersionNoSuffix} (NuGet Packages: {hostVersion.LatestHostVersion})");
            c.Info($"From Commit: {commitHash}");

            return c.Success();
        }

        [Target]
        public static BuildTargetResult LocateStage0(BuildTargetContext c)
        {
            // We should have been run in the repo root, so locate the stage 0 relative to current directory
            var stage0 = DotNetCli.Stage0.BinPath;

            if (!Directory.Exists(stage0))
            {
                return c.Failed($"Stage 0 directory does not exist: {stage0}");
            }

            // Identify the version
            string versionFile = Directory.GetFiles(stage0, ".version", SearchOption.AllDirectories).FirstOrDefault();

            if (string.IsNullOrEmpty(versionFile))
            {
                throw new Exception($"'.version' file not found in '{stage0}' folder");
            }

            var version = File.ReadAllLines(versionFile);
            c.Info($"Using Stage 0 Version: {version[1]}");

            return c.Success();
        }

        [Target]
        public static BuildTargetResult ExpectedBuildArtifacts(BuildTargetContext c)
        {
            var config = Environment.GetEnvironmentVariable("CONFIGURATION");
            var versionBadgeName = $"{CurrentPlatform.Current}_{CurrentArchitecture.Current}_{config}_version_badge.svg";
            c.BuildContext["VersionBadge"] = Path.Combine(Dirs.Output, versionBadgeName);

            var hostVersion = c.BuildContext.Get<HostVersion>("HostVersion").LockedHostVersion;
            return c.Success();
        }

        [Target]
        [BuildPlatforms(BuildPlatform.Ubuntu)]
        public static BuildTargetResult CheckUbuntuCoreclrAndCoreFxDependencies(BuildTargetContext c)
        {
            var errorMessageBuilder = new StringBuilder();
            var stage0 = DotNetCli.Stage0.BinPath;

            foreach (var package in PackageDependencies.UbuntuCoreclrAndCoreFxDependencies)
            {
                if (!AptDependencyUtility.PackageIsInstalled(package))
                {
                    errorMessageBuilder.Append($"Error: Coreclr package dependency {package} missing.");
                    errorMessageBuilder.Append(Environment.NewLine);
                    errorMessageBuilder.Append($"-> install with apt-get install {package}");
                    errorMessageBuilder.Append(Environment.NewLine);
                }
            }

            if (errorMessageBuilder.Length == 0)
            {
                return c.Success();
            }
            else
            {
                return c.Failed(errorMessageBuilder.ToString());
            }
        }

        [Target]
        [BuildPlatforms(BuildPlatform.CentOS)]
        public static BuildTargetResult CheckCentOSCoreclrAndCoreFxDependencies(BuildTargetContext c)
        {
            var errorMessageBuilder = new StringBuilder();

            foreach (var package in PackageDependencies.CentosCoreclrAndCoreFxDependencies)
            {
                if (!YumDependencyUtility.PackageIsInstalled(package))
                {
                    errorMessageBuilder.Append($"Error: Coreclr package dependency {package} missing.");
                    errorMessageBuilder.Append(Environment.NewLine);
                    errorMessageBuilder.Append($"-> install with yum install {package}");
                    errorMessageBuilder.Append(Environment.NewLine);
                }
            }

            if (errorMessageBuilder.Length == 0)
            {
                return c.Success();
            }
            else
            {
                return c.Failed(errorMessageBuilder.ToString());
            }
        }

        [Target]
        public static BuildTargetResult CheckPrereqCmakePresent(BuildTargetContext c)
        {
            try
            {
                Command.Create("cmake", "--version")
                    .CaptureStdOut()
                    .CaptureStdErr()
                    .Execute();
            }
            catch (Exception ex)
            {
                string message = $@"Error running cmake: {ex.Message}
cmake is required to build the native host 'corehost'";
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    message += Environment.NewLine + "Download it from https://www.cmake.org";
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    message += Environment.NewLine + "Ubuntu: 'sudo apt-get install cmake'";
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    message += Environment.NewLine + "OS X w/Homebrew: 'brew install cmake'";
                }
                return c.Failed(message);
            }

            return c.Success();
        }
    }
}
