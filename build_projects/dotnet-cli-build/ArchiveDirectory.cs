using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Cli.Build.Framework;
using Microsoft.DotNet.InternalAbstractions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using static Microsoft.DotNet.Cli.Build.Framework.BuildHelpers;

namespace Microsoft.DotNet.Cli.Build
{
    public class ArchiveDirectory : Task
    {
        [Required]
        public string FileName { get; set; }

        [Required]
        public string OutputDirectory { get; set; }

        [Required]
        public string InputDirectory { get; set; }

        [Output]
        public string OutputArchive { get; set; }

        public override bool Execute()
        {
            if (!Directory.Exists(InputDirectory))
            {
                return true;
            }

            if (CurrentPlatform.IsPlatform(BuildPlatform.Windows))
            {
                OutputArchive = GenerateZip();
            }
            else 
            {
                OutputArchive = GenerateTarGz();
            }

            return true;
        }

        public string GenerateZip()
        {
            var extension = ".zip";
            var outFile = Path.Combine(OutputDirectory, FileName + extension);

            CreateZipFromDirectory(InputDirectory, outFile);

            return outFile;
        }

        public string GenerateTarGz()
        {
            var extension = ".tar.gz";
            var outFile = Path.Combine(OutputDirectory, FileName + extension);

            CreateTarGzFromDirectory(InputDirectory, outFile);

            return outFile;
        }

        private static void CreateZipFromDirectory(string directory, string outputArchivePath)
        {
            FS.Mkdirp(Path.GetDirectoryName(outputArchivePath));

            if (File.Exists(outputArchivePath))
            {
                File.Delete(outputArchivePath);
            }

            ZipFile.CreateFromDirectory(directory, outputArchivePath, CompressionLevel.Optimal, false);
        }

        private static void CreateTarGzFromDirectory(string directory, string outputArchivePath)
        {
            FS.Mkdirp(Path.GetDirectoryName(outputArchivePath));

            if (File.Exists(outputArchivePath))
            {
                File.Delete(outputArchivePath);
            }

            Cmd("tar", "-czf", outputArchivePath, "-C", directory, ".")
                .Execute()
                .EnsureSuccessful();
        }
    }
}
