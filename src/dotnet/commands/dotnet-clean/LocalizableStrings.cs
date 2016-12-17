namespace Microsoft.DotNet.Tools.Clean
{
    internal class LocalizableStrings
    {
        public const string AppFullName = ".NET Clean Command";

        public const string AppDescription = "Command to clean previously generated build outputs.";

        public const string CmdArgProject = "PROJECT";

        public const string CmdArgProjDescription= "The MSBuild project file to build. If a project file is not specified, MSBuild searches the current working directory for a file that has a file extension that ends in `proj` and uses that file.";

        public const string CmdOutputDir = "OUTPUT_DIR";

        public const string CmdOutputDirDescription = "Directory in which the build outputs have been placed.";

        public const string CmdFramework = "FRAMEWORK";

        public const string CmdFrameworkDescription = "Clean a specific framework.";

        public const string CmdConfiguration = "CONFIGURATION";

        public const string CmdConfigurationDescription = "Clean a specific configuration.";
    }
}
