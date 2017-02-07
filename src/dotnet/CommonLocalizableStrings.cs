// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Tools
{
    internal class CommonLocalizableStrings
    {
        public const string ProjectAlreadyHasAreference = "Project already has a reference to `{0}`.";
        public const string ProjectReferenceCouldNotBeFound = "Project reference `{0}` could not be found.";
        public const string ProjectReferenceRemoved = "Project reference `{0}` removed.";

        // Project related
        public const string ProjectReferenceOneOrMore = "Project reference(s)";
        public const string P2P = "Project to Project";
        public const string ReferenceAddedToTheProject = "Reference `{0}` added to the project.";

        // Command Line Parsing
        public const string RequiredArgumentNotPassed = "Required argument {0} was not provided.";
        public const string RequiredCommandNotPassed = "Required command was not provided.";

        // dotnet <verb>
        /// Project
        public const string CouldNotFindAnyProjectInDirectory = "Could not find any project in `{0}`.";

        public const string CouldNotFindProjectOrDirectory = "Could not find project or directory `{0}`.";
        public const string MoreThanOneProjectInDirectory = "Found more than one project in `{0}`. Please specify which one to use.";
        public const string FoundInvalidProject = "Found a project `{0}` but it is invalid.";
        private const string InvalidProject = "Invalid project `{0}`.";

        /// Solution
        public const string CouldNotFindSolutionIn = "Specified solution file {0} does not exist, or there is no solution file in the directory.";

        public const string CouldNotFindSolutionOrDirectory = "Could not find solution or directory `{0}`.";
        public const string MoreThanOneSolutionInDirectory = "Found more than one solution file in {0}. Please specify which one to use.";
        public const string InvalidSolutionFormatString = "Invalid solution `{0}`. {1}"; // {0} is the solution path, {1} is already localized details on the failure
        
        /// add p2p
        public const string ReferenceDoesNotExist = "Reference {0} does not exist.";
        public const string SpecifyAtLeastOneReferenceToAdd = "You must specify at least one reference to add.";
       
        /// add sln
        public const string ProjectDoesNotExist = "Project `{0}` does not exist.";

        public const string ProjectIsInvalid = "Project `{0}` is invalid.";
        public const string SpecifyAtLeastOneProjectToAdd = "You must specify at least one project to add.";
        public const string ProjectAddedToTheSolution = "Project `{0}` added to the solution.";
        public const string SolutionAlreadyContainsProject = "Solution {0} already contains project {1}.";

        public const string SpecifyAtLeastOneReferenceToRemove = "You must specify at least one reference to remove.";
        
        /// del sln
        public const string SpecifyAtLeastOneProjectToRemove = "You must specify at least one project to remove.";

        /// list
        public const string NoReferencesFound = "There are no {0} references in project {1}. ;; {0} is the type of the item being requested (project, package, p2p) and {1} is the object operated on (a project file or a solution file). ";

        public const string NoProjectsFound = "No projects found in the solution.";

        /// sln
        public const string ArgumentsProjectDescription = "The project file to operate on. If a file is not specified, the command will search the current directory for one.";

        public const string ArgumentsSolutionDescription = "Solution file to operate on. If not specified, the command will search the current directory for one.";

        /// commands
        public const string CmdFramework = "FRAMEWORK";
        
        public const string ProjectNotCompatibleWithFrameworks = "Project `{0}` cannot be added due to incompatible targeted frameworks between the two projects. Please review the project you are trying to add and verify that is compatible with the following targets:";
        public const string ProjectDoesNotTargetFramework = "Project `{0}` does not target framework `{1}`.";
        public const string ProjectCouldNotBeEvaluated = "Project `{0}` could not be evaluated. Evaluation failed with following error:\n{1}";
    }
}
