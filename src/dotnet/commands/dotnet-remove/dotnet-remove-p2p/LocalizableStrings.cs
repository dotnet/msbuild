namespace Microsoft.DotNet.Tools.Remove.ProjectToProjectReference
{
    internal class LocalizableStrings
    {
        public const string AppFullName = ".NET Remove Project to Project (p2p) reference Command";

        public const string AppDescription = "Command to remove project to project (p2p) reference";

        public const string AppArgumentSeparatorHelpText = "Project to project references to remove";

        public const string CmdArgProject = "PROJECT";

        public const string CmdArgumentDescription = "The project file to modify. If a project file is not specified, it searches the current working directory for an MSBuild file that has a file extension that ends in `proj` and uses that file.";

        public const string CmdFramework = "FRAMEWORK";

        public const string CmdFrameworkDescription = "Remove reference only when targetting a specific framework";

        public const string ProjectException = "Project";

        public const string ReferenceNotFoundInTheProject = "Specified reference {0} does not exist in project {1}.";

        public const string ReferenceRemoved = "Reference `{0}` deleted from the project.";

        public const string SpecifyAtLeastOneReferenceToRemove = "You must specify at least one reference to delete. Please run dotnet delete --help for more information.";

        public const string ReferenceDeleted = "Reference `{0}` deleted.";

        public const string SpecifyAtLeastOneReferenceToDelete = "You must specify at least one reference to delete. Please run dotnet delete --help for more information.";

        public const string NetRemoveCommand = ".NET Remove Command";

        public const string Usage = "Usage";

        public const string Options = "Options";

        public const string HelpDefinition = "Show help information";

        public const string Arguments = "Arguments";

        public const string ArgumentsObjectDefinition = "The object of the operation. If a project file is not specified, it defaults to the current directory.";

        public const string ArgumentsCommandDefinition = "Command to be executed on <object>.";

        public const string ArgsDefinition = "Any extra arguments passed to the command. Use `dotnet add <command> --help` to get help about these arguments.";

        public const string Commands = "Commands";

        public const string CommandP2PDefinition = "Remove project to project (p2p) reference from a project";
    }
}
