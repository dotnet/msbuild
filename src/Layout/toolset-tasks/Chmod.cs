// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Cli.Build
{
    public class Chmod : ToolTask
    {
        [Required]
        public string Glob { get; set; }

        [Required]
        public string Mode { get; set; }

        public bool Recursive { get; set; }

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
            return $"{GetRecursive()} {GetMode()} {GetGlob()}";
        }

        private string GetGlob()
        {
            return Glob;
        }

        private string GetMode()
        {
            return Mode;
        }

        private string GetRecursive()
        {
            if (Recursive)
            {
                return "--recursive";
            }

            return null;
        }
    }
}
