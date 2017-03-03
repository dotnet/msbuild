// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.IO;
using System.IO.Compression;

namespace Microsoft.DotNet.Build.Tasks
{
    public sealed class ZipFileExtractToDirectory : Task
    {
        /// <summary>
        /// The path to the directory to be archived.
        /// </summary>
        [Required]
        public string SourceArchive { get; set; }

        /// <summary>
        /// The path of the archive to be created.
        /// </summary>
        [Required]
        public string DestinationDirectory { get; set; }

        /// <summary>
        /// Indicates if the destination directory should be cleaned if it already exists.
        /// </summary>
        public bool OverwriteDestination { get; set; }

        public override bool Execute()
        {
            try
            {
                if (Directory.Exists(DestinationDirectory))
                {
                    if (OverwriteDestination == true)
                    {
                        Log.LogMessage(MessageImportance.Low, "'{0}' already exists, trying to delete before unzipping...", DestinationDirectory);
                        Directory.Delete(DestinationDirectory, recursive: true);
                    }
                }

                Log.LogMessage(MessageImportance.High, "Decompressing '{0}' into '{1}'...", SourceArchive, DestinationDirectory);
                if (!Directory.Exists(Path.GetDirectoryName(DestinationDirectory)))
                    Directory.CreateDirectory(Path.GetDirectoryName(DestinationDirectory));

                // match tar default behavior to overwrite by default
                // Replace this code with ZipFile.ExtractToDirectory when https://github.com/dotnet/corefx/pull/14806 is available
                using (ZipArchive archive = ZipFile.Open(SourceArchive, ZipArchiveMode.Read))
                {
                    DirectoryInfo di = Directory.CreateDirectory(DestinationDirectory);
                    string destinationDirectoryFullPath = di.FullName;

                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        string fileDestinationPath = Path.GetFullPath(Path.Combine(destinationDirectoryFullPath, entry.FullName));

                        if (Path.GetFileName(fileDestinationPath).Length == 0)
                        {
                            // If it is a directory:
                            Directory.CreateDirectory(fileDestinationPath);
                        }
                        else
                        {
                            // If it is a file:
                            // Create containing directory:
                            Directory.CreateDirectory(Path.GetDirectoryName(fileDestinationPath));
                            entry.ExtractToFile(fileDestinationPath, overwrite: true);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                // We have 2 log calls because we want a nice error message but we also want to capture the callstack in the log.
                Log.LogError("An exception has occured while trying to decompress '{0}' into '{1}'.", SourceArchive, DestinationDirectory);
                Log.LogMessage(MessageImportance.Low, e.ToString());
                return false;
            }
            return true;
        }
    }
}
