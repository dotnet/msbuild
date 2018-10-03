// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Cli.Build
{
    public class DownloadFile : Microsoft.Build.Utilities.Task
    {
        [Required]
        public string Uri { get; set; }

        [Required]
        public string DestinationPath { get; set; }

        public bool Overwrite { get; set; }

        public override bool Execute()
        {

            ExponentialRetry.ExecuteWithRetry(
                  action: DownloadFileToDestination,
                  isSuccess: s => s == "",
                  maxRetryCount: 3,
                  timer: () => ExponentialRetry.Timer(ExponentialRetry.Intervals),
                  taskDescription: $"Download file from {Uri} to {DestinationPath}")
                .ConfigureAwait(false).GetAwaiter().GetResult();

            return true;

        }

        private async Task<string> DownloadFileToDestination()
        {
            FS.Mkdirp(Path.GetDirectoryName(DestinationPath));

            if (File.Exists(DestinationPath) && !Overwrite)
            {
                return string.Empty;
            }

            const string FileUriProtocol = "file://";

            if (Uri.StartsWith(FileUriProtocol, StringComparison.Ordinal))
            {
                var filePath = Uri.Substring(FileUriProtocol.Length);
                Log.LogMessage($"Copying '{filePath}' to '{DestinationPath}'");
                File.Copy(filePath, DestinationPath);
            }
            else
            {
                Log.LogMessage($"Downloading '{Uri}' to '{DestinationPath}'");

                using (var httpClient = new HttpClient())
                {
                    var getTask = httpClient.GetStreamAsync(Uri).ConfigureAwait(false);

                    try
                    {
                        using (var outStream = File.Create(DestinationPath))
                        {
                            Stream stream = await getTask;
                            stream.CopyTo(outStream);
                        }
                    }
                    catch (Exception e)
                    {
                        File.Delete(DestinationPath);
                        return e.ToString();
                    }
                }

            }

            return string.Empty;
        }
    }
}
