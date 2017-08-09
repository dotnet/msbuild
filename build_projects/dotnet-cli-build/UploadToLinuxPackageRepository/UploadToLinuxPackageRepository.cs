// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Newtonsoft.Json.Linq;
using Task = Microsoft.Build.Utilities.Task;

namespace Microsoft.DotNet.Cli.Build.UploadToLinuxPackageRepository
{
    public class UploadToLinuxPackageRepository : Task
    {
        /// <summary>
        ///     The Azure repository service user name.
        /// </summary>
        [Required]
        public string Username { get; set; }

        /// <summary>
        ///     The Azure repository service Password.
        /// </summary>
        [Required]
        public string Password { get; set; }

        /// <summary>
        ///     The Azure repository service URL ex: "tux-devrepo.corp.microsoft.com".
        /// </summary>
        [Required]
        public string Server { get; set; }

        [Required]
        public string RepositoryId { get; set; }

        [Required]
        public string PathOfPackageToUpload { get; set; }

        [Required]
        public string PackageNameInLinuxPackageRepository { get; set; }


        [Required]
        public string PackageVersionInLinuxPackageRepository { get; set; }


        public override bool Execute()
        {
            ExecuteAsyncWithRetry().GetAwaiter().GetResult();
            return true;
        }

        private async System.Threading.Tasks.Task ExecuteAsyncWithRetry()
        {
            await ExponentialRetry.ExecuteWithRetry(
                UploadAndAddpackageAndEnsureItIsReady,
                s => s == "",
                maxRetryCount: 3,
                timer: () => ExponentialRetry.Timer(ExponentialRetry.Intervals),
                taskDescription: $"running {nameof(UploadAndAddpackageAndEnsureItIsReady)}");
        }

        private async Task<string> UploadAndAddpackageAndEnsureItIsReady()
        {
            try
            {
                var linuxPackageRepositoryDestiny =
                    new LinuxPackageRepositoryDestiny(Username, Password, Server, RepositoryId);
                var uploadResponse = await new LinuxPackageRepositoryHttpPrepare(
                    linuxPackageRepositoryDestiny,
                    new FileUploadStrategy(PathOfPackageToUpload)).RemoteCall();

                var idInRepositoryService = new IdInRepositoryService(JObject.Parse(uploadResponse)["id"].ToString());

                var addPackageResponse = await new LinuxPackageRepositoryHttpPrepare(
                    linuxPackageRepositoryDestiny,
                    new AddPackageStrategy(
                        idInRepositoryService,
                        PackageNameInLinuxPackageRepository,
                        PackageVersionInLinuxPackageRepository,
                        linuxPackageRepositoryDestiny.RepositoryId)).RemoteCall();

                var queueResourceLocation = new QueueResourceLocation(addPackageResponse);

                Func<Task<string>> pullQueuedPackageStatus = new LinuxPackageRepositoryHttpPrepare(
                    linuxPackageRepositoryDestiny,
                    new PullQueuedPackageStatus(queueResourceLocation)).RemoteCall;

                await ExponentialRetry.ExecuteWithRetry(
                    pullQueuedPackageStatus,
                    s => s == "fileReady",
                    5,
                    () => ExponentialRetry.Timer(ExponentialRetry.Intervals),
                    $"PullQueuedPackageStatus location: {queueResourceLocation.Location}");
                return "";
            }
            catch (FailedToAddPackageToPackageRepositoryException e)
            {
                return e.ToString();
            }
            catch (HttpRequestException e)
            {
                return e.ToString();
            }
        }
    }
}
