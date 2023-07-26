// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.NET.Sdk.Publish.Tasks.WebJobs
{
    public class GenerateRunCommandFile : Task
    {
        private const string RunCommandFile = "run.cmd";
        [Required]
        public string ProjectDirectory { get; set; }
        [Required]
        public string WebJobsDirectory { get; set; }
        [Required]
        public string TargetPath { get; set; }
        [Required]
        public bool UseAppHost { get; set; }
        public string ExecutableExtension { get; set; }

        public override bool Execute()
        {
            bool isRunCommandFilePresent = File.Exists(Path.Combine(ProjectDirectory, RunCommandFile));
            if (!isRunCommandFilePresent)
            {
                string command = WebJobsCommandGenerator.RunCommand(TargetPath, UseAppHost, ExecutableExtension);
                File.WriteAllText(Path.Combine(WebJobsDirectory, RunCommandFile), command);
            }

            return true;
        }
    }
}
