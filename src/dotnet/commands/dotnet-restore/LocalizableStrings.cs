namespace Microsoft.DotNet.Tools.Restore
{
    internal class LocalizableStrings
    {
        public const string AppFullName = ".NET dependency restorer";

        public const string AppDescription = "restore for msbuild";

        public const string CmdArgument = "PROJECT";

        public const string CmdArgumentDescription = "Optional path to a project file or MSBuild arguments.";

        public const string CmdSourceOption = "SOURCE";

        public const string CmdSourceOptionDescription = "Specifies a NuGet package source to use during the restore.";

        public const string CmdPackagesOption = "PACKAGES_DIRECTORY";

        public const string CmdPackagesOptionDescription = "Directory to install packages in.";

        public const string CmdDisableParallelOptionDescription = "Disables restoring multiple projects in parallel.";

        public const string CmdConfigFileOption = "FILE";

        public const string CmdConfigFileOptionDescription = "The NuGet configuration file to use.";

        public const string CmdNoCacheOptionDescription = "Do not cache packages and http requests.";

        public const string CmdIgnoreFailedSourcesOptionDescription = "Treat package source failures as warnings.";

        public const string CmdNoDependenciesOptionDescription = "Set this flag to ignore project to project references and only restore the root project";
    }
}
