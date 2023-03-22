// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Text.Json.Serialization;

namespace Microsoft.NET.Sdk.Publish.Tasks.ZipDeploy
{
    public class DeploymentResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("status")]
        public DeployStatus? Status { get; set; }

        [JsonPropertyName("log_url")]
        public string LogUrl { get; set; }
    }

    public static class DeploymentResponseExtensions
    {
        public static string GetLogUrlWithId(this DeploymentResponse deploymentResponse)
        {
            if (deploymentResponse is null
                || string.IsNullOrEmpty(deploymentResponse.LogUrl)
                || string.IsNullOrEmpty(deploymentResponse.Id))
            {
                return deploymentResponse?.LogUrl;
            }

            try
            {
                Uri logUrl = new(deploymentResponse.LogUrl);
                string pathAndQuery = logUrl.PathAndQuery;

                // try to replace '../latest/log' with '../{deploymentResponse.Id}/log'
                if (!string.IsNullOrEmpty(pathAndQuery))
                {
                    string[] pathAndQueryParts = pathAndQuery.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                    string[] pathWithIdParts = new string[pathAndQueryParts.Length];

                    for (int i = pathAndQueryParts.Length - 1; i >= 0; i--)
                    {
                        if (string.Equals("latest", pathAndQueryParts[i], StringComparison.Ordinal))
                        {
                            pathWithIdParts[i] = deploymentResponse.Id;
                            continue;
                        }

                        pathWithIdParts[i] = pathAndQueryParts[i].Trim();
                    }

                    return new UriBuilder()
                    {
                        Scheme = logUrl.Scheme,
                        Host = logUrl.Host,
                        Path = string.Join("/", pathWithIdParts)
                    }.ToString();
                }
            }
            catch
            {
                // do nothing
            }

            return deploymentResponse.LogUrl;
        }
    }
}

