namespace Microsoft.DotNet.Tools.Pack
{
    internal class LocalizableStrings
    {
        public const string AppFullName = "pack";

        public const string AppDescription = "pack for msbuild";

        public const string CmdOutputDir  = "OUTPUT_DIR";

        public const string CmdOutputDirDescription = "Directory in which to place outputs";

        public const string CmdNoBuildOptionDescription = "Do not build project before packing";

        public const string CmdIncludeSymbolsDescription = "Include PDBs along with the DLLs in the output folder";

        public const string CmdIncludeSourceDescription  = "Include PDBs and source files. Source files go into the src folder in the resulting nuget package";

        public const string CmdConfig = "CONFIGURATION";

        public const string CmdConfigDescription = "Configuration under which to build";

        public const string CmdVersionSuffix = "VERSION_SUFFIX";

        public const string CmdVersionSuffixDescription = "Defines what `*` should be replaced with in version field in project.json";

        public const string CmdServiceableDescription = "Set the serviceable flag in the package";

        public const string CmdArgumentProject = "PROJECT";

        public const string CmdArgumentDescription = "The project to pack, defaults to the project file in the current directory. Can be a path to any project file";
    }
}
