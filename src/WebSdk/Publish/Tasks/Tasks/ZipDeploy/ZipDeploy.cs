// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using Microsoft.Build.Framework;
using Microsoft.NET.Sdk.Publish.Tasks.MsDeploy;
using Microsoft.NET.Sdk.Publish.Tasks.Properties;

namespace Microsoft.NET.Sdk.Publish.Tasks.ZipDeploy
{
    public class ZipDeploy : Task
    {
        private const string UserAgentName = "websdk";

        [Required]
        public string ZipToPublishPath { get; set; }

        [Required]
        public string UserAgentVersion { get; set; }

        [Required]
        public string DestinationUsername { get; set; }

        public string DestinationPassword { get; set; }

        public string PublishUrl { get; set; }

        /// <summary>
        /// Our fallback if PublishUrl is not given, which is the case for ZIP Deploy profiles
        /// profiles created prior to 15.8 Preview 4. Using this will fail if the site is a slot.
        /// </summary>
        public string SiteName { get; set; }

        public override bool Execute()
        {
            string user = DestinationUsername;
            string password = DestinationPassword;

            if (string.IsNullOrEmpty(password) && !GetDestinationCredentials(out user, out password))
            {
                Log.LogError(Resources.ZIPDEPLOY_FailedToRetrieveCred);
                return false;
            }

            using (DefaultHttpClient client = new DefaultHttpClient())
            {
                System.Threading.Tasks.Task<bool> t = ZipDeployAsync(ZipToPublishPath, user, password, PublishUrl, SiteName, UserAgentVersion, client, true);
                t.Wait();
                return t.Result;
            }
        }

        public async System.Threading.Tasks.Task<bool> ZipDeployAsync(string zipToPublishPath, string userName, string password, string publishUrl, string siteName, string userAgentVersion, IHttpClient client, bool logMessages)
        {
            if (!File.Exists(zipToPublishPath) || client == null)
            {
                return false;
            }

            string zipDeployPublishUrl = null;

            if(!string.IsNullOrEmpty(publishUrl))
            {
                if (!publishUrl.EndsWith("/"))
                {
                    publishUrl += "/";
                }

                zipDeployPublishUrl = publishUrl + "api/zipdeploy";
            }
            else if(!string.IsNullOrEmpty(siteName))
            {
                zipDeployPublishUrl = $"https://{siteName}.scm.azurewebsites.net/api/zipdeploy";
            }
            else
            {
                if(logMessages)
                {
                    Log.LogError(Resources.ZIPDEPLOY_InvalidSiteNamePublishUrl);
                }

                return false;
            }

            if (logMessages)
            {
                Log.LogMessage(MessageImportance.High, string.Format(Resources.ZIPDEPLOY_PublishingZip, zipToPublishPath, zipDeployPublishUrl));
            }

            // use the async version of the api
            Uri uri = new Uri($"{zipDeployPublishUrl}?isAsync=true", UriKind.Absolute);
            string userAgent = $"{UserAgentName}/{userAgentVersion}";
            FileStream stream = File.OpenRead(zipToPublishPath);
            IHttpResponse response = await client.PostRequestAsync(uri, userName, password, "application/zip", userAgent, Encoding.UTF8, stream);
            if (response.StatusCode != HttpStatusCode.OK && response.StatusCode != HttpStatusCode.Accepted)
            {
                if (logMessages)
                {
                    Log.LogError(string.Format(Resources.ZIPDEPLOY_FailedDeploy, zipDeployPublishUrl, response.StatusCode));
                }

                return false;
            }
            else
            {
                if (logMessages)
                {
                    Log.LogMessage(Resources.ZIPDEPLOY_Uploaded);
                }

                string deploymentUrl = response.GetHeader("Location").FirstOrDefault();
                if (!string.IsNullOrEmpty(deploymentUrl))
                {
                    ZipDeploymentStatus deploymentStatus = new(client, userAgent, Log, logMessages);

                    DeploymentResponse deploymentResponse = await deploymentStatus.PollDeploymentStatusAsync(deploymentUrl, userName, password);
                    if (deploymentResponse?.Status == DeployStatus.Success)
                    {
                        Log.LogMessage(MessageImportance.High, Resources.ZIPDEPLOY_Succeeded);
                        return true;
                    }
                    else if (deploymentResponse is null || deploymentResponse?.Status == DeployStatus.Failed || deploymentResponse?.Status == DeployStatus.Unknown)
                    {
                        Log.LogError(string.Format(Resources.ZIPDEPLOY_FailedDeployWithLogs,
                            zipDeployPublishUrl,
                            deploymentResponse?.Status ?? DeployStatus.Unknown,
                            deploymentResponse?.GetLogUrlWithId()));

                        return false;
                    }
                }
            }

            return true;
        }

        private bool GetDestinationCredentials(out string user, out string password)
        {
            VSHostObject hostObj = new VSHostObject(HostObject as System.Collections.Generic.IEnumerable<ITaskItem>);
            return hostObj.ExtractCredentials(out user, out password);
        }
    }
}
