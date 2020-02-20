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
        // This type is used to set the protected ArgumentsRule property.
        // This enables subcommands to change the argument's name and description as needed.
        internal class ListCommand : Command
        {
            public ListCommand()
                : base(
                    name: "list",
                    help: LocalizableStrings.NetListCommand,
                    options: new Option[]
                    {
                        CommonOptions.HelpOption(),
                        ListPackageReferencesCommandParser.ListPackageReferences(),
                        ListProjectToProjectReferencesCommandParser.ListProjectToProjectReferences(),
                    },
                    arguments: Accept.ZeroOrOneArgument()
                        .With(
                            name: CommonLocalizableStrings.SolutionOrProjectArgumentName,
                            description: CommonLocalizableStrings.SolutionOrProjectArgumentDescription)
                        .DefaultToCurrentDirectory())
            {
            }

            public void SetArgumentsRule(ArgumentsRule rule)
            {
                ArgumentsRule = rule;
            }
        }

        public static Command List() => new ListCommand();
    }
}
