// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine;
using Microsoft.DotNet.Tools;
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

        public static Command GetCommand()
        {
            var command = new Command("package", LocalizableStrings.AppFullName);

            command.AddArgument(CmdPackageArgument);
            command.AddOption(InteractiveOption);

            return command;
        }
    }
}
