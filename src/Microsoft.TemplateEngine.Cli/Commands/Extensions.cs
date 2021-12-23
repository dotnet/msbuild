// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal static class Extensions
    {
        internal static string? GetValueForOptionOrNull(this ParseResult parseResult, Option option)
        {
            OptionResult? result = parseResult.FindResultFor(option);
            if (result == null)
            {
                return null;
            }
            return result.GetValueOrDefault()?.ToString();
        }

        /// <summary>
        /// Gets name of <see cref="NewCommand"/> from <paramref name="parseResult"/>.
        /// <paramref name="parseResult"/> might be result for subcommand, then method will traverse up until <see cref="NewCommand"/> is found.
        /// </summary>
        /// <param name="parseResult"></param>
        /// <returns></returns>
        internal static string GetNewCommandName(this ParseResult parseResult)
        {
            var commandResult = parseResult.CommandResult;

            while (commandResult?.Command != null && commandResult.Command is not NewCommand)
            {
                commandResult = (commandResult.Parent as CommandResult);
            }

            //if new command is not found, search for instantiate command
            //in instantiation workflow via new command (dotnet new template) NewCommand is replaced by InstantiateCommand
            if (string.IsNullOrWhiteSpace(commandResult?.Command?.Name))
            {
                commandResult = parseResult.CommandResult;
                while (commandResult?.Command != null && commandResult.Command is not InstantiateCommand)
                {
                    commandResult = (commandResult.Parent as CommandResult);
                }
            }
            return commandResult?.Command?.Name ?? string.Empty;
        }

        /// <summary>
        /// Checks if <paramref name="parseResult"/> contains an error for <paramref name="option"/>.
        /// </summary>
        internal static bool HasErrorFor(this ParseResult parseResult, Option option)
        {
            if (!parseResult.Errors.Any())
            {
                return false;
            }

            if (parseResult.Errors.Any(e => e.SymbolResult?.Symbol == option))
            {
                return true;
            }

            if (parseResult.Errors.Any(e => e.SymbolResult?.Parent?.Symbol == option))
            {
                return true;
            }

            return false;
        }
    }
}
