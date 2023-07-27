// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.Http;
using Microsoft.Build.Utilities;
using Microsoft.NET.Sdk.Publish.Tasks.Properties;

namespace Microsoft.NET.Sdk.Publish.Tasks.Kudu
{
    public class KuduVfsDeploy : KuduConnect
    {
        private object _syncObject = new object();
        private TaskLoggingHelper _logger;
        private List<System.Threading.Tasks.Task> _postTasks;

        public KuduVfsDeploy(KuduConnectionInfo connectionInfo, TaskLoggingHelper logger)
            : base(connectionInfo, logger)
        {
            _logger = logger;
            _postTasks = new List<System.Threading.Tasks.Task>();
        }

        public override string DestinationUrl
        {
            get
            {
                return String.Format(ConnectionInfo.DestinationUrl, ConnectionInfo.SiteName, "vfs/site/wwwroot/");
            }
        }

        public System.Threading.Tasks.Task DeployAsync(string sourcePath)
        {
            if (!Directory.Exists(sourcePath))
            {
                return null;
            }

            List<string> files = Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories).ToList();

            foreach (var file in files)
            {
                var postTask = PostFilesAsync(file, sourcePath);
                _postTasks.Add(postTask);
            }

            return System.Threading.Tasks.Task.WhenAll(_postTasks);
        }

        private System.Threading.Tasks.Task PostFilesAsync(string file, string sourcePath)
        {
            return System.Threading.Tasks.Task.Run(
                async () =>
                {
                    string relPath = file.Replace(sourcePath, String.Empty);
                    string relUrl = relPath.Replace(Path.DirectorySeparatorChar, '/');
                    string apiUrl = DestinationUrl + relUrl;

                    using (var client = new HttpClient())
                    {
                        using (var content = new StreamContent(File.OpenRead(file)))
                        {
                            content.Headers.Remove("Content-Type");
                            content.Headers.Add("Content-Type", "application/octet-stream");

                            using (var req = new HttpRequestMessage(HttpMethod.Put, apiUrl))
                            {
                                req.Headers.Add("Authorization", "Basic " + AuthorizationInfo);
                                req.Headers.Add("If-Match", "*");

                                req.Content = content;
                                using (var response = await client.SendAsync(req))
                                {
                                    if (!response.IsSuccessStatusCode)
                                    {
                                        lock (_syncObject)
                                        {
                                            _logger.LogMessage(Microsoft.Build.Framework.MessageImportance.High, String.Format(Resources.KUDUDEPLOY_AddingFileFailed, ConnectionInfo.SiteName + "/" + relUrl, response.ReasonPhrase));
                                        }
                                    }
                                    else
                                    {
                                        lock (_syncObject)
                                        {
                                            _logger.LogMessage(Microsoft.Build.Framework.MessageImportance.High, String.Format(Resources.KUDUDEPLOY_AddingFile, ConnectionInfo.SiteName + "/" + relUrl));
                                        }
                                    }
                                }
                            }
                        }
                    }
                });
        }
    }
}
