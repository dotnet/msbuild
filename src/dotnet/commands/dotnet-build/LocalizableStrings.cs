namespace Microsoft.DotNet.Tools.Build
{
    internal class LocalizableStrings
    {
        public const string AppDescription = "Builder for the .NET Platform. Delegates to the MSBuild 'Build' target in the project file.";

        public const string AppFullName = ".NET Builder";

        public const string ConfigurationOptionDescription = "Configuration under which to build";

        public const string ConfigurationOptionName = "CONFIGURATION";

        public const string FrameworkOptionDescription = "Compile a specific framework";

        public const string FrameworkOptionName = "FRAMEWORK";

        public const string NoDependenciesOptionDescription = "Set this flag to ignore project to project references and only build the root project";

        public const string NoIncrementialOptionDescription = "Set this flag to turn off incremental build";

        public const string OutputOptionDescription = "Directory in which to place outputs";

        public const string OutputOptionName = "OUTPUT_DIR";

        public const string ProjectArgumentDescription = "The MSBuild project file to build. If a project file is not specified, MSBuild searches the current working directory for a file that has a file extension that ends in `proj` and uses that file.";

        public const string ProjectArgumentValueName = "PROJECT";

        public const string RuntimeOptionDescription = "Target runtime to build for. The default is to build a portable application.";

        public const string RuntimeOptionName = "RUNTIME_IDENTIFIER";

        public const string VersionSuffixOptionDescription = "Defines the value for the $(VersionSuffix) property in the project";

        public const string VersionSuffixOptionName = "VERSION_SUFFIX";

    }
}
