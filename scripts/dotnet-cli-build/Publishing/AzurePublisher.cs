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
    public class AzurePublisher
    {
        private static readonly string s_dotnetBlobRootUrl = "https://dotnetcli.blob.core.windows.net/dotnet/";
        private static readonly string s_dotnetBlobContainerName = "dotnet";

        private string _connectionString { get; set; }
        private CloudBlobContainer _blobContainer { get; set; }

        public AzurePublisher()
        {
            _connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING").Trim('"');

            _blobContainer = GetDotnetBlobContainer(_connectionString);
        }

        private CloudBlobContainer GetDotnetBlobContainer(string connectionString)
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(connectionString);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            
            return blobClient.GetContainerReference(s_dotnetBlobContainerName);
        }

        public void PublishInstallerFileAndLatest(string installerFile, string channel, string version)
        {
            var installerFileBlob = CalculateInstallerBlob(installerFile, channel, version);

            var latestInstallerFileName = installerFile.Replace(version, "latest");
            var latestInstallerFileBlob = CalculateInstallerBlob(latestInstallerFileName, channel, "Latest");

            PublishFile(installerFileBlob, installerFile);
            PublishFile(latestInstallerFileBlob, installerFile);
        }

        public void PublishArchiveAndLatest(string archiveFile, string channel, string version)
        {
            var archiveFileBlob = CalculateArchiveBlob(archiveFile, channel, version);

            var latestArchiveFileName = archiveFile.Replace(version, "latest");
            var latestArchiveFileBlob = CalculateArchiveBlob(latestArchiveFileName, channel, "Latest");

            PublishFile(archiveFileBlob, archiveFile);
            PublishFile(latestArchiveFileBlob, archiveFile);
        }

        public void PublishFile(string blob, string file)
        {
            CloudBlockBlob blockBlob = _blobContainer.GetBlockBlobReference(blob);
            blockBlob.UploadFromFileAsync(file, FileMode.Open).Wait();

            SetBlobPropertiesBasedOnFileType(blockBlob);
        }

        private void SetBlobPropertiesBasedOnFileType(CloudBlockBlob blockBlob)
        {
            if (Path.GetExtension(blockBlob.Uri.AbsolutePath.ToLower()) == ".svg")
            {
                blockBlob.Properties.ContentType = "image/svg+xml";
                blockBlob.Properties.CacheControl = "no-cache";
                blockBlob.SetPropertiesAsync().Wait();
            }
        }

        public string CalculateInstallerUploadUrl(string installerFile, string channel, string version)
        {
            return $"{s_dotnetBlobRootUrl}{CalculateInstallerBlob(installerFile, channel, version)}";
        }

        public string CalculateInstallerBlob(string installerFile, string channel, string version)
        {
            return $"{channel}/Installers/{version}/{Path.GetFileName(installerFile)}";
        }

        public string CalculateArchiveUploadUrl(string archiveFile, string channel, string version)
        {
            return $"{s_dotnetBlobRootUrl}{CalculateArchiveBlob(archiveFile, channel, version)}";
        }

        public string CalculateArchiveBlob(string archiveFile, string channel, string version)
        {
            return $"{channel}/Binaries/{version}/{Path.GetFileName(archiveFile)}";
        }
    }
}
