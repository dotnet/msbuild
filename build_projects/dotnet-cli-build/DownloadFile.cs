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

            Log.LogMessage($"Downloading '{Uri}' to '{DestinationPath}'");

            using (var httpClient = new HttpClient())
            {
                var getTask = httpClient.GetStreamAsync(Uri);

                using (var outStream = File.Create(DestinationPath))
                {
                    getTask.Result.CopyTo(outStream);
                }
            }

            return true;
        }
    }
}
