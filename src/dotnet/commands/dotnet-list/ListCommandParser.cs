// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.List.ProjectToProjectReferences;
using LocalizableStrings = Microsoft.DotNet.Tools.List.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ListCommandParser
    {
        public static Command List() => Create.Command(
            "list",
            LocalizableStrings.NetListCommand,
            Accept.ZeroOrOneArgument()
                .With(
                    name: CommonLocalizableStrings.CmdProjectFile,
                    description: CommonLocalizableStrings.ArgumentsProjectDescription)
                .DefaultToCurrentDirectory(),
            CommonOptions.HelpOption(),
            ListProjectToProjectReferencesCommandParser.ListProjectToProjectReferences());
    }
}
