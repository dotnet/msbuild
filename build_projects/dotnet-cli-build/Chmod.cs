// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Cli.Build
{
    public class Chmod : ToolTask
    {
        [Required]
        public string File { get; set; }

        [Required]
        public string Mode { get; set; }

        public bool Recursive { get; set; }

        protected override bool ValidateParameters()
        {
            base.ValidateParameters();

            if (!System.IO.File.Exists(File))
            {
                Log.LogError($"File '{File} does not exist.");

                return false;
            }

            return true;
        }

        protected override string ToolName
        {
            get { return "chmod"; }
        }

        protected override MessageImportance StandardOutputLoggingImportance
        {
            get { return MessageImportance.High; } // or else the output doesn't get logged by default
        }

        protected override string GenerateFullPathToTool()
        {
            return "chmod";
        }

        protected override string GenerateCommandLineCommands()
        {
            return $"{GetRecursive()} {GetMode()} {GetFilePath()}";
        }

        private string GetFilePath()
        {
            return File;
        }

        private string GetMode()
        {
            return Mode;
        }

        private string GetRecursive()
        {
            if(Recursive)
            {
                return "--recursive";
            }

            return null;
        }
    }
}
