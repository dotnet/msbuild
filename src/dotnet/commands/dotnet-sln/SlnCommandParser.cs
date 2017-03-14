// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.CommandLine;

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
                      .With(name: "SLN_FILE"),
                CommonOptions.HelpOption(),
                Create.Command("add",
                               ".NET Add project(s) to a solution file Command",
                               Accept.OneOrMoreArguments(),
                               CommonOptions.HelpOption()),
                Create.Command("list",
                               "List all projects in the solution.",
                               CommonOptions.HelpOption()),
                Create.Command("remove",
                               "Remove the specified project(s) from the solution. The project is not impacted.",
                               Accept.OneOrMoreArguments(),
                               CommonOptions.HelpOption()));
    }
}