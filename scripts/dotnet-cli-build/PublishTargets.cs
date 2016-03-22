using System;
using System.IO;
using System.Net.Http;
using System.Text;
using Microsoft.DotNet.Cli.Build.Framework;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

using static Microsoft.DotNet.Cli.Build.Framework.BuildHelpers;

namespace Microsoft.DotNet.Cli.Build
{
    public static class PublishTargets
    {
        private static CloudBlobContainer BlobContainer { get; set; }

        private static string Channel { get; set; }

        private static string Version { get; set; }

        private static string NuGetVersion { get; set; }


        [Target]
        public static BuildTargetResult InitPublish(BuildTargetContext c)
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("CONNECTION_STRING").Trim('"'));
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            BlobContainer = blobClient.GetContainerReference("dotnet");

            Version = c.BuildContext.Get<BuildVersion>("BuildVersion").SimpleVersion;
            NuGetVersion = c.BuildContext.Get<BuildVersion>("BuildVersion").NuGetVersion;
            Channel = c.BuildContext.Get<string>("Channel");
            return c.Success();
        }

        [Target(nameof(PrepareTargets.Init),
        nameof(PublishTargets.InitPublish),
        nameof(PublishTargets.PublishArtifacts),
        nameof(PublishTargets.TriggerDockerHubBuilds))]
        [Environment("PUBLISH_TO_AZURE_BLOB", "1", "true")] // This is set by CI systems
        public static BuildTargetResult Publish(BuildTargetContext c)
        {
            return c.Success();
        }

        [Target(
            nameof(PublishTargets.PublishVersionBadge),
            nameof(PublishTargets.PublishSdkInstallerFile),
            nameof(PublishTargets.PublishDebFilesToDebianRepo),
            nameof(PublishTargets.PublishCombinedFrameworkSDKHostFile),
            nameof(PublishTargets.PublishCombinedFrameworkHostFile),
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
        [BuildPlatforms(BuildPlatform.Windows, BuildPlatform.OSX)]//, BuildPlatform.Ubuntu)]
        public static BuildTargetResult PublishSdkInstallerFile(BuildTargetContext c)
        {
            var installerFile = c.BuildContext.Get<string>("CombinedFrameworkSDKHostInstallerFile");
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
            var latestVersionFile = Path.Combine(Dirs.Stage2, "sdk", NuGetVersion, ".version");

            PublishFileAzure(latestVersionBlob, latestVersionFile);
            return c.Success();
        }

        [Target(
            nameof(PublishSdkDebToDebianRepo),
            nameof(PublishSharedFrameworkDebToDebianRepo),
            nameof(PublishSharedHostDebToDebianRepo))]
        [BuildPlatforms(BuildPlatform.Ubuntu)]
        public static BuildTargetResult PublishDebFilesToDebianRepo(BuildTargetContext c)
        {
            return c.Success();
        }

        [Target]
        [BuildPlatforms(BuildPlatform.Ubuntu)]
        public static BuildTargetResult PublishSdkDebToDebianRepo(BuildTargetContext c)
        {
            var packageName = Monikers.GetSdkDebianPackageName(c);
            var installerFile = c.BuildContext.Get<string>("SdkInstallerFile");
            var uploadUrl = $"https://dotnetcli.blob.core.windows.net/dotnet/{Channel}/Installers/{Version}/{Path.GetFileName(installerFile)}";
            var uploadJson = GenerateUploadJsonFile(packageName, Version, uploadUrl);

            Cmd(Path.Combine(Dirs.RepoRoot, "scripts", "publish", "repoapi_client.sh"), "-addpkg", uploadJson)
                    .Execute()
                    .EnsureSuccessful();

            return c.Success();
        }

        [Target]
        [BuildPlatforms(BuildPlatform.Ubuntu)]
        public static BuildTargetResult PublishSharedFrameworkDebToDebianRepo(BuildTargetContext c)
        {
            var packageName = Monikers.GetDebianSharedFrameworkPackageName(c);
            var installerFile = c.BuildContext.Get<string>("SharedFrameworkInstallerFile");
            var uploadUrl = $"https://dotnetcli.blob.core.windows.net/dotnet/{Channel}/Installers/{Version}/{Path.GetFileName(installerFile)}";
            var uploadJson = GenerateUploadJsonFile(packageName, Version, uploadUrl);

            Cmd(Path.Combine(Dirs.RepoRoot, "scripts", "publish", "repoapi_client.sh"), "-addpkg", uploadJson)
                    .Execute()
                    .EnsureSuccessful();

            return c.Success();
        }

        [Target]
        [BuildPlatforms(BuildPlatform.Ubuntu)]
        public static BuildTargetResult PublishSharedHostDebToDebianRepo(BuildTargetContext c)
        {
            var packageName = Monikers.GetDebianSharedHostPackageName(c);
            var installerFile = c.BuildContext.Get<string>("SharedHostInstallerFile");
            var uploadUrl = $"https://dotnetcli.blob.core.windows.net/dotnet/{Channel}/Installers/{Version}/{Path.GetFileName(installerFile)}";
            var uploadJson = GenerateUploadJsonFile(packageName, Version, uploadUrl);

            Cmd(Path.Combine(Dirs.RepoRoot, "scripts", "publish", "repoapi_client.sh"), "-addpkg", uploadJson)
                    .Execute()
                    .EnsureSuccessful();

            return c.Success();
        }

        [Target]
        [Environment("DOCKER_HUB_REPO")]
        [Environment("DOCKER_HUB_TRIGGER_TOKEN")]
        public static BuildTargetResult TriggerDockerHubBuilds(BuildTargetContext c)
        {
            string dockerHubRepo = Environment.GetEnvironmentVariable("DOCKER_HUB_REPO");
            string dockerHubTriggerToken = Environment.GetEnvironmentVariable("DOCKER_HUB_TRIGGER_TOKEN");

            Uri baseDockerHubUri = new Uri("https://registry.hub.docker.com/u/");
            Uri dockerHubTriggerUri;
            if (!Uri.TryCreate(baseDockerHubUri, $"{dockerHubRepo}/trigger/{dockerHubTriggerToken}/", out dockerHubTriggerUri))
            {
                return c.Failed("Invalid DOCKER_HUB_REPO and/or DOCKER_HUB_TRIGGER_TOKEN");
            }

            c.Info($"Triggering automated DockerHub builds for {dockerHubRepo}");
            using (HttpClient client = new HttpClient())
            {
                StringContent requestContent = new StringContent("{\"build\": true}", Encoding.UTF8, "application/json");
                try
                {
                    HttpResponseMessage response = client.PostAsync(dockerHubTriggerUri, requestContent).Result;
                    if (!response.IsSuccessStatusCode)
                    {
                        StringBuilder sb = new StringBuilder();
                        sb.AppendLine($"HTTP request to {dockerHubTriggerUri.ToString()} was unsuccessful.");
                        sb.AppendLine($"Response status code: {response.StatusCode}. Reason phrase: {response.ReasonPhrase}.");
                        sb.Append($"Respone content: {response.Content.ReadAsStringAsync().Result}");
                        return c.Failed(sb.ToString());
                    }
                }
                catch (AggregateException e)
                {
                    return c.Failed($"HTTP request to {dockerHubTriggerUri.ToString()} failed. {e.ToString()}");
                }
            }
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
        public static BuildTargetResult PublishCombinedFrameworkSDKHostFile(BuildTargetContext c)
        {
            var compressedFile = c.BuildContext.Get<string>("CombinedFrameworkSDKHostCompressedFile");
            var compressedFileBlob = $"{Channel}/Binaries/{Version}/{Path.GetFileName(compressedFile)}";
            var latestCompressedFile = compressedFile.Replace(Version, "latest");
            var latestCompressedFileBlob = $"{Channel}/Binaries/Latest/{Path.GetFileName(latestCompressedFile)}";

            PublishFileAzure(compressedFileBlob, compressedFile);
            PublishFileAzure(latestCompressedFileBlob, compressedFile);
            return c.Success();
        }

        [Target]
        public static BuildTargetResult PublishCombinedFrameworkHostFile(BuildTargetContext c)
        {
            var compressedFile = c.BuildContext.Get<string>("CombinedFrameworkHostCompressedFile");
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
