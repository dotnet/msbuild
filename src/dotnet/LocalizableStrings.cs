using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.DotNet.Tools
{
    internal static class LocalizableStrings
    {
        // Arguments parsing
        public const string RequiredArgumentNotPassed = "Required argument {0} was not passed.";
        public const string RequiredArgumentIsInvalid = "Required argument {0} is invalid.";

        // Project
        public const string CouldNotFindProjectOrDirectory = "Could not find project or directory `{0}`.";
        public const string CouldNotFindAnyProjectInDirectory = "Could not find any project in `{0}`.";
        public const string MoreThanOneProjectInDirectory = "Found more than one project in `{0}`. Please specify which one to use.";
        public const string FoundInvalidProject = "Found a project `{0}` but it is invalid.";
        public const string ProjectIsInvalid = "Invalid project `{0}`.";
        public const string ProjectDoesNotExist = "Project `{0}` does not exist.";

        // Project Reference
        public const string ProjectAlreadyHasAreference = "Project already has a reference to `{0}`.";
        public const string ReferenceAddedToTheProject = "Reference `{0}` added to the project.";
        public const string ReferenceDoesNotExist = "Reference `{0}` does not exist.";
        public const string SpecifyAtLeastOneReference = "You must specify at least one reference to add.";
    }
}
