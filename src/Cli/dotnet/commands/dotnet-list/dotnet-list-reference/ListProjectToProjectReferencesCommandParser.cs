// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.CommandLine;
using LocalizableStrings = Microsoft.DotNet.Tools.List.ProjectToProjectReferences.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ListProjectToProjectReferencesCommandParser
    {
        public static Command ListProjectToProjectReferences()
        {
            return Create.Command(
                "reference",
                LocalizableStrings.AppFullName,
                Accept.ZeroOrOneArgument(),
                CommonOptions.HelpOption());
        }
    }
}
