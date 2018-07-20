// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !SOURCE_BUILD
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Microsoft.DotNet.Cli.Build
{
    public class AzurePublisher
    {
        public enum Product
        {
            SharedFramework,
            Host,
            HostFxr,
            Sdk,
        }

        private const string s_dotnetBlobContainerName = "dotnet";

        private string _connectionString { get; set; }
        private string _containerName { get; set; }
        private CloudBlobContainer _blobContainer { get; set; }

        public AzurePublisher(string containerName = s_dotnetBlobContainerName)
        {
            _connectionString = EnvVars.EnsureVariable("CONNECTION_STRING").Trim('"');
            _containerName = containerName;
            _blobContainer = GetDotnetBlobContainer(_connectionString, containerName);
        }

        public AzurePublisher(string accountName, string accountKey, string containerName = s_dotnetBlobContainerName)
        {
            _containerName = containerName;
            _blobContainer = GetDotnetBlobContainer(accountName, accountKey, containerName);
        }

        private CloudBlobContainer GetDotnetBlobContainer(string connectionString, string containerName)
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(connectionString);

            return GetDotnetBlobContainer(storageAccount, containerName);
        }

        private CloudBlobContainer GetDotnetBlobContainer(string accountName, string accountKey, string containerName)
        {
            var storageCredentials = new StorageCredentials(accountName, accountKey);
            var storageAccount = new CloudStorageAccount(storageCredentials, true);
            return GetDotnetBlobContainer(storageAccount, containerName);
        }

        private CloudBlobContainer GetDotnetBlobContainer(CloudStorageAccount storageAccount, string containerName)
        {
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

            return blobClient.GetContainerReference(containerName);
        }

        public string UploadFile(string file, Product product, string version)
        {
            string url = CalculateRelativePathForFile(file, product, version);
            CloudBlockBlob blob = _blobContainer.GetBlockBlobReference(url);
            blob.UploadFromFileAsync(file).Wait();
            SetBlobPropertiesBasedOnFileType(blob);
            return url;
        }

        public void PublishStringToBlob(string blob, string content)
        {
            CloudBlockBlob blockBlob = _blobContainer.GetBlockBlobReference(blob);
            blockBlob.UploadTextAsync(content).Wait();

            SetBlobPropertiesBasedOnFileType(blockBlob);
        }

        public void CopyBlob(string sourceBlob, string targetBlob)
        {
            CloudBlockBlob source = _blobContainer.GetBlockBlobReference(sourceBlob);
            CloudBlockBlob target = _blobContainer.GetBlockBlobReference(targetBlob);

            // Create the empty blob
            using (MemoryStream ms = new MemoryStream())
            {
                target.UploadFromStreamAsync(ms).Wait();
            }

            // Copy actual blob data
            target.StartCopyAsync(source).Wait();
        }

        public void SetBlobPropertiesBasedOnFileType(string path)
        {
            CloudBlockBlob blob = _blobContainer.GetBlockBlobReference(path);
            SetBlobPropertiesBasedOnFileType(blob);
        }

        private void SetBlobPropertiesBasedOnFileType(CloudBlockBlob blockBlob)
        {
            if (Path.GetExtension(blockBlob.Uri.AbsolutePath.ToLower()) == ".svg")
            {
                blockBlob.Properties.ContentType = "image/svg+xml";
                blockBlob.Properties.CacheControl = "no-cache";
                blockBlob.SetPropertiesAsync().Wait();
            }
            else if (Path.GetExtension(blockBlob.Uri.AbsolutePath.ToLower()) == ".version")
            {
                blockBlob.Properties.ContentType = "text/plain";
                blockBlob.Properties.CacheControl = "no-cache";
                blockBlob.SetPropertiesAsync().Wait();
            }
        }

        public IEnumerable<string> ListBlobs(Product product, string version)
        {
            string virtualDirectory = $"{product}/{version}";
            return ListBlobs(virtualDirectory);
        }

        public IEnumerable<string> ListBlobs(string virtualDirectory)
        {
            CloudBlobDirectory blobDir = _blobContainer.GetDirectoryReference(virtualDirectory);
            BlobContinuationToken continuationToken = new BlobContinuationToken();

            var blobFiles = blobDir.ListBlobsSegmentedAsync(continuationToken).Result;
            return blobFiles.Results.Select(bf => bf.Uri.PathAndQuery.Replace($"/{_containerName}/", string.Empty));
        }

        public string AcquireLeaseOnBlob(
            string blob,
            TimeSpan? maxWaitDefault = null,
            TimeSpan? delayDefault = null)
        {
            TimeSpan maxWait = maxWaitDefault ?? TimeSpan.FromSeconds(120);
            TimeSpan delay = delayDefault ?? TimeSpan.FromMilliseconds(500);

            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            // This will throw an exception with HTTP code 409 when we cannot acquire the lease
            // But we should block until we can get this lease, with a timeout (maxWaitSeconds)
            while (stopWatch.ElapsedMilliseconds < maxWait.TotalMilliseconds)
            {
                try
                {
                    CloudBlockBlob cloudBlob = _blobContainer.GetBlockBlobReference(blob);
                    Task<string> task = cloudBlob.AcquireLeaseAsync(TimeSpan.FromMinutes(1), null);
                    task.Wait();
                    return task.Result;
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Retrying lease acquisition on {blob}, {e.Message}");
                    Thread.Sleep(delay);
                }
            }

            throw new Exception($"Unable to acquire lease on {blob}");
        }

        public void ReleaseLeaseOnBlob(string blob, string leaseId)
        {
            CloudBlockBlob cloudBlob = _blobContainer.GetBlockBlobReference(blob);
            AccessCondition ac = new AccessCondition() { LeaseId = leaseId };
            cloudBlob.ReleaseLeaseAsync(ac).Wait();
        }

        public bool IsLatestSpecifiedVersion(string version)
        {
            Task<bool> task = _blobContainer.GetBlockBlobReference(version).ExistsAsync();
            task.Wait();
            return task.Result;
        }

        public void DropLatestSpecifiedVersion(string version)
        {
            CloudBlockBlob blob = _blobContainer.GetBlockBlobReference(version);
            using (MemoryStream ms = new MemoryStream())
            {
                blob.UploadFromStreamAsync(ms).Wait();
            }
        }

        public void CreateBlobIfNotExists(string path)
        {
            Task<bool> task = _blobContainer.GetBlockBlobReference(path).ExistsAsync();
            task.Wait();
            if (!task.Result)
            {
                CloudBlockBlob blob = _blobContainer.GetBlockBlobReference(path);
                using (MemoryStream ms = new MemoryStream())
                {
                    blob.UploadFromStreamAsync(ms).Wait();
                }
            }
        }

        public bool TryDeleteBlob(string path)
        {
            try
            {
                DeleteBlob(path);

                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Deleting blob {path} failed with \r\n{e.Message}");

                return false;
            }
        }

        private void DeleteBlob(string path)
        {
            _blobContainer.GetBlockBlobReference(path).DeleteAsync().Wait();
        }

        private static string CalculateRelativePathForFile(string file, Product product, string version)
        {
            return $"{product}/{version}/{Path.GetFileName(file)}";
        }
    }
}
#endif