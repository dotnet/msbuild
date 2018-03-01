// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Build.Framework;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Build.CloudTestTasks
{
    public class EnsureFileExistsOnAzureBlobStorage : Microsoft.Build.Utilities.Task
    {
        [Required]
        public string FileUrl{ get; set; }

        public override bool Execute()
        {
            if (!ExecuteAsync().GetAwaiter().GetResult())
            {
                throw new FileDoesNotExistOnAzureBlobStorageException(FileUrl);

            }
            return true;
        }

        private Task<bool> ExecuteAsync()
        {
            var blobClient = new CloudBlockBlob(new Uri(FileUrl));
            return blobClient.ExistsAsync();
        }

        public class FileDoesNotExistOnAzureBlobStorageException : Exception
        {
            public FileDoesNotExistOnAzureBlobStorageException(string message) : base(message + " cannot be found on Azure Blob Storage")
            {
            }
        }
    }
}
