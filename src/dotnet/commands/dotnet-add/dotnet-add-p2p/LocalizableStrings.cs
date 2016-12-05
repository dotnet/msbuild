namespace Microsoft.DotNet.Tools.Add.ProjectToProjectReference
{
    internal class LocalizableStrings
    {
        public const string AppFullName = ".NET Add Project to Project (p2p) reference Command";

        public const string AppDescription = "Command to add project to project (p2p) reference";

        public const string AppHelpText = "Project to project references to add";

        public const string CmdProject = "PROJECT";

        public const string CmdProjectDescription = "The project file to modify. If a project file is not specified, it searches the current working directory for an MSBuild file that has a file extension that ends in `proj` and uses that file.";

        public const string CmdFramework = "FRAMEWORK";

        public const string CmdFrameworkDescription = "Add reference only when targetting a specific framework";

        public const string CmdForceDescription = "Add reference even if it does not exist, do not convert paths to relative";

        public const string ProjectException = "Project";
    }
}