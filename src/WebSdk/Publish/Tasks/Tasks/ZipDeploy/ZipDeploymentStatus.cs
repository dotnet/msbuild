// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Build.Utilities;
using Microsoft.NET.Sdk.Publish.Tasks.Properties;

namespace Microsoft.NET.Sdk.Publish.Tasks.ZipDeploy
{
    public class ZipDeploymentStatus
    {
        private const int MaxMinutesToWait = 3;
        private const int StatusRefreshDelaySeconds = 3;
        private const int RetryCount = 3;
        private const int RetryDelaySeconds = 1;

        private readonly IHttpClient _client;
        private readonly string _userAgent;
        private readonly TaskLoggingHelper _log;
        private readonly bool _logMessages;

        public ZipDeploymentStatus(IHttpClient client, string userAgent, TaskLoggingHelper log, bool logMessages)
        {
            _client = client;
            _userAgent = userAgent;
            _log = log;
            _logMessages = logMessages;
        }

        public async System.Threading.Tasks.Task<DeploymentResponse> PollDeploymentStatusAsync(string deploymentUrl, string userName, string password)
        {
            var tokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(MaxMinutesToWait));

            if (_logMessages)
            {
                _log.LogMessage(Resources.ZIPDEPLOY_DeploymentStatusPolling);
            }

            DeploymentResponse deploymentResponse = null;
            DeployStatus? deployStatus = DeployStatus.Pending;

            while (!tokenSource.IsCancellationRequested
                && deployStatus != DeployStatus.Success
                && deployStatus != DeployStatus.Failed
                && deployStatus != DeployStatus.Unknown)
            {
                try
                {
                    deploymentResponse = await InvokeGetRequestWithRetryAsync<DeploymentResponse>(
                        deploymentUrl, userName, password, RetryCount, TimeSpan.FromSeconds(RetryDelaySeconds), tokenSource);

                    deployStatus = deploymentResponse?.Status is not null
                        ? deploymentResponse.Status
                        : DeployStatus.Unknown;

                    if (_logMessages)
                    {
                        _log.LogMessage(string.Format(Resources.ZIPDEPLOY_DeploymentStatus, Enum.GetName(typeof(DeployStatus), deployStatus)));
                    }
                }
                catch (HttpRequestException)
                {
                    return deploymentResponse;
                }

                await System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(StatusRefreshDelaySeconds));
            }

            return deploymentResponse ?? new() { Status = DeployStatus.Unknown };
        }

        private async System.Threading.Tasks.Task<T> InvokeGetRequestWithRetryAsync<T>(string url, string userName, string password, int retryCount, TimeSpan retryDelay, CancellationTokenSource cts)
        {
            IHttpResponse response = null;
            await RetryAsync(async () =>
            {
                response = await _client.GetRequestAsync(new Uri(url, UriKind.RelativeOrAbsolute), userName, password, _userAgent, cts.Token);
            }, retryCount, retryDelay);

            if (response.StatusCode != HttpStatusCode.OK && response.StatusCode != HttpStatusCode.Accepted)
            {
                return default(T);
            }
            else
            {
                using (var stream = await response.GetResponseBodyAsync())
                {
                    var reader = new StreamReader(stream, Encoding.UTF8);
                    return FromJson<T>(reader.ReadToEnd());
                }
            }
        }

        private async System.Threading.Tasks.Task RetryAsync(Func<System.Threading.Tasks.Task> func, int retryCount, TimeSpan retryDelay)
        {
            while (true)
            {
                try
                {
                    await func();
                    return;
                }
                catch (Exception)
                {
                    if (retryCount <= 0)
                    {
                        throw;
                    }
                    retryCount--;
                }

                await System.Threading.Tasks.Task.Delay(retryDelay);
            }
        }

        private static T FromJson<T>(string jsonString)
        {
            return JsonSerializer.Deserialize<T>(jsonString,
                new JsonSerializerOptions
                {
                    AllowTrailingCommas = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                });
        }
    }
}
