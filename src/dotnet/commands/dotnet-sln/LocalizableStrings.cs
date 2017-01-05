// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Tools.Sln
{
    internal class LocalizableStrings
    {
        public const string AppFullName = ".NET modify solution file(s) command";

        public const string AppDescription = "Command to add, remove and list projects from the solution (SLN) file.";

        public const string AppHelpText = "Projects to add to the solution.";

        public const string CmdSlnFile = "<SLN_FILE>";

        public const string CmdSlnFileText = "Solution file to operate on. If not specified, the command will search the current directory for one.";

        public const string AddSubcommandHelpText = "Add a specified project to the solution.";

        public const string RemoveSubcommandHelpText = "Remove the specified project from the solution. The project is not impacted.";
        
        public const string ListSubcommandHelpText = "List all projects in the solution.";

        public const string CreateSubcommandHelpText = "Create a solution file.";

        public const string MultipleSlnFilesError = "The current directory contains more than one solution file. Please specify the solution file to use.";

        public const string ProjectFileNotFoundError = "The specified project {0} was not found.";

        public const string SolutionFileNotFoundError = "The specified solution file {0} was not found.";

        public const string NoSolutionFileError = "The current directory does not contain a solution file. Please specify a solution file to use.";
    }
}
