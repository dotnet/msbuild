// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Remove.PackageReference;
using LocalizableStrings = Microsoft.DotNet.Tools.Remove.PackageReference.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class RemovePackageParser
    {
        public static readonly Argument<IEnumerable<string>> CmdPackageArgument = new Argument<IEnumerable<string>>(Tools.Add.PackageReference.LocalizableStrings.CmdPackage)
        {
            Description = LocalizableStrings.AppHelpText,
            Arity = ArgumentArity.OneOrMore,
        };

        public static readonly Option<bool> InteractiveOption = new ForwardedOption<bool>("--interactive", CommonLocalizableStrings.CommandInteractiveOptionDescription)
            .ForwardAs("--interactive");

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            var command = new Command("package", LocalizableStrings.AppFullName);

            command.AddArgument(CmdPackageArgument);
            command.AddOption(InteractiveOption);

            command.SetHandler((parseResult) => new RemovePackageReferenceCommand(parseResult).Execute());

            return command;
        }
    }
}
