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
                LocalizableStrings.AppFullName,
                Accept.ExactlyOneArgument()
                      .DefaultToCurrentDirectory()
                      .With(name: LocalizableStrings.SolutionArgumentName,
                            description: LocalizableStrings.SolutionArgumentDescription),
                CommonOptions.HelpOption(),
                SlnAddParser.SlnAdd(),
                SlnListParser.SlnList(),
                SlnRemoveParser.SlnRemove());
    }
}