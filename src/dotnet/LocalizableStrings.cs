namespace Microsoft.DotNet.Tools
{
    internal class LocalizableStrings
    {
        public const string CouldNotFindAnyProjectInDirectory = "Could not find any project in `{0}`.";
        public const string MoreThanOneProjectInDirectory = "Found more than one project in `{0}`. Please specify which one to use.";
        public const string ProjectAlreadyHasAreference = "Project already has a reference to `{0}`.";
        public const string ProjectReferenceCouldNotBeFound = "Project reference `{0}` could not be found.";
        public const string ProjectReferenceRemoved = "Project reference `{0}` removed.";
        
        // General purpose words
        public const string RequiredArgument = "Required argument";
        public const string Option = "Option";
        public const string Argument = "Argument";
        public const string Help = "Help";

        // Project related
        public const string Project = "Project";
        public const string ProjectFile = "Project file";
        public const string Reference = "Reference";
        public const string ProjectReference = "Project reference";
        public const string PackageReference = "Package reference";
        public const string P2P = "Project to Project";
        public const string P2PReference = "Project to Project reference";
        public const string Package = "Package";
        public const string Solution = "Solution";
        public const string SolutionFile = "Solution file";
        public const string Executable = "Executable";
        public const string Library = "Library";
        public const string Program = "Program";
        public const string Application = "Application";

        // Verbs
        public const string Add = "Add";
        public const string Remove = "Remove";
        public const string Delete = "Delete";
        public const string Update = "Update";
        public const string New = "New";
        public const string List = "List";
        public const string Load = "Load";
        public const string Save = "Save";
        public const string Find = "Find";

        // Other
        public const string Error = "Error";
        public const string Warning = "Warning";

        public const string File = "File";
        public const string Directory = "Directory";

        public const string Type = "Type";
        public const string Value = "Value";
        public const string Group = "Group";

        // General sentences";
        public const string XAddedToY = "{0} added to {1}.";
        public const string XRemovedFromY = "{0} removed from {1}.";
        public const string XDeletedFromY = "{0} deleted from {1}.";
        public const string XSuccessfullyUpdated = "{0} successfully updated.";

        // General errors
        /// Invalid
        public const string XIsInvalid = "{0} is invalid.";
        public const string XYFoundButInvalid = "{0} `{1}` found but is invalid.";
        public const string XFoundButInvalid = "`{0}` found but is invalid.";
        public const string OperationInvalid = "Operation is invalid.";
        public const string OperationXInvalid = "Operation {0} is invalid.";

        /// Not Found
        public const string XNotFound = "{0} not found.";
        public const string XOrYNotFound = "{0} or {1} not found.";
        public const string XOrYNotFoundInZ = "{0} or {1} not found in `{2}`.";
        public const string FileNotFound = "File `{0}` not found.";

        /// Does not exist
        public const string XDoesNotExist = "{0} does not exist.";
        public const string XYDoesNotExist = "{0} `{1}` does not exist.";

        /// Duplicate
        public const string MoreThanOneXFound = "More than one {0} found.";
        public const string XAlreadyContainsY = "{0} already contains {1}.";
        public const string XAlreadyContainsYZ = "{0} already contains {1} `{2}`.";
        public const string XAlreadyHasY = "{0} already has {1}.";
        public const string XAlreadyHasYZ = "{0} already has {1} `{2}`.";

        /// Other
        public const string XWasNotExpected = "{0} was not expected.";
        public const string XNotProvided = "{0} not provided.";
        public const string SpecifyAtLeastOne = "Please specify at least one {0}.";
        public const string CouldNotConnectWithTheServer = "Could not connect with the server.";

        // Command Line Parsing
        public const string RequiredArgumentIsInvalid = "Required argument {0} is invalid.";
        public const string OptionIsInvalid = "Option {0} is invalid.";
        public const string ArgumentIsInvalid = "Argument {0} is invalid.";
        public const string RequiredArgumentNotPassed = "Required argument {0} was not provided.";

        // dotnet <verb>
        /// Project
        public const string CouldNotFindProjectIn = "Could not find any project in `{0}`.";
        public const string CouldNotFindProjectOrDirectory = "Could not find project or directory `{0}`.";
        public const string FoundMoreThanOneProjectIn = "Found more than one project in `{0}`. Please specify which one to use.";
        public const string FoundInvalidProject = "Found a project `{0}` but it is invalid.";
        public const string InvalidProject = "Invalid project `{0}`.";

        /// Solution
        public const string CouldNotFindSolutionIn = "Specified solution file {0} does not exist, or there is no solution file in the directory.";
        public const string CouldNotFindSolutionOrDirectory = "Could not find solution or directory `{0}`.";
        public const string FoundMoreThanOneSolutionIn = "Found more than one solution file in {0}. Please specify which one to use.";
        public const string FoundInvalidSolution = "The solution file {0} seems to be invalid. Please check if it is a valid solution file.";
        public const string InvalidSolution = "Invalid solution `{0}`.";
        public const string SolutionDoesNotExist = "Specified solution file {0} does not exist, or there is no solution file in the directory.";
        public const string SolutionAlreadyContainsAProject = "Solution {0} already contains project {1}.";

        /// add p2p
        public const string ReferenceDoesNotExist = "Reference {0} does not exist. If you still want to add it, please use --force option. Please note that this may have adverse effects on the project. ";
        public const string ReferenceIsInvalid = "Reference `{0}` is invalid.";
        public const string SpecifyAtLeastOneReferenceToAdd = "You must specify at least one reference to add. Please run dotnet add --help for more information.";
        public const string ReferenceAddedToTheProject = "Reference `{0}` added to the project.";
        public const string ProjectAlreadyHasAReference = "Project {0} already has a reference `{1}`.";

        /// add package
        public const string PackageReferenceDoesNotExist = "Package reference `{0}` does not exist.";
        public const string PackageReferenceIsInvalid = "Package reference `{0}` is invalid.";
        public const string SpecifyAtLeastOnePackageReferenceToAdd = "You must specify at least one reference to add. Please run dotnet add --help for more information.";
        public const string PackageReferenceAddedToTheProject = "Package reference `{0}` added to the project.";
        public const string ProjectAlreadyHasAPackageReference = "Project {0} already has a reference `{1}`.";
        public const string PleaseSpecifyVersion = "Please specify a version of the package.";

        /// add sln
        public const string ProjectDoesNotExist = "Project `{0}` does not exist.";
        public const string ProjectIsInvalid = "Project `{0}` is invalid.";
        public const string SpecifyAtLeastOneProjectToAdd = "You must specify at least one project to add. Please run dotnet add --help for more information.";
        public const string ProjectAddedToTheSolution = "Project `{0}` added to the solution.";
        public const string SolutionAlreadyHasAProject = "Solution {0} already contains project {1}.";

        /// del p2p
        public const string ReferenceNotFoundInTheProject = "Specified reference {0} does not exist in project {1}.";
        public const string ReferenceRemoved = "Reference `{0}` deleted from the project.";
        public const string SpecifyAtLeastOneReferenceToRemove = "You must specify at least one reference to delete. Please run dotnet delete --help for more information.";
        public const string ReferenceDeleted = "Reference `{0}` deleted.";
        public const string SpecifyAtLeastOneReferenceToDelete = "You must specify at least one reference to delete. Please run dotnet delete --help for more information.";

        /// del pkg
        public const string PackageReferenceNotFoundInTheProject = "Package reference `{0}` could not be found in the project.";
        public const string PackageReferenceRemoved = "Reference `{0}` deleted from the project.";
        public const string SpecifyAtLeastOnePackageReferenceToRemove = "You must specify at least one reference to delete. Please run dotnet delete --help for more information.";
        public const string PackageReferenceDeleted = "Package reference `{0}` deleted.";
        public const string SpecifyAtLeastOnePackageReferenceToDelete = "You must specify at least one package reference to delete.";

        /// del sln
        public const string ProjectNotFoundInTheSolution = "Project `{0}` could not be found in the solution.";
        public const string ProjectRemoved = "Project `{0}` removed from solution.";
        public const string SpecifyAtLeastOneProjectToRemove = "You must specify at least one project to remove.";
        public const string ProjectDeleted = "Project `{0}` deleted from solution.";
        public const string SpecifyAtLeastOneProjectToDelete = "You must specify at least one project to delete from solution.";

        /// list
        public const string NoReferencesFound = "There are no {0} references in project {1}. ;; {0} is the type of the item being requested (project, package, p2p) and {1} is the object operated on (a project file or a solution file). ";
        public const string NoProjectsFound = "No projects found in the solution.";

        /// update pkg
        public const string PleaseSpecifyNewVersion = "Please specify new version of the package.";
        public const string PleaseSpecifyWhichPackageToUpdate = "Please specify which package to update.";
        public const string NothingToUpdate = "Nothing to update.";
        public const string EverythingUpToDate = "Everything is already up-to-date.";
        public const string PackageVersionUpdatedTo = "Version of package `{0}` updated to `{1}`.";
        public const string PackageVersionUpdated = "Version of package `{0}` updated.";
        public const string CouldNotUpdateTheVersion = "Could not update the version of the package `{0}`.";

        /// new
        public const string TemplateCreatedSuccessfully = "The template {0} created successfully. Please run \"dotnet restore\" to get started!";
        public const string TemplateInstalledSuccesfully = "The template {0} installed successfully. You can use \"dotnet new {0}\" to get started with the new template.";
        public const string TemplateCreateError = "Template {0} could not be created. Error returned was: {1}.";
        public const string TemplateInstallError = "Template {0} could not be installed. Error returned was: {1}.";
        public const string SpecifiedNameExists = "Specified name {0} already exists. Please specify a different name.";
        public const string SpecifiedAliasExists = "Specified alias {0} already exists. Please specify a different alias.";
        public const string MandatoryParameterMissing = "Mandatory parameter {0} missing for template {1}. ";
    }
}
