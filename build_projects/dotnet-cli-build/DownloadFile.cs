// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Net.Http;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Cli.Build
{
    public class DownloadFile : Task
    {
        [Required]
        public string Uri { get; set; }

        [Required]
        public string DestinationPath { get; set; }

        public bool Overwrite { get; set; }

        public override bool Execute()
        {
            FS.Mkdirp(Path.GetDirectoryName(DestinationPath));

            if (File.Exists(DestinationPath) && !Overwrite)
            {
                return true;
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
                    var getTask = httpClient.GetStreamAsync(Uri);

                    try
                    {
                        using (var outStream = File.Create(DestinationPath))
                        {
                            getTask.Result.CopyTo(outStream);
                        }
                    }
                    catch (Exception)
                    {
                        File.Delete(DestinationPath);
                        throw;
                    }
                }
            }

            return true;
        }
    }
}
