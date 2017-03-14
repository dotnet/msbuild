// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Tools;
using LocalizableStrings = Microsoft.DotNet.Tools.Sln.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class SlnCommandParser
    {
        public static Command Sln() =>
            Create.Command(
                "sln",
                ".NET modify solution file command",
                Accept.ExactlyOneArgument()
                      .DefaultToCurrentDirectory()
                      .With(name: "SLN_FILE",
                            description: CommonLocalizableStrings.ArgumentsSolutionDescription),
                CommonOptions.HelpOption(),
                Create.Command("add",
                               ".NET Add project(s) to a solution file Command",
                               Accept.OneOrMoreArguments()
                                     .With(name: "args",
                                           description: LocalizableStrings.AddSubcommandHelpText),
                               CommonOptions.HelpOption()),
                Create.Command("list",
                               "List all projects in the solution.",
                               CommonOptions.HelpOption()),
                Create.Command("remove",
                               "Remove the specified project(s) from the solution. The project is not impacted.",
                               Accept.OneOrMoreArguments()
                                     .With(name: "args",
                                           description: LocalizableStrings.RemoveSubcommandHelpText),
                               CommonOptions.HelpOption()));
    }
}