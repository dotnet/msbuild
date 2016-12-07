namespace Microsoft.DotNet.Tools.List.ProjectToProjectReferences
{
    internal class LocalizableStrings
    {
        public const string AppFullName = ".NET Core Project-to-Project dependency viewer";

        public const string AppDescription = "Command to list project to project (p2p) references";

        public const string ProjectArgumentValueName = "PROJECT";

        public const string ProjectArgumentDescription = "The project file to modify. If a project file is not specified, it searches the current working directory for an MSBuild file that has a file extension that ends in `proj` and uses that file.";

        public const string NoReferencesFound = "There are no {0} references in project {1}.\n{0} is the type of the item being requested (project, package, p2p) and {1} is the object operated on (a project file or a solution file). ";
    }
}
