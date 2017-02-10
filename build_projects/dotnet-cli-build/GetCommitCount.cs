// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Runtime.InteropServices;

namespace Microsoft.DotNet.Cli.Build
{
    public class GetCommitCount : ToolTask
    {
        [Output]
        public string CommitCount { get; set; }

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
            // Workaround: https://github.com/Microsoft/msbuild/issues/1215
            // There's a "git" folder on the PATH in VS 2017 Developer command prompt and it causes msbuild to fail to execute git.
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "git.exe" : "git";
        }

        protected override string GenerateCommandLineCommands()
        {
            return $"rev-list --count HEAD";
        }

        protected override void LogEventsFromTextOutput(string line, MessageImportance importance)
        {
            CommitCount = line;
        }
    }
}
