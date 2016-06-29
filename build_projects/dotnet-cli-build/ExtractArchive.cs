using System.IO.Compression;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Cli.Build.Framework;
using static Microsoft.DotNet.Cli.Build.Framework.BuildHelpers;

namespace Microsoft.DotNet.Cli.Build
{
    public class ExtractArchive : Task
    {
        [Required]
        public string InputFile { get; set; }

        [Required]
        public string DestinationDirectory { get; set; }

        public override bool Execute()
        {
            FS.Mkdirp(DestinationDirectory);

            Log.LogMessage($"Extracting Archive '{InputFile}' to '{DestinationDirectory}'");

            if (CurrentPlatform.IsWindows)
            {
                ZipFile.ExtractToDirectory(InputFile, DestinationDirectory);
            }
            else
            {
                Exec("tar", "xf", InputFile, "-C", DestinationDirectory);
            }

            return true;
        }
    }
}
