using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

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
                string appName = Path.GetFileName(TargetPath);

                string command = $"dotnet {appName}";
                if (UseAppHost || string.Equals(Path.GetExtension(TargetPath), ".exe", StringComparison.OrdinalIgnoreCase))
                {
                    command = Path.ChangeExtension(appName, !string.IsNullOrWhiteSpace(ExecutableExtension) ? ExecutableExtension : null);
                }

                File.WriteAllText(Path.Combine(WebJobsDirectory, RunCommandFile), command);
            }

            return true;
        }
    }
}
