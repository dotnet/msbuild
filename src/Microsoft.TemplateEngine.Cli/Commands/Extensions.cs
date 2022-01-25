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
    }
}
