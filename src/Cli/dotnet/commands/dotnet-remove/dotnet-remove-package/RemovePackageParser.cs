// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Remove.PackageReference;
using LocalizableStrings = Microsoft.DotNet.Tools.Remove.PackageReference.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class RemovePackageParser
    {
        public static readonly CliArgument<IEnumerable<string>> CmdPackageArgument = new(Tools.Add.PackageReference.LocalizableStrings.CmdPackage)
        {
            Description = LocalizableStrings.AppHelpText,
            Arity = ArgumentArity.OneOrMore,
        };

        public static readonly CliOption<bool> InteractiveOption = new ForwardedOption<bool>("--interactive")
        {
            Description = CommonLocalizableStrings.CommandInteractiveOptionDescription
        }.ForwardAs("--interactive");

        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            var command = new CliCommand("package", LocalizableStrings.AppFullName);

            command.Arguments.Add(CmdPackageArgument);
            command.Options.Add(InteractiveOption);

            command.SetAction((parseResult) => new RemovePackageReferenceCommand(parseResult).Execute());

            return command;
        }
    }
}
