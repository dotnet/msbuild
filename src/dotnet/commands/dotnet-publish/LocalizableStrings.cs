namespace Microsoft.DotNet.Tools.Publish
{
    internal class LocalizableStrings
    {
        public const string AppFullName = ".NET Publisher";

        public const string AppDescription = "Publisher for the .NET Platform";

        public const string ProjectArgument = "PROJECT";

        public const string ProjectArgDescription = "The MSBuild project file to publish. If a project file is not specified, MSBuild searches the current working directory for a file that has a file extension that ends in `proj` and uses that file.";

        public const string FrameworkOption = "FRAMEWORK";

        public const string FrameworkOptionDescription = " Target framework to publish for. The target framework has to be specified in the project file.";

        public const string RuntimeOption = "RUNTIME_IDENTIFIER";

        public const string RuntimeOptionDescription = "Publish the project for a given runtime. This is used when creating self-contained deployment. Default is to publish a framework-dependented app.";

        public const string OutputOption = "OUTPUT_DIR";

        public const string OutputOptionDescription = "Output directory in which to place the published artifacts.";

        public const string ConfigurationOption = "CONFIGURATION";

        public const string ConfigurationOptionDescription = "Configuration to use for building the project.  Default is  \"Debug\".";

        public const string VersionSuffixOption = "VERSION_SUFFIX";

        public const string VersionSuffixOptionDescription = "Defines the value for the $(VersionSuffix) property in the project.";
    }
}
