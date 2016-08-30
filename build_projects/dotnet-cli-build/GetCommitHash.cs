// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Cli.Build
{
    public class GetCommitHash : ToolTask
    {
        [Required]
        public string RepoRoot { get; set; }

        [Output]
        public string CommitHash { get; set; }

        protected override string ToolName
        {
            get { return "git"; }
        }

        protected override MessageImportance StandardOutputLoggingImportance
        {
            get { return MessageImportance.High; } // or else the output doesn't get logged by default
        }

        protected override string GenerateFullPathToTool()
        {
            return "git";
        }

        protected override string GenerateCommandLineCommands()
        {
            return $"rev-parse HEAD";
        }

        protected override void LogEventsFromTextOutput(string line, MessageImportance importance)
        {
            CommitHash = line;
        }
    }
}