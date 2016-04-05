using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Cli.Build.Framework;
using Microsoft.Extensions.PlatformAbstractions;

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

        private static string SharedHostMsi { get; set; }

        private static string SharedFrameworkMsi { get; set; }

        private static string SharedFrameworkBundle { get; set; }

        private static string SdkEngine { get; set; }

        private static string SharedFrameworkEngine { get; set; }

        private static string MsiVersion { get; set; }

        private static string CliVersion { get; set; }

        private static string Arch { get; } = CurrentArchitecture.Current.ToString();

        private static string Channel { get; set; }

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

        [Target]
        [BuildPlatforms(BuildPlatform.Windows)]
        public static BuildTargetResult InitMsi(BuildTargetContext c)
        {
            SdkBundle = c.BuildContext.Get<string>("CombinedFrameworkSDKHostInstallerFile");
            SdkMsi = Path.ChangeExtension(SdkBundle, "msi");
            SdkEngine = GetEngineName(SdkBundle);

            SharedFrameworkBundle = c.BuildContext.Get<string>("CombinedFrameworkHostInstallerFile");
            SharedHostMsi = Path.ChangeExtension(c.BuildContext.Get<string>("SharedHostInstallerFile"), "msi");
            SharedFrameworkMsi = Path.ChangeExtension(c.BuildContext.Get<string>("SharedFrameworkInstallerFile"), "msi");
            SharedFrameworkEngine = GetEngineName(SharedFrameworkBundle);

            var buildVersion = c.BuildContext.Get<BuildVersion>("BuildVersion");
            MsiVersion = buildVersion.GenerateMsiVersion();
            CliVersion = buildVersion.SimpleVersion;
            Channel = c.BuildContext.Get<string>("Channel");

            AcquireWix(c);
            return c.Success();
        }

        [Target(nameof(MsiTargets.InitMsi),
        nameof(GenerateDotnetSharedHostMsi),
        nameof(GenerateDotnetSharedFrameworkMsi),
        nameof(GenerateCliSdkMsi))]
        [BuildPlatforms(BuildPlatform.Windows)]
        public static BuildTargetResult GenerateMsis(BuildTargetContext c)
        {
            return c.Success();
        }

        [Target(nameof(MsiTargets.InitMsi),
        nameof(GenerateCliSdkBundle),
        nameof(GenerateSharedFxBundle))]
        [BuildPlatforms(BuildPlatform.Windows)]
        public static BuildTargetResult GenerateBundles(BuildTargetContext c)
        {
            return c.Success();
        }

        [Target]
        [BuildPlatforms(BuildPlatform.Windows)]
        public static BuildTargetResult GenerateCliSdkMsi(BuildTargetContext c)
        {
            var cliSdkRoot = c.BuildContext.Get<string>("CLISDKRoot");
            var upgradeCode = Utils.GenerateGuidFromName(SdkMsi).ToString().ToUpper();

            Cmd("powershell", "-NoProfile", "-NoLogo",
                Path.Combine(Dirs.RepoRoot, "packaging", "windows", "clisdk", "generatemsi.ps1"),
                cliSdkRoot, SdkMsi, WixRoot, MsiVersion, CliVersion, upgradeCode, Arch, Channel)
                    .Execute()
                    .EnsureSuccessful();
            return c.Success();
        }

        [Target]
        [BuildPlatforms(BuildPlatform.Windows)]
        public static BuildTargetResult GenerateDotnetSharedHostMsi(BuildTargetContext c)
        {
            var inputDir = c.BuildContext.Get<string>("SharedHostPublishRoot");
            var wixObjRoot = Path.Combine(Dirs.Output, "obj", "wix", "sharedhost");

            if (Directory.Exists(wixObjRoot))
            {
                Utils.DeleteDirectory(wixObjRoot);
            }
            Directory.CreateDirectory(wixObjRoot);

            Cmd("powershell", "-NoProfile", "-NoLogo",
                Path.Combine(Dirs.RepoRoot, "packaging", "windows", "host", "generatemsi.ps1"),
                inputDir, SharedHostMsi, WixRoot, MsiVersion, CliVersion, Arch, wixObjRoot)
                    .Execute()
                    .EnsureSuccessful();
            return c.Success();
        }

        [Target]
        [BuildPlatforms(BuildPlatform.Windows)]
        public static BuildTargetResult GenerateDotnetSharedFrameworkMsi(BuildTargetContext c)
        {
            var inputDir = c.BuildContext.Get<string>("SharedFrameworkPublishRoot");
            var sharedFrameworkNuGetName = Monikers.SharedFrameworkName;
            var sharedFrameworkNuGetVersion = c.BuildContext.Get<string>("SharedFrameworkNugetVersion");
            var msiVerison = sharedFrameworkNuGetVersion.Split('-')[0];
            var upgradeCode = Utils.GenerateGuidFromName(SharedFrameworkMsi).ToString().ToUpper();
            var wixObjRoot = Path.Combine(Dirs.Output, "obj", "wix", "sharedframework");

            if (Directory.Exists(wixObjRoot))
            {
                Utils.DeleteDirectory(wixObjRoot);
            }
            Directory.CreateDirectory(wixObjRoot);

            Cmd("powershell", "-NoProfile", "-NoLogo",
                Path.Combine(Dirs.RepoRoot, "packaging", "windows", "sharedframework", "generatemsi.ps1"),
                inputDir, SharedFrameworkMsi, WixRoot, msiVerison, sharedFrameworkNuGetName, sharedFrameworkNuGetVersion, upgradeCode, Arch, wixObjRoot)
                    .Execute()
                    .EnsureSuccessful();
            return c.Success();
        }


        [Target(nameof(MsiTargets.InitMsi))]
        [BuildPlatforms(BuildPlatform.Windows)]
        public static BuildTargetResult GenerateCliSdkBundle(BuildTargetContext c)
        {
            var upgradeCode = Utils.GenerateGuidFromName(SdkBundle).ToString().ToUpper();

            Cmd("powershell", "-NoProfile", "-NoLogo",
                Path.Combine(Dirs.RepoRoot, "packaging", "windows", "clisdk", "generatebundle.ps1"),
                SdkMsi, SharedFrameworkMsi, SharedHostMsi, SdkBundle, WixRoot, MsiVersion, CliVersion, upgradeCode, Arch, Channel)
                    .EnvironmentVariable("Stage2Dir", Dirs.Stage2)
                    .Execute()
                    .EnsureSuccessful();
            return c.Success();
        }

        [Target(nameof(MsiTargets.InitMsi))]
        [BuildPlatforms(BuildPlatform.Windows)]
        public static BuildTargetResult GenerateSharedFxBundle(BuildTargetContext c)
        {
            var sharedFrameworkNuGetName = Monikers.SharedFrameworkName;
            var sharedFrameworkNuGetVersion = c.BuildContext.Get<string>("SharedFrameworkNugetVersion");
            var upgradeCode = Utils.GenerateGuidFromName(SharedFrameworkBundle).ToString().ToUpper();

            Cmd("powershell", "-NoProfile", "-NoLogo",
                Path.Combine(Dirs.RepoRoot, "packaging", "windows", "sharedframework", "generatebundle.ps1"),
                SharedFrameworkMsi, SharedHostMsi, SharedFrameworkBundle, WixRoot, MsiVersion, CliVersion, sharedFrameworkNuGetName, sharedFrameworkNuGetVersion, upgradeCode, Arch, Channel)
                    .Execute()
                    .EnsureSuccessful();
            return c.Success();
        }

        [Target(nameof(MsiTargets.InitMsi))]
        [BuildPlatforms(BuildPlatform.Windows)]
        public static BuildTargetResult ExtractEngineFromBundle(BuildTargetContext c)
        {
            ExtractEngineFromBundleHelper(SdkBundle, SdkEngine);
            ExtractEngineFromBundleHelper(SharedFrameworkBundle, SharedFrameworkEngine);
            return c.Success();
        }

        [Target(nameof(MsiTargets.InitMsi))]
        [BuildPlatforms(BuildPlatform.Windows)]
        public static BuildTargetResult ReattachEngineToBundle(BuildTargetContext c)
        {
            ReattachEngineToBundleHelper(SdkBundle, SdkEngine);
            ReattachEngineToBundleHelper(SharedFrameworkBundle, SharedFrameworkEngine);
            return c.Success();
        }

        private static string GetEngineName(string bundle)
        {
            var engine = $"{Path.GetFileNameWithoutExtension(bundle)}-{ENGINE}";
            return Path.Combine(Path.GetDirectoryName(bundle), engine);
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
    }
}
