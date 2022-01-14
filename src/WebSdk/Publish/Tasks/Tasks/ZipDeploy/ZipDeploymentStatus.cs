using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Utilities;
using Microsoft.NET.Sdk.Publish.Tasks.Properties;
using Task = System.Threading.Tasks.Task;

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

        public async Task<DeployStatus> PollDeploymentStatusAsync(string deploymentUrl, string userName, string password)
        {
            DeployStatus deployStatus = DeployStatus.Pending;
            var tokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(MaxMinutesToWait));
            if (_logMessages)
            {
                _log.LogMessage(Resources.ZIPDEPLOY_DeploymentStatusPolling);
            }
            while (!tokenSource.IsCancellationRequested && deployStatus != DeployStatus.Success && deployStatus != DeployStatus.Failed && deployStatus != DeployStatus.Unknown)
            {
                try
                {
                    deployStatus = await GetDeploymentStatusAsync(deploymentUrl, userName, password, RetryCount, TimeSpan.FromSeconds(RetryDelaySeconds), tokenSource);
                    if (_logMessages)
                    {
                        _log.LogMessage(String.Format(Resources.ZIPDEPLOY_DeploymentStatus, Enum.GetName(typeof(DeployStatus), deployStatus)));
                    }
                }
                catch (HttpRequestException)
                {
                    return DeployStatus.Unknown;
                }

                await Task.Delay(TimeSpan.FromSeconds(StatusRefreshDelaySeconds));
            }

            return deployStatus;
        }

        private async Task<DeployStatus> GetDeploymentStatusAsync(string deploymentUrl, string userName, string password, int retryCount, TimeSpan retryDelay, CancellationTokenSource cts)
        {
            Dictionary<string, object> json = await InvokeGetRequestWithRetryAsync<Dictionary<string, object>>(deploymentUrl, userName, password, retryCount, retryDelay, cts);
            object status = null;
            if (json!= null && !json.TryGetValue("status", out status))
            {
                return DeployStatus.Unknown;
            }

            if (status != null && Enum.TryParse(status.ToString(), out DeployStatus result))
            {
                return result;
            }

            return DeployStatus.Unknown;
        }

        private async Task<T> InvokeGetRequestWithRetryAsync<T>(string url, string userName, string password, int retryCount, TimeSpan retryDelay, CancellationTokenSource cts)
        {
            IHttpResponse response = null;
            await RetryAsync(async () =>
            {
                response = await _client.GetWithBasicAuthAsync(new Uri(url, UriKind.RelativeOrAbsolute), userName, password, _userAgent, cts.Token);
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

        private async Task RetryAsync(Func<Task> func, int retryCount, TimeSpan retryDelay)
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

                await Task.Delay(retryDelay);
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
