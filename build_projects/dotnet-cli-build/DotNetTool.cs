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

    public class DotNetRestore : DotNetTool
    {
        protected override string Command
        {
            get { return "restore"; }
        }

        protected override string Args
        {
            get { return $"{GetVerbosity()}"; }
        }

        public string Verbosity { get; set; }

        private string GetVerbosity()
        {
            if (!string.IsNullOrEmpty(Verbosity))
            {
                return $"--verbosity {Verbosity}";
            }

            return null;
        }
    }

    public class DotNetTest : DotNetTool
    {
        protected override string Command
        {
            get { return "test"; }
        }

        protected override string Args
        {
            get { return $"{GetConfiguration()} {GetXml()} {GetNoTrait()}"; }
        }

        public string Configuration { get; set; }

        public string Xml { get; set; }

        public string NoTrait { get; set; }

        private string GetConfiguration()
        {
            if (!string.IsNullOrEmpty(Configuration))
            {
                return $"--configuration {Configuration}";
            }

            return null;
        }

        private string GetNoTrait()
        {
            if (!string.IsNullOrEmpty(Configuration))
            {
                return $"-notrait {NoTrait}";
            }

            return null;
        }

        private string GetXml()
        {
            if (!string.IsNullOrEmpty(Xml))
            {
                return $"-xml {Xml}";
            }

            return null;
        }
    }
}
