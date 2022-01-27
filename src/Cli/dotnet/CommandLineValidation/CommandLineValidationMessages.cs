// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli.CommandLineValidation;

namespace Microsoft.DotNet.Cli
{
    internal sealed class CommandLineValidationMessages : LocalizationResources
    {
        public override string ExpectsOneArgument(SymbolResult symbolResult) =>
            symbolResult is CommandResult
                ? string.Format(LocalizableStrings.CommandAcceptsOnlyOneArgument, symbolResult.Token().Value, symbolResult.Tokens.Count)
                : string.Format(LocalizableStrings.OptionAcceptsOnlyOneArgument, symbolResult.Token().Value, symbolResult.Tokens.Count);

        public override string NoArgumentProvided(SymbolResult symbolResult) =>
            symbolResult is CommandResult
                ? string.Format(LocalizableStrings.RequiredArgumentMissingForCommand, symbolResult.Token().Value)
                : string.Format(LocalizableStrings.RequiredArgumentMissingForOption, symbolResult.Token().Value);

        public override string FileDoesNotExist(string filePath) =>
            string.Format(LocalizableStrings.FileDoesNotExist, filePath);

        public override string RequiredCommandWasNotProvided() =>
            string.Format(LocalizableStrings.RequiredCommandWasNotProvided);

        public override string UnrecognizedArgument(string unrecognizedArg, IReadOnlyCollection<string> allowedValues) =>
            string.Format(LocalizableStrings.UnrecognizedArgument, unrecognizedArg, string.Join("\n\t", allowedValues.Select(v => $"'{v}'")));

        public override string UnrecognizedCommandOrArgument(string arg) =>
            string.Format(LocalizableStrings.UnrecognizedCommandOrArgument, arg);

        public override string ExpectsFewerArguments(
            Token token,
            int providedNumberOfValues,
            int maximumNumberOfValues) =>
            token.Type == TokenType.Command
                ? string.Format(LocalizableStrings.CommandExpectsFewerArguments, token, maximumNumberOfValues, providedNumberOfValues)
                : string.Format(LocalizableStrings.OptionExpectsFewerArguments, token, maximumNumberOfValues, providedNumberOfValues);

        public override string DirectoryDoesNotExist(string path) =>
            string.Format(LocalizableStrings.DirectoryDoesNotExist, path);

        public override string FileOrDirectoryDoesNotExist(string path) =>
            string.Format(LocalizableStrings.FileOrDirectoryDoesNotExist, path);

        public override string InvalidCharactersInPath(char invalidChar) =>
            string.Format(LocalizableStrings.CharacterNotAllowedInPath, invalidChar);

        public override string RequiredArgumentMissing(SymbolResult symbolResult) =>
            symbolResult is CommandResult
                ? string.Format(LocalizableStrings.RequiredCommandArgumentMissing, symbolResult.Token().Value)
                : string.Format(LocalizableStrings.RequiredOptionArgumentMissing, symbolResult.Token().Value);

        public override string ResponseFileNotFound(string filePath) =>
            string.Format(LocalizableStrings.ResponseFileNotFound, filePath);

        public override string ErrorReadingResponseFile(string filePath, IOException e) =>
            string.Format(LocalizableStrings.ErrorReadingResponseFile, filePath, e.Message);

        public override string HelpOptionDescription() => LocalizableStrings.ShowHelpInfo;
    }

    internal static class SymbolResultExtensions
    {
        internal static Token Token(this SymbolResult symbolResult)
        {
            return symbolResult switch
            {
                CommandResult commandResult => commandResult.Token,
                OptionResult optionResult => optionResult.Token == default ?
                                             new Token($"--{optionResult.Option.Name}", TokenType.Option, optionResult.Option)
                                             : optionResult.Token,
                ArgumentResult argResult => new Token(argResult.GetValueOrDefault<string>(), TokenType.Argument, argResult.Argument),
                _ => default
            };
        }
    }
}
