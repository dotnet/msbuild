// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

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

        public AzurePublisher(string containerName)
        {
            throw new NotImplementedException();
        }

        public AzurePublisher(string accountName, string accountKey, string containerName)
        {
            throw new NotImplementedException();
        }

        public string UploadFile(string file, Product product, string version)
        {
            throw new NotImplementedException();
        }

        public void PublishStringToBlob(string blob, string content)
        {
            throw new NotImplementedException();
        }

        public void CopyBlob(string sourceBlob, string targetBlob)
        {
            throw new NotImplementedException();
        }

        public void SetBlobPropertiesBasedOnFileType(string path)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<string> ListBlobs(Product product, string version)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<string> ListBlobs(string virtualDirectory)
        {
            throw new NotImplementedException();
        }

        public string AcquireLeaseOnBlob(
            string blob,
            TimeSpan? maxWaitDefault = null,
            TimeSpan? delayDefault = null)
        {
            throw new NotImplementedException();
        }

        public void ReleaseLeaseOnBlob(string blob, string leaseId)
        {
            throw new NotImplementedException();
        }

        public bool IsLatestSpecifiedVersion(string version)
        {
            throw new NotImplementedException();
        }

        public void DropLatestSpecifiedVersion(string version)
        {
            throw new NotImplementedException();
        }

        public void CreateBlobIfNotExists(string path)
        {
            throw new NotImplementedException();
        }

        public bool TryDeleteBlob(string path)
        {
            throw new NotImplementedException();
        }
    }
}
