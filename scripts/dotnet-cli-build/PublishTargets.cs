using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Cli.Build.Framework;
using Microsoft.Extensions.PlatformAbstractions;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;

using static Microsoft.DotNet.Cli.Build.Framework.BuildHelpers;

namespace Microsoft.DotNet.Cli.Build
{
    public static class PublishTargets
    {
        private static CloudBlobContainer BlobContainer { get; set; }

        private static string Channel { get; set; }

        private static string Version { get; set; }


        [Target]
        public static BuildTargetResult InitPublish(BuildTargetContext c)
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("CONNECTION_STRING").Trim('"'));
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            BlobContainer = blobClient.GetContainerReference("dotnet");

            Version = c.BuildContext.Get<BuildVersion>("BuildVersion").SimpleVersion;
            Channel = c.BuildContext.Get<string>("Channel");
            return c.Success();
        }

        [Target(nameof(PrepareTargets.Init),
        nameof(PublishTargets.InitPublish),
        nameof(PublishTargets.PublishArtifacts))]
        [Environment("PUBLISH_TO_AZURE_BLOB", "1", "true")] // This is set by CI systems
        public static BuildTargetResult Publish(BuildTargetContext c)
        {
            return c.Success();
        }

        [Target(nameof(PublishTargets.PublishVersionBadge),
        nameof(PublishTargets.PublishCompressedFile),
        nameof(PublishTargets.PublishSdkInstallerFile),
        nameof(PublishTargets.PublishSharedFrameworkCompressedFile),
        nameof(PublishTargets.PublishSharedHostCompressedFile),
        nameof(PublishTargets.PublishLatestVersionTextFile))]
        public static BuildTargetResult PublishArtifacts(BuildTargetContext c)
        {
            return c.Success();
        }

        [Target]
        public static BuildTargetResult PublishVersionBadge(BuildTargetContext c)
        {
            var versionBadge = c.BuildContext.Get<string>("VersionBadge");
            var latestVersionBadgeBlob = $"{Channel}/Binaries/Latest/{Path.GetFileName(versionBadge)}";
            var versionBadgeBlob = $"{Channel}/Binaries/{Version}/{Path.GetFileName(versionBadge)}";

            PublishFileAzure(versionBadgeBlob, versionBadge);
            PublishFileAzure(latestVersionBadgeBlob, versionBadge);
            return c.Success();
        }

        [Target]
        public static BuildTargetResult PublishCompressedFile(BuildTargetContext c)
        {
            var compressedFile = c.BuildContext.Get<string>("SdkCompressedFile");
            var compressedFileBlob = $"{Channel}/Binaries/{Version}/{Path.GetFileName(compressedFile)}";
            var latestCompressedFile = compressedFile.Replace(Version, "latest");
            var latestCompressedFileBlob = $"{Channel}/Binaries/Latest/{Path.GetFileName(latestCompressedFile)}";

            PublishFileAzure(compressedFileBlob, compressedFile);
            PublishFileAzure(latestCompressedFileBlob, compressedFile);
            return c.Success();
        }

        [Target]
        [BuildPlatforms(BuildPlatform.Windows, BuildPlatform.OSX, BuildPlatform.Ubuntu)]
        public static BuildTargetResult PublishSdkInstallerFile(BuildTargetContext c)
        {
            var installerFile = c.BuildContext.Get<string>("SdkInstallerFile");
            var installerFileBlob = $"{Channel}/Installers/{Version}/{Path.GetFileName(installerFile)}";
            var latestInstallerFile = installerFile.Replace(Version, "latest");
            var latestInstallerFileBlob = $"{Channel}/Installers/Latest/{Path.GetFileName(latestInstallerFile)}";

            PublishFileAzure(installerFileBlob, installerFile);
            PublishFileAzure(latestInstallerFileBlob, installerFile);
            return c.Success();
        }

        [Target]
        public static BuildTargetResult PublishLatestVersionTextFile(BuildTargetContext c)
        {
            var osname = Monikers.GetOSShortName();
            var latestVersionBlob = $"{Channel}/dnvm/latest.{osname}.{CurrentArchitecture.Current}.version";
            var latestVersionFile = Path.Combine(Dirs.Stage2, ".version");

            PublishFileAzure(latestVersionBlob, latestVersionFile);
            return c.Success();
        }

        [Target(nameof(PublishSdkInstallerFile))]
        [BuildPlatforms(BuildPlatform.Ubuntu)]
        public static BuildTargetResult PublishDebFileToDebianRepo(BuildTargetContext c)
        {
            var packageName = Monikers.GetDebianPackageName(c);
            var installerFile = c.BuildContext.Get<string>("SdkInstallerFile");
            var uploadUrl =  $"https://dotnetcli.blob.core.windows.net/dotnet/{Channel}/Installers/{Version}/{Path.GetFileName(installerFile)}";
            var uploadJson = GenerateUploadJsonFile(packageName, Version, uploadUrl);

            Cmd(Path.Combine(Dirs.RepoRoot, "scripts", "publish", "repoapi_client.sh"), "-addpkg", uploadJson)
                    .Execute()
                    .EnsureSuccessful();

            return c.Success();
        }

        private static string GenerateUploadJsonFile(string packageName, string version, string uploadUrl)
        {
            var repoID = Environment.GetEnvironmentVariable("REPO_ID");
            var uploadJson = Path.Combine(Dirs.Packages, "package_upload.json");
            File.Delete(uploadJson);

            using (var fileStream = File.Create(uploadJson))
            {
                using (StreamWriter sw = new StreamWriter(fileStream))
                {
                   sw.WriteLine("{");
                   sw.WriteLine($"  \"name\":\"{packageName}\",");
                   sw.WriteLine($"  \"version\":\"{version}\",");
                   sw.WriteLine($"  \"repositoryId\":\"{repoID}\",");
                   sw.WriteLine($"  \"sourceUrl\":\"{uploadUrl}\"");
                   sw.WriteLine("}");
                }
            }

            return uploadJson;
        }

        [Target]
        public static BuildTargetResult PublishSharedFrameworkCompressedFile(BuildTargetContext c)
        {
            var compressedFile = c.BuildContext.Get<string>("SharedFrameworkCompressedFile");
            var compressedFileBlob = $"{Channel}/Binaries/{Version}/{Path.GetFileName(compressedFile)}";
            var latestCompressedFile = compressedFile.Replace(Version, "latest");
            var latestCompressedFileBlob = $"{Channel}/Binaries/Latest/{Path.GetFileName(latestCompressedFile)}";

            PublishFileAzure(compressedFileBlob, compressedFile);
            PublishFileAzure(latestCompressedFileBlob, compressedFile);
            return c.Success();
        }

        [Target]
        public static BuildTargetResult PublishSharedHostCompressedFile(BuildTargetContext c)
        {
            var compressedFile = c.BuildContext.Get<string>("SharedHostCompressedFile");
            var compressedFileBlob = $"{Channel}/Binaries/{Version}/{Path.GetFileName(compressedFile)}";
            var latestCompressedFile = compressedFile.Replace(Version, "latest");
            var latestCompressedFileBlob = $"{Channel}/Binaries/Latest/{Path.GetFileName(latestCompressedFile)}";

            PublishFileAzure(compressedFileBlob, compressedFile);
            PublishFileAzure(latestCompressedFileBlob, compressedFile);
            return c.Success();
        }

        private static BuildTargetResult PublishFile(BuildTargetContext c, string file)
        {
            var env = PackageTargets.GetCommonEnvVars(c);
            Cmd("powershell", "-NoProfile", "-NoLogo",
                Path.Combine(Dirs.RepoRoot, "scripts", "publish", "publish.ps1"), file)
                    .Environment(env)
                    .Execute()
                    .EnsureSuccessful();
            return c.Success();
        }

        private static void PublishFileAzure(string blob, string file)
        {
            CloudBlockBlob blockBlob = BlobContainer.GetBlockBlobReference(blob);
            blockBlob.UploadFromFileAsync(file, FileMode.Open).Wait();

            if (Path.GetExtension(blockBlob.Uri.AbsolutePath.ToLower()) == ".svg")
            {
                blockBlob.Properties.ContentType = "image/svg+xml";
                blockBlob.Properties.CacheControl = "no-cache";
                blockBlob.SetPropertiesAsync().Wait();
            }
        }
    }
}
