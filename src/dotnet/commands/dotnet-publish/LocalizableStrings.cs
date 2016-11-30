namespace Microsoft.DotNet.Tools.Publish
{
    internal class LocalizableStrings
    {
        public const string AppFullName = ".NET Publisher";

        public const string AppDescription = "Publisher for the .NET Platform";

        public const string ProjectArgument = "PROJECT";

        public const string ProjectArgDescription = "The MSBuild project file to publish. If a project file is not specified, MSBuild searches the current working directory for a file that has a file extension that ends in `proj` and uses that file.";

        public const string FrameworkOption = "FRAMEWORK";

        public const string FrameworkOptionDescription = "Target framework to publish for";

        public const string RuntimeOption = "RUNTIME_IDENTIFIER";

        public const string RuntimeOptionDescription = "Target runtime to publish for. The default is to publish a portable application.";

        public const string OutputOption = "OUTPUT_DIR";

        public const string OutputOptionDescription = "Path in which to publish the app";

        public const string ConfigurationOption = "CONFIGURATION";

        public const string ConfigurationOptionDescription = "Configuration under which to build";

        public const string VersionSuffixOption = "VERSION_SUFFIX";

        public const string VersionSuffixOptionDescription = "Defines the value for the $(VersionSuffix) property in the project";
    }
}
