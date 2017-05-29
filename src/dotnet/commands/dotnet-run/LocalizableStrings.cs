// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Tools.Run
{
    internal class LocalizableStrings
    {
        public const string AppFullName = ".NET Run Command";

        public const string AppDescription = "Command used to run .NET apps";

        public const string CommandOptionNoBuildDescription = "Skip building the project prior to running. By default, the project will be built.";

        public const string CommandOptionFrameworkDescription = "Build and run the app using the specified framework. The framework has to be specified in the project file. ";

        public const string CommandOptionNoBuild = "Do not build the project before running.";

        public const string CommandOptionProjectDescription = "The path to the project file to run (defaults to the current directory if there is only one project).";

        public const string CommandOptionLaunchProfileDescription = "The name of the launch profile (if any) to use when launching the application.";

        public const string CommandOptionNoLaunchProfileDescription = "Do not attempt to use launchSettings.json to configure the application.";

        public const string RunCommandException = "The build failed. Please fix the build errors and run again.";

        public const string RunCommandExceptionUnableToRunSpecifyFramework = "Unable to run your project\nYour project targets multiple frameworks. Please specify which framework to run using '{0}'.";

        public const string RunCommandExceptionUnableToRun = "Unable to run your project.\nPlease ensure you have a runnable project type and ensure '{0}' supports this project.\nA runnable project should target a runnable TFM (for instance, netcoreapp2.0) and have OutputType 'Exe'.\nThe current {1} is '{2}'.";

        public const string RunCommandExceptionNoProjects = "Couldn't find a project to run. Ensure a project exists in {0}, or pass the path to the project using {1}.";

        public const string RunCommandExceptionMultipleProjects = "Specify which project file to use because {0} contains more than one project file.";

        public const string RunCommandAdditionalArgsHelpText = "Arguments passed to the application that is being run.";

        public const string RunCommandExceptionCouldNotLocateALaunchSettingsFile = "The specified launch profile could not be located.";

        public const string RunCommandExceptionCouldNotApplyLaunchSettings = "The launch profile \"{0}\" could not be applied.\n{1}";

        public const string DefaultLaunchProfileDisplayName = "(Default)";

        public const string UsingLaunchSettingsFromMessage = "Using launch settings from {0}...";

        public const string LaunchProfileIsNotAJsonObject = "Launch profile is not a JSON object.";

        public const string LaunchProfileHandlerCannotBeLocated = "The launch profile type '{0}' is not supported.";

        public const string UsableLaunchProfileCannotBeLocated = "A usable launch profile could not be located.";

        public const string UnexpectedExceptionProcessingLaunchSettings = "An unexpected exception occurred while processing launch settings:\n{0}";

        public const string LaunchProfilesCollectionIsNotAJsonObject = "The 'profiles' property of the launch settings document is not a JSON object.";
    }
}
