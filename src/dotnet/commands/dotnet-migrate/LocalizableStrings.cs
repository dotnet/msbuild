namespace Microsoft.DotNet.Tools.Migrate
{
    internal class LocalizableStrings
    {
        public const string AppFullName = ".NET Migrate Command";

        public const string AppDescription = "Command used to migrate project.json projects to msbuild";

        public const string CmdProjectArgument = "PROJECT_JSON/GLOBAL_JSON/SOLUTION_FILE/PROJECT_DIR";
        public const string CmdProjectArgumentDescription = 
@"The path to one of the following:
    - a project.json file to migrate.
    - a global.json file, it will migrate the folders specified in global.json.
    - a solution.sln file, it will migrate the projects referenced in the solution.
    - a directory to migrate, it will recursively search for project.json files to migrate.
Defaults to current directory if nothing is specified.";

        public const string CmdTemplateDescription = "Base MSBuild template to use for migrated app. The default is the project included in dotnet new.";

        public const string CmdVersionDescription = "The version of the sdk package that will be referenced in the migrated app. The default is the version of the sdk in dotnet new.";

        public const string CmdXprojFileDescription = "The path to the xproj file to use. Required when there is more than one xproj in a project directory.";

        public const string CmdSkipProjectReferencesDescription = "Skip migrating project references. By default project references are migrated recursively.";

        public const string CmdReportFileDescription = "Output migration report to the given file in addition to the console.";

        public const string CmdReportOutputDescription = "Output migration report file as json rather than user messages.";

        public const string CmdSkipBackupDescription = "Skip moving project.json, global.json, and *.xproj to a `backup` directory after successful migration.";

        public const string MigrationFailedError = "Migration failed.";

        public const string MigrationAdditionalHelp = "The project migration has finished. Please visit https://aka.ms/coremigration to report any issues you've encountered or ask for help.";

        public const string MigrationReportSummary = "Summary";

        public const string MigrationReportTotalProjects = "Total Projects: {0}";

        public const string MigrationReportSucceededProjects = "Succeeded Projects: {0}";

        public const string MigrationReportFailedProjects = "Failed Projects: {0}";

        public const string ProjectMigrationSucceeded = "Project {0} migration succeeded ({1}).";

        public const string ProjectMigrationFailed = "Project {0} migration failed ({1}).";

        public const string MigrationFailedToFindProjectInGlobalJson = "Unable to find any projects in global.json.";

        public const string MigrationUnableToFindProjects = "Unable to find any projects in {0}.";

        public const string MigrationProjectJsonNotFound = "No project.json file found in '{0}'.";

        public const string MigrationInvalidProjectArgument = "Invalid project argument - '{0}' is not a project.json, global.json, or solution.sln file and a directory named '{0}' doesn't exist.";

        public const string MigratonUnableToFindProjectJson = "Unable to find project.json file at {0}.";

        public const string MigrationUnableToFindGlobalJson = "Unable to find global settings file at {0}.";

        public const string MigrationUnableToFindSolutionFile = "Unable to find the solution file at {0}.";

        public const string MigrateFilesBackupLocation = "Files backed up to {0}";
    }
}
