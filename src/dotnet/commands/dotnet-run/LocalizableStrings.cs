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

        public const string RunCommandException = "The build failed. Please fix the build errors and run again.";

        public const string RunCommandExceptionUnableToRunSpecifyFramework = "Unable to run your project\nYour project targets multiple frameworks. Please specify which framework to run using '{0}'.";

        public const string RunCommandExceptionUnableToRun = "Unable to run your project\nPlease ensure you have a runnable project type and ensure '{0}' supports this project.\nThe current {1} is '{2}'";

        public const string RunCommandExceptionNoProjects = "Couldn't find a project to run. Ensure a project exists in {0}, or pass the path to the project using {1}.";

        public const string RunCommandExceptionMultipleProjects = "Specify which project file to use because {0} contains more than one project file.";

        public const string RunCommandAdditionalArgsHelpText = "Arguments passed to the application that is being run.";
    }
}
