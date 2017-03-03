// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.Build.Framework;

using Task = Microsoft.Build.Utilities.Task;

namespace Microsoft.DotNet.Build.CloudTestTasks
{
    public sealed class CreateAzureContainer : Task
    {
        /// <summary>
        /// The Azure account key used when creating the connection string.
        /// </summary>
        [Required]
        public string AccountKey { get; set; }

        /// <summary>
        /// The Azure account name used when creating the connection string.
        /// </summary>
        [Required]
        public string AccountName { get; set; }

        /// <summary>
        /// The name of the container to create.  The specified name must be in the correct format, see the
        /// following page for more info.  https://msdn.microsoft.com/en-us/library/azure/dd135715.aspx
        /// </summary>
        [Required]
        public string ContainerName { get; set; }

        /// <summary>
        /// When false, if the specified container already exists get a reference to it.
        /// When true, if the specified container already exists the task will fail.
        /// </summary>
        public bool FailIfExists { get; set; }

        /// <summary>
        /// The read-only SAS token created when ReadOnlyTokenDaysValid is greater than zero.
        /// </summary>
        [Output]
        public string ReadOnlyToken { get; set; }

        /// <summary>
        /// The number of days for which the read-only token should be valid.
        /// </summary>
        public int ReadOnlyTokenDaysValid { get; set; }

        /// <summary>
        /// The URI of the created container.
        /// </summary>
        [Output]
        public string StorageUri { get; set; }

        /// <summary>
        /// The write-only SAS token create when WriteOnlyTokenDaysValid is greater than zero.
        /// </summary>
        [Output]
        public string WriteOnlyToken { get; set; }

        /// <summary>
        /// The number of days for which the write-only token should be valid.
        /// </summary>
        public int WriteOnlyTokenDaysValid { get; set; }

        public override bool Execute()
        {
            return ExecuteAsync().GetAwaiter().GetResult();
        }

        public async Task<bool> ExecuteAsync()
        {
            Log.LogMessage(
                MessageImportance.High,
                "Creating container named '{0}' in storage account {1}.",
                ContainerName,
                AccountName);
            string url = string.Format(
                "https://{0}.blob.core.windows.net/{1}?restype=container",
                AccountName,
                ContainerName);
            StorageUri = string.Format(
                "https://{0}.blob.core.windows.net/{1}/",
                AccountName,
                ContainerName);

            Log.LogMessage(MessageImportance.Low, "Sending request to create Container");
            using (HttpClient client = new HttpClient())
            {
                Func<HttpRequestMessage> createRequest = () =>
                {
                    DateTime dt = DateTime.UtcNow;
                    var req = new HttpRequestMessage(HttpMethod.Put, url);
                    req.Headers.Add(AzureHelper.DateHeaderString, dt.ToString("R", CultureInfo.InvariantCulture));
                    req.Headers.Add(AzureHelper.VersionHeaderString, AzureHelper.StorageApiVersion);
                    req.Headers.Add(AzureHelper.AuthorizationHeaderString, AzureHelper.AuthorizationHeader(
                            AccountName,
                            AccountKey,
                            "PUT",
                            dt,
                            req));
                    byte[] bytestoWrite = new byte[0];
                    int bytesToWriteLength = 0;

                    Stream postStream = new MemoryStream();
                    postStream.Write(bytestoWrite, 0, bytesToWriteLength);
                    req.Content = new StreamContent(postStream);

                    return req;
                };

                Func<HttpResponseMessage, bool> validate = (HttpResponseMessage response) =>
                {
                    // the Conflict status (409) indicates that the container already exists, so
                    // if FailIfExists is set to false and we get a 409 don't fail the task.
                    return response.IsSuccessStatusCode || (!FailIfExists && response.StatusCode == HttpStatusCode.Conflict);
                };

                using (HttpResponseMessage response = await AzureHelper.RequestWithRetry(Log, client, createRequest, validate))
                {
                    try
                    {
                        Log.LogMessage(
                            MessageImportance.Low,
                            "Received response to create Container {0}: Status Code: {1} {2}",
                            ContainerName, response.StatusCode, response.Content.ToString());

                        // specifying zero is valid, it means "I don't want a token"
                        if (ReadOnlyTokenDaysValid > 0)
                        {
                            ReadOnlyToken = AzureHelper.CreateContainerSasToken(
                                AccountName,
                                ContainerName,
                                AccountKey,
                                AzureHelper.SasAccessType.Read,
                                ReadOnlyTokenDaysValid);
                        }

                        // specifying zero is valid, it means "I don't want a token"
                        if (WriteOnlyTokenDaysValid > 0)
                        {
                            WriteOnlyToken = AzureHelper.CreateContainerSasToken(
                                AccountName,
                                ContainerName,
                                AccountKey,
                                AzureHelper.SasAccessType.Write,
                                WriteOnlyTokenDaysValid);
                        }
                    }
                    catch (Exception e)
                    {
                        Log.LogErrorFromException(e, true);
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
