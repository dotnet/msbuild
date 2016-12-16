namespace Microsoft.DotNet.Tools.Run
{
    internal class LocalizableStrings
    {
        public const string AppFullName = ".NET Run Command";

        public const string AppDescription = "Command used to run .NET apps";

        public const string CommandOptionConfigurationDescription = "Configuration to use for building the project. Default is \"Debug\".";

        public const string CommandOptionFramework = "FRAMEWORK";

        public const string CommandOptionFrameworkDescription = "Build and run the specified framework. The framework has to be specified in the project file. ";

        public const string CommandOptionProjectDescription = "The path to the project file to run (defaults to the current directory if there is only one project).";

        public const string RunCommandException = "The build failed. Please fix the build errors and run again.";

        public const string RunCommandMSBuildExtensionsPath = "MSBuildExtensionsPath";

        public const string RunCommandConfiguration = "Configuration";

        public const string RunCommandTargetFramework = "TargetFramework";

        public const string RunCommandProjectInstance = "RunCommand";

        public const string RunCommandOutputType = "OutputType";

        public const string RunCommandExceptionUnableToRun1 = "Unable to run your project.";

        public const string RunCommandExceptionUnableToRun2 = "Please ensure you have a runnable project type and ensure 'dotnet run' supports this project.";

        public const string RunCommandExceptionUnableToRun3 = "The current OutputType is ";

        public const string RunCommandRunArguments = "RunArguments";

        public const string RunCommandRunWorkingDirectory = "RunWorkingDirectory";

        public const string RunCommandInvalidOperationException1 = "Couldn't find a project to run. Ensure a project exists in ";

        public const string RunCommandInvalidOperationException2 = "Or pass the path to the project using --project";

        public const string RunCommandInvalidOperationException3 = "Specify which project file to use because this ";

        public const string RunCommandInvalidOperationException4 = "contains more than one project file.";

        public const string RunCommandAdditionalArgsHelpText = "Arguments passed to the application that is being run.";
    }
}