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
    public class MsiTargets
    {
        private const string ENGINE = "engine.exe";

        private static string WixRoot
        {
            get
            {
                return Path.Combine(Dirs.Output, "WixTools");
            }
        }

        private static string Msi { get; set; }

        private static string Bundle { get; set; }

        private static string Engine { get; set; }

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
            // Download Wix version 3.10.2 - https://wix.codeplex.com/releases/view/619491
            Cmd("powershell", "-NoProfile", "-NoLogo",
                $"Invoke-WebRequest -Uri https://wix.codeplex.com/downloads/get/1540241 -Method Get -OutFile {WixRoot}\\WixTools.zip")
                    .Execute()
                    .EnsureSuccessful();

            c.Info("Extracting WixTools..");
            ZipFile.ExtractToDirectory($"{WixRoot}\\WixTools.zip", WixRoot);
        }

        [Target]
        [BuildPlatforms(BuildPlatform.Windows)]
        public static BuildTargetResult InitMsi(BuildTargetContext c)
        {
            Bundle = c.BuildContext.Get<string>("InstallerFile");
            Msi = Path.ChangeExtension(Bundle, "msi");
            Engine = Path.Combine(Path.GetDirectoryName(Bundle), ENGINE);

            var buildVersion = c.BuildContext.Get<BuildVersion>("BuildVersion");
            MsiVersion = buildVersion.GenerateMsiVersion();
            CliVersion = buildVersion.SimpleVersion;
            Channel = c.BuildContext.Get<string>("Channel");

            AcquireWix(c);
            return c.Success();
        }

        [Target(nameof(MsiTargets.InitMsi),
        nameof(GenerateDotnetMuxerMsi),
        nameof(GenerateDotnetSharedFxMsi),
        nameof(GenerateCLISDKMsi))]
        [BuildPlatforms(BuildPlatform.Windows)]
        public static BuildTargetResult GenerateMsis(BuildTargetContext c)
        {
            return c.Success();
        }

        [Target]
        [BuildPlatforms(BuildPlatform.Windows)]
        public static BuildTargetResult GenerateCLISDKMsi(BuildTargetContext c)
        {
            Cmd("powershell", "-NoProfile", "-NoLogo",
                Path.Combine(Dirs.RepoRoot, "packaging", "windows", "generatemsi.ps1"),
                Dirs.Stage2, Msi, WixRoot, MsiVersion, CliVersion, Arch, Channel)
                    .Execute()
                    .EnsureSuccessful();
            return c.Success();
        }

        [Target]
        [BuildPlatforms(BuildPlatform.Windows)]
        public static BuildTargetResult GenerateDotnetMuxerMsi(BuildTargetContext c)
        {
            return c.Success();
        }

        [Target]
        [BuildPlatforms(BuildPlatform.Windows)]
        public static BuildTargetResult GenerateDotnetSharedFxMsi(BuildTargetContext c)
        {
            return c.Success();
        }


        [Target(nameof(MsiTargets.InitMsi))]
        [BuildPlatforms(BuildPlatform.Windows)]
        public static BuildTargetResult GenerateBundle(BuildTargetContext c)
        {
            Cmd("powershell", "-NoProfile", "-NoLogo",
                Path.Combine(Dirs.RepoRoot, "packaging", "windows", "generatebundle.ps1"),
                Msi, Bundle, WixRoot, MsiVersion, CliVersion, Arch, Channel)
                    .EnvironmentVariable("Stage2Dir", Dirs.Stage2)
                    .Execute()
                    .EnsureSuccessful();
            return c.Success();
        }

        [Target(nameof(MsiTargets.InitMsi))]
        [BuildPlatforms(BuildPlatform.Windows)]
        public static BuildTargetResult ExtractEngineFromBundle(BuildTargetContext c)
        {
            Cmd($"{WixRoot}\\insignia.exe", "-ib", Bundle, "-o", Engine)
                    .Execute()
                    .EnsureSuccessful();
            return c.Success();
        }

        [Target(nameof(MsiTargets.InitMsi))]
        [BuildPlatforms(BuildPlatform.Windows)]
        public static BuildTargetResult ReattachEngineToBundle(BuildTargetContext c)
        {
            Cmd($"{WixRoot}\\insignia.exe", "-ab", Engine, Bundle, "-o", Bundle)
                    .Execute()
                    .EnsureSuccessful();
            return c.Success();
        }
    }
}
