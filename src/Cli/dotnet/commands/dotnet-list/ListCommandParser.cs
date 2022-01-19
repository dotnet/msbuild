// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using Microsoft.DotNet.Tools;
using LocalizableStrings = Microsoft.DotNet.Tools.List.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ListCommandParser
    {
        public static readonly Argument<string> SlnOrProjectArgument = new Argument<string>(CommonLocalizableStrings.SolutionOrProjectArgumentName)
        {
            Description = CommonLocalizableStrings.SolutionOrProjectArgumentDescription,
            Arity = ArgumentArity.ZeroOrOne
        }.DefaultToCurrentDirectory();

        public static Command GetCommand()
        {
            var command = new Command("list", LocalizableStrings.NetListCommand);

            command.AddArgument(SlnOrProjectArgument);
            command.AddCommand(ListPackageReferencesCommandParser.GetCommand());
            command.AddCommand(ListProjectToProjectReferencesCommandParser.GetCommand());

            return command;
        }
    }
}
