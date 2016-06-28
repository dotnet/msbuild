using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Cli.Build.Framework;
using Microsoft.DotNet.InternalAbstractions;

using static Microsoft.DotNet.Cli.Build.Framework.BuildHelpers;

namespace Microsoft.DotNet.Cli.Build
{
    public class MsiTargets
    {
        private const string ENGINE = "engine.exe";

        private const string WixVersion = "3.10.2";

        private static string WixRoot
        {
            get
            {
                return Path.Combine(Dirs.Output, $"WixTools.{WixVersion}");
            }
        }

        private static string SdkMsi { get; set; }

        private static string SdkBundle { get; set; }

        private static string HostFxrMsi { get; set; }

        private static string SharedHostMsi { get; set; }

        private static string SharedFrameworkMsi { get; set; }

        private static string SdkEngine { get; set; }

        private static string MsiVersion { get; set; }

        private static string CliDisplayVersion { get; set; }

        private static string CliNugetVersion { get; set; }

        private static string Arch { get; } = CurrentArchitecture.Current.ToString();

        private static void AcquireWix(BuildTargetContext c)
        {
            if (File.Exists(Path.Combine(WixRoot, "candle.exe")))
            {
                return;
            }

            Directory.CreateDirectory(WixRoot);

            c.Info("Downloading WixTools..");

            DownloadFile($"https://dotnetcli.blob.core.windows.net/build/wix/wix.{WixVersion}.zip", Path.Combine(WixRoot, "WixTools.zip"));

            c.Info("Extracting WixTools..");
            ZipFile.ExtractToDirectory(Path.Combine(WixRoot, "WixTools.zip"), WixRoot);
        }

        private static void DownloadFile(string uri, string destinationPath)
        {
            using (var httpClient = new HttpClient())
            {
                var getTask = httpClient.GetStreamAsync(uri);

                using (var outStream = File.OpenWrite(destinationPath))
                {
                    getTask.Result.CopyTo(outStream);
                }
            }
        }

        public static BuildTargetResult InitMsi(BuildTargetContext c)
        {
            if (CurrentPlatform.IsPlatform(BuildPlatform.Windows))
            {
                SdkBundle = c.BuildContext.Get<string>("CombinedFrameworkSDKHostInstallerFile");
                SdkMsi = Path.ChangeExtension(SdkBundle, "msi");
                SdkEngine = GetEngineName(SdkBundle);

                SharedFrameworkMsi = Path.ChangeExtension(c.BuildContext.Get<string>("SharedFrameworkInstallerFile"), "msi");
                HostFxrMsi = Path.ChangeExtension(c.BuildContext.Get<string>("HostFxrInstallerFile"), "msi");
                SharedHostMsi = Path.ChangeExtension(c.BuildContext.Get<string>("SharedHostInstallerFile"), "msi");

                var buildVersion = c.BuildContext.Get<BuildVersion>("BuildVersion");
                MsiVersion = buildVersion.GenerateMsiVersion();
                CliDisplayVersion = buildVersion.SimpleVersion;
                CliNugetVersion = buildVersion.NuGetVersion;

                AcquireWix(c);
            }

            return c.Success();
        }
        public static BuildTargetResult GenerateMsisAndBundles(BuildTargetContext c)
        {
            if (CurrentPlatform.IsPlatform(BuildPlatform.Windows))
            {
                InitMsi(c);
                GenerateCliSdkMsi(c);
                GenerateCliSdkBundle(c);
            }

            return c.Success();
        }

        public static BuildTargetResult GenerateCliSdkMsi(BuildTargetContext c)
        {
            if (CurrentPlatform.IsPlatform(BuildPlatform.Windows))
            {
                var cliSdkRoot = c.BuildContext.Get<string>("CLISDKRoot");
                var upgradeCode = GenerateGuidFromName.GenerateGuid(SdkMsi).ToString().ToUpper();
                var cliSdkBrandName = $"'{Monikers.CLISdkBrandName}'";

                Cmd("powershell", "-NoProfile", "-NoLogo",
                    Path.Combine(Dirs.RepoRoot, "packaging", "windows", "clisdk", "generatemsi.ps1"),
                    cliSdkRoot, SdkMsi, WixRoot, cliSdkBrandName, MsiVersion, CliDisplayVersion, CliNugetVersion, upgradeCode, Arch)
                        .Execute()
                        .EnsureSuccessful();
            }

            return c.Success();
        }

        public static BuildTargetResult GenerateCliSdkBundle(BuildTargetContext c)
        {
            if (CurrentPlatform.IsPlatform(BuildPlatform.Windows))
            {
                var upgradeCode = GenerateGuidFromName.GenerateGuid(SdkBundle).ToString().ToUpper();
                var cliSdkBrandName = $"'{Monikers.CLISdkBrandName}'";

                Cmd("powershell", "-NoProfile", "-NoLogo",
                    Path.Combine(Dirs.RepoRoot, "packaging", "windows", "clisdk", "generatebundle.ps1"),
                    SdkMsi, SharedFrameworkMsi, HostFxrMsi, SharedHostMsi, SdkBundle, WixRoot, cliSdkBrandName, MsiVersion, CliDisplayVersion, CliNugetVersion, upgradeCode, Arch)
                        .EnvironmentVariable("Stage2Dir", Dirs.Stage2)
                        .Execute()
                        .EnsureSuccessful();
            }

            return c.Success();
        }

        // TODO: The following "Engine" tasks need to be separate MSBuild targets so the Windows
        // VSO build can invoke them directly for signing.
        [Target(nameof(MsiTargets.InitMsi))]
        [BuildPlatforms(BuildPlatform.Windows)]
        public static BuildTargetResult ExtractEngineFromBundle(BuildTargetContext c)
        {
            ExtractEngineFromBundleHelper(SdkBundle, SdkEngine);
            return c.Success();
        }

        [Target(nameof(MsiTargets.InitMsi))]
        [BuildPlatforms(BuildPlatform.Windows)]
        public static BuildTargetResult ReattachEngineToBundle(BuildTargetContext c)
        {
            ReattachEngineToBundleHelper(SdkBundle, SdkEngine);
            return c.Success();
        }

        private static void ExtractEngineFromBundleHelper(string bundle, string engine)
        {
            Cmd($"{WixRoot}\\insignia.exe", "-ib", bundle, "-o", engine)
                    .Execute()
                    .EnsureSuccessful();
        }

        private static void ReattachEngineToBundleHelper(string bundle, string engine)
        {
            Cmd($"{WixRoot}\\insignia.exe", "-ab", engine, bundle, "-o", bundle)
                    .Execute()
                    .EnsureSuccessful();

            File.Delete(engine);
        }

        private static string GetEngineName(string bundle)
        {
            var engine = $"{Path.GetFileNameWithoutExtension(bundle)}-{ENGINE}";
            return Path.Combine(Path.GetDirectoryName(bundle), engine);
        }
    }
}
