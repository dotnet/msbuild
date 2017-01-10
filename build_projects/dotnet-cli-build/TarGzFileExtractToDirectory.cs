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
    public sealed class TarGzFileExtractToDirectory : ToolTask
    {
        /// <summary>
        /// The path to the archive to extract.
        /// </summary>
        [Required]
        public string SourceArchive { get; set; }

        /// <summary>
        /// The path of the directory to which the archive should be extracted.
        /// </summary>
        [Required]
        public string DestinationDirectory { get; set; }

        /// <summary>
        /// Indicates if the destination directory should be cleaned if it already exists.
        /// </summary>
        public bool OverwriteDestination { get; set; }

        protected override bool ValidateParameters()
        {
            base.ValidateParameters();

            var retVal = true;

            if (Directory.Exists(DestinationDirectory))
            {
                if (OverwriteDestination == true)
                {
                    Log.LogMessage(MessageImportance.Low, "'{0}' already exists, trying to delete before unzipping...", DestinationDirectory);
                    Directory.Delete(DestinationDirectory, recursive: true);
                }
            }

            if (!File.Exists(SourceArchive))
            {
                Log.LogError($"SourceArchive '{SourceArchive} does not exist.");

                retVal = false;
            }

            if (retVal)
            {
                Log.LogMessage($"Creating Directory {DestinationDirectory}");
                Directory.CreateDirectory(DestinationDirectory);
            }
            
            return retVal;
        }

        public override bool Execute()
        {
            bool retVal = base.Execute();

            if (!retVal)
            {
                Log.LogMessage($"Deleting Directory {DestinationDirectory}");
                Directory.Delete(DestinationDirectory);
            }

            return retVal;
        }

        protected override string ToolName
        {
            get { return "tar"; }
        }

        protected override MessageImportance StandardOutputLoggingImportance
        {
            get { return MessageImportance.High; } // or else the output doesn't get logged by default
        }

        protected override string GenerateFullPathToTool()
        {
            return "tar";
        }

        protected override string GenerateCommandLineCommands()
        {
            return $"xf {GetSourceArchive()} -C {GetDestinationDirectory()}";
        }
        
        private string GetSourceArchive()
        {
            return SourceArchive;
        }

        private string GetDestinationDirectory()
        {
            return DestinationDirectory;
        }
    }
}
