// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Tools.List.ProjectToProjectReferences
{
    internal class LocalizableStrings
    {
        public const string AppFullName = ".NET Core Project-to-Project dependency viewer";

        public const string AppDescription = "Command to list project to project (p2p) references";

        public const string NoReferencesFound = "There are no {0} references in project {1}.\n{0} is the type of the item being requested (project, package, p2p) and {1} is the object operated on (a project file or a solution file). ";
    }
}
