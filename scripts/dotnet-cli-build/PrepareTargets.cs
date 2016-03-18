using Microsoft.DotNet.Cli.Build.Framework;
using Microsoft.Extensions.PlatformAbstractions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

using static Microsoft.DotNet.Cli.Build.FS;
using static Microsoft.DotNet.Cli.Build.Utils;
using static Microsoft.DotNet.Cli.Build.Framework.BuildHelpers;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.Cli.Build
{
    public class PrepareTargets
    {
        [Target(nameof(Init), nameof(RestorePackages))]
        public static BuildTargetResult Prepare(BuildTargetContext c) => c.Success();

        [Target(nameof(CheckPrereqCmakePresent), nameof(CheckPlatformDependencies))]
        public static BuildTargetResult CheckPrereqs(BuildTargetContext c) => c.Success();

        [Target(nameof(CheckCoreclrPlatformDependencies), nameof(CheckInstallerBuildPlatformDependencies))]
        public static BuildTargetResult CheckPlatformDependencies(BuildTargetContext c) => c.Success();

        [Target(nameof(CheckUbuntuCoreclrAndCoreFxDependencies), nameof(CheckCentOSCoreclrAndCoreFxDependencies))]
        public static BuildTargetResult CheckCoreclrPlatformDependencies(BuildTargetContext c) => c.Success();

        [Target(nameof(CheckUbuntuDebianPackageBuildDependencies))]
        public static BuildTargetResult CheckInstallerBuildPlatformDependencies(BuildTargetContext c) => c.Success();

        // All major targets will depend on this in order to ensure variables are set up right if they are run independently
        [Target(nameof(GenerateVersions), nameof(CheckPrereqs), nameof(LocateStage0), nameof(ExpectedBuildArtifacts))]
        public static BuildTargetResult Init(BuildTargetContext c)
        {
            var runtimeInfo = PlatformServices.Default.Runtime;

            var configEnv = Environment.GetEnvironmentVariable("CONFIGURATION");

            if (string.IsNullOrEmpty(configEnv))
            {
                configEnv = "Debug";
            }

            c.BuildContext["Configuration"] = configEnv;
            c.BuildContext["Channel"] = Environment.GetEnvironmentVariable("CHANNEL");
            c.BuildContext["SharedFrameworkNugetVersion"] = GetVersionFromProjectJson(Path.Combine(Dirs.RepoRoot, "src", "sharedframework", "framework", "project.json"));

            c.Info($"Building {c.BuildContext["Configuration"]} to: {Dirs.Output}");
            c.Info("Build Environment:");
            c.Info($" Operating System: {runtimeInfo.OperatingSystem} {runtimeInfo.OperatingSystemVersion}");
            c.Info($" Platform: {runtimeInfo.OperatingSystemPlatform}");

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

            var branchInfo = ReadBranchInfo(c, Path.Combine(c.BuildContext.BuildDirectory, "branchinfo.txt"));
            var buildVersion = new BuildVersion()
            {
                Major = int.Parse(branchInfo["MAJOR_VERSION"]),
                Minor = int.Parse(branchInfo["MINOR_VERSION"]),
                Patch = int.Parse(branchInfo["PATCH_VERSION"]),
                ReleaseSuffix = branchInfo["RELEASE_SUFFIX"],
                CommitCount = commitCount
            };
            c.BuildContext["BuildVersion"] = buildVersion;
            c.BuildContext["CommitHash"] = commitHash;

            c.Info($"Building Version: {buildVersion.SimpleVersion} (NuGet Packages: {buildVersion.NuGetVersion})");
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

            // // Identify the version
            // string versionFile = Directory.GetFiles(stage0, ".version", SearchOption.AllDirectories).FirstOrDefault();

            // if (string.IsNullOrEmpty(versionFile))
            // {
            //     throw new Exception($"'.version' file not found in '{stage0}' folder");
            // }

            // var version = File.ReadAllLines(versionFile);
            // c.Info($"Using Stage 0 Version: {version[1]}");

            return c.Success();
        }

        [Target]
        public static BuildTargetResult ExpectedBuildArtifacts(BuildTargetContext c)
        {
            var config = Environment.GetEnvironmentVariable("CONFIGURATION");
            var versionBadgeName = $"{CurrentPlatform.Current}_{CurrentArchitecture.Current}_{config}_version_badge.svg";
            c.BuildContext["VersionBadge"] = Path.Combine(Dirs.Output, versionBadgeName);

            AddInstallerArtifactToContext(c, "dotnet-sdk", "Sdk");
            AddInstallerArtifactToContext(c, "dotnet-host", "SharedHost");
            AddInstallerArtifactToContext(c, "dotnet-sharedframework", "SharedFramework");
            AddInstallerArtifactToContext(c, "dotnet-dev", "CombinedFrameworkSDKHost");
            AddInstallerArtifactToContext(c, "dotnet", "CombinedFrameworkHost");

            return c.Success();
        }

        [Target]
        public static BuildTargetResult CheckPackageCache(BuildTargetContext c)
        {
            var ciBuild = string.Equals(Environment.GetEnvironmentVariable("CI_BUILD"), "1", StringComparison.Ordinal);

            if (ciBuild)
            {
                // On CI, HOME is redirected under the repo, which gets deleted after every build.
                // So make NUGET_PACKAGES outside of the repo.
                var nugetPackages = Path.GetFullPath(Path.Combine(c.BuildContext.BuildDirectory, "..", ".nuget", "packages"));
                Environment.SetEnvironmentVariable("NUGET_PACKAGES", nugetPackages);
                Dirs.NuGetPackages = nugetPackages;
            }

            // Set the package cache location in NUGET_PACKAGES just to be safe
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NUGET_PACKAGES")))
            {
                Environment.SetEnvironmentVariable("NUGET_PACKAGES", Dirs.NuGetPackages);
            }

            CleanNuGetTempCache();

            // Determine cache expiration time
            var cacheExpiration = 7 * 24; // cache expiration in hours
            var cacheExpirationStr = Environment.GetEnvironmentVariable("NUGET_PACKAGES_CACHE_TIME_LIMIT");
            if (!string.IsNullOrEmpty(cacheExpirationStr))
            {
                cacheExpiration = int.Parse(cacheExpirationStr);
            }

            if (ciBuild)
            {
                var cacheTimeFile = Path.Combine(Dirs.NuGetPackages, "packageCacheTime.txt");

                DateTime? cacheTime = null;
                try
                {
                    // Read the cache file
                    if (File.Exists(cacheTimeFile))
                    {
                        var content = File.ReadAllText(cacheTimeFile);
                        if (!string.IsNullOrEmpty(content))
                        {
                            cacheTime = DateTime.ParseExact("O", content, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
                        }
                    }
                }
                catch (Exception ex)
                {
                    c.Warn($"Error reading NuGet cache time file, leaving the cache alone");
                    c.Warn($"Error Detail: {ex.ToString()}");
                }

                if (cacheTime == null || (cacheTime.Value.AddHours(cacheExpiration) < DateTime.UtcNow))
                {
                    // Cache has expired or the status is unknown, clear it and write the file
                    c.Info("Clearing NuGet cache");
                    Rmdir(Dirs.NuGetPackages);
                    Mkdirp(Dirs.NuGetPackages);
                    File.WriteAllText(cacheTimeFile, DateTime.UtcNow.ToString("O"));
                }
            }

            return c.Success();
        }

        [Target(nameof(CheckPackageCache))]
        public static BuildTargetResult RestorePackages(BuildTargetContext c)
        {
            var dotnet = DotNetCli.Stage0;

            dotnet.Restore("--verbosity", "verbose", "--disable-parallel").WorkingDirectory(Path.Combine(c.BuildContext.BuildDirectory, "src")).Execute().EnsureSuccessful();
            dotnet.Restore("--verbosity", "verbose", "--disable-parallel").WorkingDirectory(Path.Combine(c.BuildContext.BuildDirectory, "tools")).Execute().EnsureSuccessful();

            return c.Success();
        }

        [Target]
        [BuildPlatforms(BuildPlatform.Ubuntu)]
        public static BuildTargetResult CheckUbuntuDebianPackageBuildDependencies(BuildTargetContext c)
        {

            var messageBuilder = new StringBuilder();
            var aptDependencyUtility = new AptDependencyUtility();


            foreach (var package in PackageDependencies.DebianPackageBuildDependencies)
            {
                if (!AptDependencyUtility.PackageIsInstalled(package))
                {
                    messageBuilder.Append($"Error: Debian package build dependency {package} missing.");
                    messageBuilder.Append(Environment.NewLine);
                    messageBuilder.Append($"-> install with apt-get install {package}");
                    messageBuilder.Append(Environment.NewLine);
                }
            }

            if (messageBuilder.Length == 0)
            {
                return c.Success();
            }
            else
            {
                return c.Failed(messageBuilder.ToString());
            }
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

        private static string GetVersionFromProjectJson(string pathToProjectJson)
        {
            Regex r = new Regex($"\"{Regex.Escape(Monikers.SharedFrameworkName)}\"\\s*:\\s*\"(?'version'[^\"]*)\"");

            foreach (var line in File.ReadAllLines(pathToProjectJson))
            {
                var m = r.Match(line);

                if (m.Success)
                {
                    return m.Groups["version"].Value;
                }
            }

            throw new InvalidOperationException("Unable to match the version name from " + pathToProjectJson);
        }

        private static bool AptPackageIsInstalled(string packageName)
        {
            var result = Command.Create("dpkg", "-s", packageName)
                .CaptureStdOut()
                .CaptureStdErr()
                .QuietBuildReporter()
                .Execute();

            return result.ExitCode == 0;
        }

        private static IDictionary<string, string> ReadBranchInfo(BuildTargetContext c, string path)
        {
            var lines = File.ReadAllLines(path);
            var dict = new Dictionary<string, string>();
            c.Verbose("Branch Info:");
            foreach (var line in lines)
            {
                if (!line.Trim().StartsWith("#") && !string.IsNullOrWhiteSpace(line))
                {
                    var splat = line.Split(new[] { '=' }, 2);
                    dict[splat[0]] = splat[1];
                    c.Verbose($" {splat[0]} = {splat[1]}");
                }
            }
            return dict;
        }

        private static void AddInstallerArtifactToContext(BuildTargetContext c, string artifactPrefix, string contextPrefix)
        {
            var productName = Monikers.GetProductMoniker(c, artifactPrefix);

            var extension = CurrentPlatform.IsWindows ? ".zip" : ".tar.gz";
            c.BuildContext[contextPrefix + "CompressedFile"] = Path.Combine(Dirs.Packages, productName + extension);

            string installer = "";
            switch (CurrentPlatform.Current)
            {
                case BuildPlatform.Windows:
                    installer = productName + ".exe";
                    break;
                case BuildPlatform.OSX:
                    installer = productName + ".pkg";
                    break;
                case BuildPlatform.Ubuntu:
                    installer = productName + ".deb";
                    break;
                default:
                    break;
            }

            if (!string.IsNullOrEmpty(installer))
            {
                c.BuildContext[contextPrefix + "InstallerFile"] = Path.Combine(Dirs.Packages, installer);
            }

        }
    }
}
