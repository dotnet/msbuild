// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.Http;
using Microsoft.Build.Utilities;
using Microsoft.NET.Sdk.Publish.Tasks.Properties;

namespace Microsoft.NET.Sdk.Publish.Tasks.Kudu
{
    public class KuduZipDeploy : KuduConnect
    {
        private TaskLoggingHelper _logger;

        public KuduZipDeploy(KuduConnectionInfo connectionInfo, TaskLoggingHelper logger)
            : base(connectionInfo, logger)
        {
            _logger = logger;
        }

        public override string DestinationUrl
        {
            get
            {
                return string.Format(ConnectionInfo.DestinationUrl, ConnectionInfo.SiteName, "zip/site/wwwroot/");
            }
        }

        public async System.Threading.Tasks.Task<bool> DeployAsync(string zipFileFullPath)
        {

            if (!File.Exists(zipFileFullPath))
            {
                // If the source file directory does not exist quit early.
                _logger.LogError(string.Format(Resources.KUDUDEPLOY_AzurePublishErrorReason, Resources.KUDUDEPLOY_DeployOutputPathEmpty));
                return false;
            }

            var success = await PostZipAsync(zipFileFullPath);
            return success;
        }

        private async System.Threading.Tasks.Task<bool> PostZipAsync(string zipFilePath)
        {
            if (string.IsNullOrEmpty(zipFilePath))
            {
                return false;
            }

            using (var client = new HttpClient())
            {
                using (var content = new StreamContent(File.OpenRead(zipFilePath)))
                {
                    content.Headers.Remove("Content-Type");
                    content.Headers.Add("Content-Type", "application/octet-stream");

                    using (var req = new HttpRequestMessage(HttpMethod.Put, DestinationUrl))
                    {
                        req.Headers.Add("Authorization", "Basic " + AuthorizationInfo);
                        req.Content = content;

                        _logger.LogMessage(Build.Framework.MessageImportance.High, Resources.KUDUDEPLOY_PublishAzure);
                        using (var response = await client.SendAsync(req))
                        {
                            if (!response.IsSuccessStatusCode)
                            {
                                _logger.LogError(string.Format(Resources.KUDUDEPLOY_PublishZipFailedReason, ConnectionInfo.SiteName, response.ReasonPhrase));
                                return false;
                            }
                        }
                    }
                }
            }

            return true;
        }
    }
}
