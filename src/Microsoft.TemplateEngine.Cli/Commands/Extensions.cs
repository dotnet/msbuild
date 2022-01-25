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

        internal static string GetDisplayArgumentName(this Argument argument)
        {
            return argument.Arity.MinimumNumberOfValues > 0
                ? $"<{argument.Name}>"
                : $"[{argument.Name}]";
        }

        /// <summary>
        /// Gets root <see cref="NewCommand"/> from <paramref name="parseResult"/>.
        /// </summary>
        internal static NewCommand GetNewCommandFromParseResult(this ParseResult parseResult)
        {
            var commandResult = parseResult.CommandResult;

            while (commandResult?.Command != null && commandResult.Command is not NewCommand)
            {
                commandResult = (commandResult.Parent as CommandResult);
            }
            if (commandResult == null || commandResult.Command is not NewCommand newCommand)
            {
                throw new Exception($"Command structure is not correct: {nameof(NewCommand)} is not found as part of parse result.");
            }
            return newCommand;
        }

        /// <summary>
        /// Gets parent command list including topmost <see cref="NewCommand"/>.
        /// </summary>
        /// <returns> list of called commands before and including <see cref="NewCommand"/>.</returns>
        internal static IReadOnlyList<Command> GetParentCommandListFromParseResult(this ParseResult parseResult)
        {
            var commandResult = parseResult.CommandResult;

            while (commandResult?.Command != null && commandResult.Command is not NewCommand)
            {
                commandResult = (commandResult.Parent as CommandResult);
            }
            if (commandResult == null || commandResult.Command is not NewCommand newCommand)
            {
                throw new Exception($"Command structure is not correct: {nameof(NewCommand)} or {nameof(InstantiateCommand)} is not found.");
            }
            List<Command> parentCommands = new List<Command>();
            while (commandResult?.Command != null)
            {
                parentCommands.Add(commandResult.Command);
                commandResult = (commandResult.Parent as CommandResult);
            }
            parentCommands.Reverse();
            return parentCommands;
        }
    }
}
