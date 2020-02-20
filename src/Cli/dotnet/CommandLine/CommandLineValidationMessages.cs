// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;

namespace Microsoft.DotNet.Cli.CommandLine
{
    internal sealed class CommandLineValidationMessages : IValidationMessages
    {
        public string CommandAcceptsOnlyOneArgument(string command, int argumentCount) =>
            string.Format(LocalizableStrings.CommandAcceptsOnlyOneArgument, command, argumentCount);

        public string CommandAcceptsOnlyOneSubcommand(string command, string subcommandsSpecified) =>
            string.Format(LocalizableStrings.CommandAcceptsOnlyOneSubcommand, command, subcommandsSpecified);

        public string FileDoesNotExist(string filePath) =>
            string.Format(LocalizableStrings.FileDoesNotExist, filePath);

        public string NoArgumentsAllowed(string option) =>
            string.Format(LocalizableStrings.NoArgumentsAllowed, option);

        public string OptionAcceptsOnlyOneArgument(string option, int argumentCount) =>
            string.Format(LocalizableStrings.OptionAcceptsOnlyOneArgument, option, argumentCount);

        public string RequiredArgumentMissingForCommand(string command) =>
            string.Format(LocalizableStrings.RequiredArgumentMissingForCommand, command);

        public string RequiredArgumentMissingForOption(string option) =>
            string.Format(LocalizableStrings.RequiredArgumentMissingForOption, option);

        public string RequiredCommandWasNotProvided() =>
            string.Format(LocalizableStrings.RequiredCommandWasNotProvided);

        public string UnrecognizedArgument(string unrecognizedArg, string[] allowedValues) =>
            string.Format(LocalizableStrings.UnrecognizedArgument, unrecognizedArg, $"\n\t{string.Join("\n\t", allowedValues.Select(v => $"'{v}'"))}");

        public string UnrecognizedCommandOrArgument(string arg) =>
            string.Format(LocalizableStrings.UnrecognizedCommandOrArgument, arg);

        public string UnrecognizedOption(string unrecognizedOption, string[] allowedValues) =>
            string.Format(LocalizableStrings.UnrecognizedOption, unrecognizedOption, $"\n\t{string.Join("\n\t", allowedValues.Select(v => $"'{v}'"))}");
    }
}
