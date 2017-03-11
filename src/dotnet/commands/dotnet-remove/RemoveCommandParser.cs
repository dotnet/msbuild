// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli
{
    internal static class RemoveCommandParser
    {
        public static Command Remove() =>
            Create.Command("remove",
                           ".NET Remove Command",
                           Accept.ZeroOrOneArgument()
                                 .With(name: "PROJECT")
                                 .DefaultToCurrentDirectory(),
                           CommonOptions.HelpOption(),
                           Create.Command("package",
                                          "Command to remove package reference.",
                                          CommonOptions.HelpOption()),
                           Create.Command("reference",
                                          "Command to remove project to project reference",
                                          Accept.AnyOneOf(Suggest.ProjectReferencesFromProjectFile),
                                          CommonOptions.HelpOption(),
                                          Create.Option("-f|--framework",
                                                        "Remove reference only when targetting a specific framework",
                                                        Accept.ExactlyOneArgument()
                                                              .With(name: "FRAMEWORK"))));
    }
}