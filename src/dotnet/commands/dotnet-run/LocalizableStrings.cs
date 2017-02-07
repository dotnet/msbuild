namespace Microsoft.DotNet.Tools.Run
{
    internal class LocalizableStrings
    {
        public const string AppFullName = ".NET Run Command";

        public const string AppDescription = "Command used to run .NET apps";

        public const string CommandOptionConfigurationDescription = "Configuration to use for building the project. Default for most projects is \"Debug\".";

        public const string CommandOptionFramework = "FRAMEWORK";

        public const string CommandOptionFrameworkDescription = "Build and run the app using the specified framework. The framework has to be specified in the project file. ";

        public const string CommandOptionProjectDescription = "The path to the project file to run (defaults to the current directory if there is only one project).";

        public const string RunCommandException = "The build failed. Please fix the build errors and run again.";

        public const string RunCommandExceptionUnableToRun = "Unable to run your project\nPlease ensure you have a runnable project type and ensure '{0}' supports this project.\nThe current {1} is '{2}'";

        public const string RunCommandAdditionalArgsHelpText = "Arguments passed to the application that is being run.";
    }
}