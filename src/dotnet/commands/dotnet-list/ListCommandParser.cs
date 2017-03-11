// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli
{
    internal static class ListCommandParser
    {
        public static Command List() =>
            Create.Command("list",
                           ".NET List Command",
                           Accept.ZeroOrOneArgument()
                                 .With(name: "PROJECT",
                                       description:
                                       "The project file to operate on. If a file is not specified, the command will search the current directory for one.")
                                 .DefaultToCurrentDirectory(),
                           CommonOptions.HelpOption(),
                           Create.Command("reference", "Command to list project to project references",
                                          Accept.ExactlyOneArgument()
                                                .With(name: "PROJECT",
                                                      description:
                                                      "The project file to operate on. If a file is not specified, the command will search the current directory for one."),
                                          CommonOptions.HelpOption()));
    }
}