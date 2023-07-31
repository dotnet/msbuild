// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    /// <summary>
    /// The class represents the information about the template option used when executing the command.
    /// </summary>
    internal class TemplateOptionResult
    {
        internal TemplateOptionResult(
          TemplateOption? templateOption,
          string inputFormat,
          string? specifiedValue)
        {
            TemplateOption = templateOption;
            InputFormat = inputFormat;
            SpecifiedValue = specifiedValue;
        }

        /// <summary>
        /// the alias used in CLI for parameter.
        /// </summary>
        internal string InputFormat { get; }

        /// <summary>
        /// The value specified for the parameter in CLI.
        /// </summary>
        internal string? SpecifiedValue { get; }

        internal TemplateOption? TemplateOption { get; }

        internal static TemplateOptionResult? FromParseResult(TemplateOption option, ParseResult parseResult)
        {
            OptionResult? optionResult = parseResult.GetResult(option.Option);

            if (optionResult == null)
            {
                //option is not specified
                return null;
            }

            return new TemplateOptionResult(
                    option,
                    optionResult.IdentifierToken?.Value ?? string.Empty,
                    optionResult.Errors.Any() ? null : optionResult.GetValueOrDefault<object>()?.ToString());
        }
    }
}
