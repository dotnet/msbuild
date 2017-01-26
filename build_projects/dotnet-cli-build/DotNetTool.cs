// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Microsoft.DotNet.Cli.Build.Framework;

namespace Microsoft.DotNet.Cli.Build
{
    public abstract class DotNetTool : ToolTask
    {
        public DotNetTool()
        {
        }

        protected abstract string Command { get; }

        protected abstract string Args { get; }

        protected override Dictionary<string, string> EnvironmentOverride
        {
            get
            {
                var ev2r = new EnvironmentFilter()
                    .GetEnvironmentVariableNamesToRemove();

                foreach (var ev in ev2r)
                {
                    Console.WriteLine($"EV {ev}");
                }

                return ev2r
                    .ToDictionary(e => e, e => (string)null);
            }
        }

        public string WorkingDirectory { get; set; }

        protected override string ToolName
        {
            get { return $"dotnet{Constants.ExeSuffix}"; }
        }

        protected override MessageImportance StandardOutputLoggingImportance
        {
            get { return MessageImportance.High; } // or else the output doesn't get logged by default
        }

        protected override string GenerateFullPathToTool()
        {
            string path = ToolPath;

            // if ToolPath was not provided by the MSBuild script 
            if (string.IsNullOrEmpty(path))
            {
                Log.LogError($"Could not find the Path to {ToolName}");

                return string.Empty;
            }

            return path;
        }

        protected override string GetWorkingDirectory()
        {
            return WorkingDirectory ?? base.GetWorkingDirectory();
        }

        protected override string GenerateCommandLineCommands()
        {
            return $"{Command} {Args}";
        }

        protected override void LogToolCommand(string message)
        {
            base.LogToolCommand($"{GetWorkingDirectory()}> {message}");
        }
    }
}
