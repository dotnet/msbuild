using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Cli.Build.Framework;
using Microsoft.DotNet.InternalAbstractions;
using static Microsoft.DotNet.Cli.Build.Framework.BuildHelpers;
using static Microsoft.DotNet.Cli.Build.FS;
using static Microsoft.DotNet.Cli.Build.Utils;

namespace Microsoft.DotNet.Cli.Build
{
    public class PrepareTargets : Task
    {
        public override bool Execute()
        {
            BuildContext context = new BuildSetup("MSBuild").UseAllTargetsFromAssembly<PrepareTargets>().CreateBuildContext();
            BuildTargetContext c = new BuildTargetContext(context, null, null);

            return Prepare(c).Success;
        }

        [Target]
        public static BuildTargetResult Prepare(BuildTargetContext c)
        {
            Init(c);
            DownloadHostAndSharedFxArtifacts(c);
            RestorePackages(c);
            ZipTemplates(c);

            return c.Success();
        }

        // All major targets will depend on this in order to ensure variables are set up right if they are run independently
        public static BuildTargetResult Init(BuildTargetContext c)
        {
            GenerateVersions(c);
            CheckPrereqs.Run(s => c.Info(s));
            ExpectedBuildArtifacts(c);
            SetTelemetryProfile(c);

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

        public static BuildTargetResult GenerateVersions(BuildTargetContext c)
        {
            var commitCount = GitUtils.GetCommitCount();
            var commitHash = GitUtils.GetCommitHash();

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

            c.BuildContext["BranchName"] = branchInfo["BRANCH_NAME"];
            c.BuildContext["CommitHash"] = commitHash;

            c.Info($"Building Version: {buildVersion.SimpleVersion} (NuGet Packages: {buildVersion.NuGetVersion})");
            c.Info($"From Commit: {commitHash}");

            return c.Success();
        }

        public static BuildTargetResult ZipTemplates(BuildTargetContext c)
        {
            var templateDirectories = Directory.GetDirectories(
                Path.Combine(Dirs.RepoRoot, "src", "dotnet", "commands", "dotnet-new"));

            foreach (var directory in templateDirectories)
            {
                var zipFile = Path.Combine(Path.GetDirectoryName(directory), Path.GetFileName(directory) + ".zip");
                if (File.Exists(zipFile))
                {
                    File.Delete(zipFile);
                }

                ZipFile.CreateFromDirectory(directory, zipFile);
            }

            return c.Success();
        }

        

        public static BuildTargetResult ExpectedBuildArtifacts(BuildTargetContext c)
        {
            var config = Environment.GetEnvironmentVariable("CONFIGURATION");
            var versionBadgeName = $"{Monikers.GetBadgeMoniker()}_{config}_version_badge.svg";
            c.BuildContext["VersionBadge"] = Path.Combine(Dirs.Output, versionBadgeName);

            var cliVersion = c.BuildContext.Get<BuildVersion>("BuildVersion").NuGetVersion;
            var sharedFrameworkVersion = CliDependencyVersions.SharedFrameworkVersion;
            var hostVersion = CliDependencyVersions.SharedHostVersion;
            var hostFxrVersion = CliDependencyVersions.HostFxrVersion;

            // Generated Installers + Archives
            AddInstallerArtifactToContext(c, "dotnet-sdk", "Sdk", cliVersion);
            AddInstallerArtifactToContext(c, "dotnet-dev", "CombinedFrameworkSDKHost", cliVersion);
            AddInstallerArtifactToContext(c, "dotnet-sharedframework-sdk", "CombinedFrameworkSDK", cliVersion);
            AddInstallerArtifactToContext(c, "dotnet-sdk-debug", "SdkSymbols", cliVersion);

            //Downloaded Installers + Archives
            AddInstallerArtifactToContext(c, "dotnet-host", "SharedHost", hostVersion);
            AddInstallerArtifactToContext(c, "dotnet-hostfxr", "HostFxr", hostFxrVersion);
            AddInstallerArtifactToContext(c, "dotnet-sharedframework", "SharedFramework", sharedFrameworkVersion);
            AddInstallerArtifactToContext(c, "dotnet", "CombinedFrameworkHost", sharedFrameworkVersion);

            return c.Success();
        }

        public static BuildTargetResult DownloadHostAndSharedFxArtifacts(BuildTargetContext c)
        {
            ExpectedBuildArtifacts(c);
            DownloadHostAndSharedFxArchives(c);
            DownloadHostAndSharedFxInstallers(c);

            return c.Success();
        }

        public static BuildTargetResult DownloadHostAndSharedFxArchives(BuildTargetContext c)
        {
            var sharedFrameworkVersion = CliDependencyVersions.SharedFrameworkVersion;
            var sharedFrameworkChannel = CliDependencyVersions.SharedFrameworkChannel;

            var combinedSharedHostAndFrameworkArchiveDownloadFile =
                Path.Combine(CliDirs.CoreSetupDownload, "combinedSharedHostAndFrameworkArchive");

            Mkdirp(Path.GetDirectoryName(combinedSharedHostAndFrameworkArchiveDownloadFile));

            if (!File.Exists(combinedSharedHostAndFrameworkArchiveDownloadFile))
            {
                // Needed for computing the blob path
                var combinedSharedHostAndFrameworkArchiveBuildContextFile =
                    c.BuildContext.Get<string>("CombinedFrameworkHostCompressedFile");

                AzurePublisher.DownloadFile(
                    CalculateArchiveBlob(
                        combinedSharedHostAndFrameworkArchiveBuildContextFile,
                        sharedFrameworkChannel,
                        sharedFrameworkVersion),
                    combinedSharedHostAndFrameworkArchiveDownloadFile).Wait();


                // Unpack the combined archive to shared framework publish directory
                Rmdir(Dirs.SharedFrameworkPublish);
                Mkdirp(Dirs.SharedFrameworkPublish);
                if (CurrentPlatform.IsWindows)
                {
                    ZipFile.ExtractToDirectory(combinedSharedHostAndFrameworkArchiveDownloadFile, Dirs.SharedFrameworkPublish);
                }
                else
                {
                    Exec("tar", "xf", combinedSharedHostAndFrameworkArchiveDownloadFile, "-C", Dirs.SharedFrameworkPublish);
                }
            }

            return c.Success();
        }

        public static BuildTargetResult DownloadHostAndSharedFxInstallers(BuildTargetContext c)
        {
            if (CurrentPlatform.IsAnyPlatform(BuildPlatform.Windows, BuildPlatform.OSX, BuildPlatform.Ubuntu))
            {
                var sharedFrameworkVersion = CliDependencyVersions.SharedFrameworkVersion;
                var hostVersion = CliDependencyVersions.SharedHostVersion;
                var hostFxrVersion = CliDependencyVersions.HostFxrVersion;

                var sharedFrameworkChannel = CliDependencyVersions.SharedFrameworkChannel;
                var sharedHostChannel = CliDependencyVersions.SharedHostChannel;
                var hostFxrChannel = CliDependencyVersions.HostFxrChannel;

                var sharedFrameworkInstallerDownloadFile = Path.Combine(CliDirs.CoreSetupDownload, "sharedFrameworkInstaller");
                var sharedHostInstallerDownloadFile = Path.Combine(CliDirs.CoreSetupDownload, "sharedHostInstaller");
                var hostFxrInstallerDownloadFile = Path.Combine(CliDirs.CoreSetupDownload, "hostFxrInstaller");

                Mkdirp(Path.GetDirectoryName(sharedFrameworkInstallerDownloadFile));
                Mkdirp(Path.GetDirectoryName(sharedHostInstallerDownloadFile));
                Mkdirp(Path.GetDirectoryName(hostFxrInstallerDownloadFile));

                if (!File.Exists(sharedFrameworkInstallerDownloadFile))
                {
                    var sharedFrameworkInstallerDestinationFile = c.BuildContext.Get<string>("SharedFrameworkInstallerFile");
                    Mkdirp(Path.GetDirectoryName(sharedFrameworkInstallerDestinationFile));

                    AzurePublisher.DownloadFile(
                        CalculateInstallerBlob(
                            sharedFrameworkInstallerDestinationFile,
                            sharedFrameworkChannel,
                            sharedFrameworkVersion),
                        sharedFrameworkInstallerDownloadFile).Wait();

                    File.Copy(sharedFrameworkInstallerDownloadFile, sharedFrameworkInstallerDestinationFile, true);
                }

                if (!File.Exists(sharedHostInstallerDownloadFile))
                {
                    var sharedHostInstallerDestinationFile = c.BuildContext.Get<string>("SharedHostInstallerFile");
                    Mkdirp(Path.GetDirectoryName(sharedHostInstallerDestinationFile));

                    AzurePublisher.DownloadFile(
                       CalculateInstallerBlob(
                           sharedHostInstallerDestinationFile,
                           sharedHostChannel,
                           hostVersion),
                       sharedHostInstallerDownloadFile).Wait();

                    File.Copy(sharedHostInstallerDownloadFile, sharedHostInstallerDestinationFile, true);
                }

                if (!File.Exists(hostFxrInstallerDownloadFile))
                {
                    var hostFxrInstallerDestinationFile = c.BuildContext.Get<string>("HostFxrInstallerFile");
                    Mkdirp(Path.GetDirectoryName(hostFxrInstallerDestinationFile));

                    AzurePublisher.DownloadFile(
                       CalculateInstallerBlob(
                           hostFxrInstallerDestinationFile,
                           hostFxrChannel,
                           hostFxrVersion),
                       hostFxrInstallerDownloadFile).Wait();

                    File.Copy(hostFxrInstallerDownloadFile, hostFxrInstallerDestinationFile, true);
                }
            }

            return c.Success();
        }

        public static BuildTargetResult CheckPackageCache(BuildTargetContext c)
        {
            var ciBuild = string.Equals(Environment.GetEnvironmentVariable("CI_BUILD"), "1", StringComparison.Ordinal);

            // Always set the package cache location local to the build
            Environment.SetEnvironmentVariable("NUGET_PACKAGES", Dirs.NuGetPackages);

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

        public static BuildTargetResult RestorePackages(BuildTargetContext c)
        {
            CheckPackageCache(c);

            var dotnet = DotNetCli.Stage0;

            dotnet.Restore("--verbosity", "verbose", "--disable-parallel")
                .WorkingDirectory(Path.Combine(c.BuildContext.BuildDirectory, "src"))
                .Execute()
                .EnsureSuccessful();
            dotnet.Restore("--verbosity", "verbose", "--disable-parallel", "--infer-runtimes")
                .WorkingDirectory(Path.Combine(c.BuildContext.BuildDirectory, "tools"))
                .Execute()
                .EnsureSuccessful();

            return c.Success();
        }

        public static BuildTargetResult SetTelemetryProfile(BuildTargetContext c)
        {
            var gitResult = Cmd("git", "rev-parse", "HEAD")
                .CaptureStdOut()
                .Execute();
            gitResult.EnsureSuccessful();

            var commitHash = gitResult.StdOut.Trim();

            Environment.SetEnvironmentVariable("DOTNET_CLI_TELEMETRY_PROFILE", $"https://github.com/dotnet/cli;{commitHash}");

            return c.Success();
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

        private static void AddInstallerArtifactToContext(
            BuildTargetContext c,
            string artifactPrefix,
            string contextPrefix,
            string version)
        {
            var productName = Monikers.GetProductMoniker(c, artifactPrefix, version);

            var extension = CurrentPlatform.IsWindows ? ".zip" : ".tar.gz";
            c.BuildContext[contextPrefix + "CompressedFile"] = Path.Combine(Dirs.Packages, productName + extension);

            string installer = "";
            switch (CurrentPlatform.Current)
            {
                case BuildPlatform.Windows:
                    if (contextPrefix.Contains("Combined"))
                    {
                        installer = productName + ".exe";
                    }
                    else
                    {
                        installer = productName + ".msi";
                    }
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

        // The following CalculateBlob methods are temporary until the core-setup repo up-takes the new Azure Publish layout and
        // CLI consumes newer Shared FX versions.
        private static string CalculateArchiveBlob(string archiveFile, string channel, string version)
        {
            return $"{channel}/Binaries/{version}/{Path.GetFileName(archiveFile)}";
        }

        private static string CalculateInstallerBlob(string installerFile, string channel, string version)
        {
            return $"{channel}/Installers/{version}/{Path.GetFileName(installerFile)}";
        }
    }
}
