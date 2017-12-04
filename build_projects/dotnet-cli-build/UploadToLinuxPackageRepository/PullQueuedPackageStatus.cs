// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.Cli.Build.UploadToLinuxPackageRepository
{
    internal class PullQueuedPackageStatus : IAzurelinuxRepositoryServiceHttpStrategy
    {
        private readonly QueueResourceLocation _queueResourceLocation;

        public PullQueuedPackageStatus(QueueResourceLocation queueResourceLocation)
        {
            _queueResourceLocation = queueResourceLocation
                                     ?? throw new ArgumentNullException(nameof(queueResourceLocation));
        }

        public async Task<string> Execute(HttpClient client, Uri baseAddress)
        {
            using (var response = await client.GetAsync(new Uri(baseAddress, _queueResourceLocation.Location)))
            {
                if (!response.IsSuccessStatusCode)
                    throw new FailedToAddPackageToPackageRepositoryException(
                        "Failed to make request to " + _queueResourceLocation.Location);
                var body = await response.Content.ReadAsStringAsync();
                return !body.Contains("status") ? "" : JObject.Parse(body)["status"].ToString();
            }
        }
    }
}
