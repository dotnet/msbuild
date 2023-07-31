// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.SdkCustomHelix.Sdk
{
    public sealed class TarGzFileCreateFromDirectory : ToolTask
    {
        /// <summary>
        /// The path to the directory to be archived.
        /// </summary>
        [Required]
        public string SourceDirectory { get; set; }

        /// <summary>
        /// The path of the archive to be created.
        /// </summary>
        [Required]
        public string DestinationArchive { get; set; }

        /// <summary>
        /// Indicates if the destination archive should be overwritten if it already exists.
        /// </summary>
        public bool OverwriteDestination { get; set; }

        /// <summary>
        /// If zipping an entire folder without exclusion patterns, whether to include the folder in the archive.
        /// </summary>
        public bool IncludeBaseDirectory { get; set; }

        /// <summary>
        /// An item group of regular expressions for content to exclude from the archive.
        /// </summary>
        public ITaskItem[] ExcludePatterns { get; set; }

        public bool IgnoreExitCode { get; set; }

        protected override int ExecuteTool(string pathToTool, string responseFileCommands, string commandLineCommands)
        {
            int returnCode = base.ExecuteTool(pathToTool, responseFileCommands, commandLineCommands);
            
            // returnCode 1 indicates that files changed during tar. In our case, it is likely being overwritten by another copy of the same file, so we can safely ignore this warning.
            if (IgnoreExitCode || returnCode == 1)
            {
                returnCode = 0;
            }

            return returnCode;
        }

        protected override bool ValidateParameters()
        {
            base.ValidateParameters();

            var retVal = true;

            if (File.Exists(DestinationArchive))
            {
                if (OverwriteDestination == true)
                {
                    Log.LogMessage(MessageImportance.Low, $"{DestinationArchive} will be overwritten");
                }
                else
                {
                    Log.LogError($"'{DestinationArchive}' already exists. Did you forget to set '{nameof(OverwriteDestination)}' to true?");

                    retVal = false;
                }
            }

            SourceDirectory = Path.GetFullPath(SourceDirectory);

            SourceDirectory = SourceDirectory.EndsWith(Path.DirectorySeparatorChar.ToString())
                ? SourceDirectory
                : SourceDirectory + Path.DirectorySeparatorChar;

            if (!Directory.Exists(SourceDirectory))
            {
                Log.LogError($"SourceDirectory '{SourceDirectory} does not exist.");

                retVal = false;
            }

            return retVal;
        }

        public override bool Execute()
        {
            return base.Execute();
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
            return $"{GetDestinationArchive()} {GetSourceSpecification()}";
        }

        private string GetSourceSpecification()
        {
            if (IncludeBaseDirectory)
            {
                var parentDirectory = Directory.GetParent(SourceDirectory).Parent.FullName;

                var sourceDirectoryName = Path.GetFileName(Path.GetDirectoryName(SourceDirectory));

                return $"--directory {parentDirectory} {sourceDirectoryName}  {GetExcludes()}";
            }
            else
            {
                return $"--directory {SourceDirectory}  {GetExcludes()} \".\"";
            }
        }

        private string GetDestinationArchive()
        {
            return $"-czf {DestinationArchive}";
        }

        private string GetExcludes()
        {
            var excludes = String.Empty;

            if (ExcludePatterns != null)
            {
                foreach (var excludeTaskItem in ExcludePatterns)
                {
                    excludes += $" --exclude {excludeTaskItem.ItemSpec}";
                }
            }
            
            return excludes;
        }
        
        protected override void LogToolCommand(string message)
        {
            base.LogToolCommand($"{base.GetWorkingDirectory()}> {message}");
        }
    }
}
