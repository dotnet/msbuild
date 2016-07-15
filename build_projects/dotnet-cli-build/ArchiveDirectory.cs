// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.IO.Compression;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Cli.Build.Framework;
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

        public bool ForceZipArchive { get; set; }

        [Output]
        public string OutputArchive { get; set; }

        public override bool Execute()
        {
            if (!Directory.Exists(InputDirectory))
            {
                return true;
            }

            if (CurrentPlatform.IsPlatform(BuildPlatform.Windows) || ForceZipArchive)
            {
                OutputArchive = GenerateZip();
            }
            else
            {
                OutputArchive = GenerateTarGz();
            }

            Log.LogMessage($"Created Archive '{OutputArchive}'");

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
