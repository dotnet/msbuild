using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Cli.Build
{
    public abstract class DotNetTool : ToolTask
    {
        public DotNetTool()
        {
        }

        protected abstract string Command { get; }

        protected abstract string Args { get; }

        public string WorkingDirectory { get; set; }

        protected override string ToolName
        {
            get { return HostArtifactNames.DotnetHostBaseName; }
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
