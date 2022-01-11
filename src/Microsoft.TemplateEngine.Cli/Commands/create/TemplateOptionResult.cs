// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

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
            OptionResult? optionResult = parseResult.FindResultFor(option.Option);

            if (optionResult == null)
            {
                //option is not specified
                return null;
            }

            return new TemplateOptionResult(
                    option,
                    optionResult.Token.Value ?? string.Empty,
                    optionResult.GetValueOrDefault<string>());
        }
    }
}
