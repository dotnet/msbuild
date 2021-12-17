// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.Commands;

namespace Microsoft.TemplateEngine.Cli
{
    /// <summary>
    /// The class combines information from<see cref="ITemplateParameter"/> and <see cref="HostSpecificTemplateData"/> for choice parameter.
    /// Other parameters are implemented in base class <see cref="CliTemplateParameter"/>.
    /// </summary>
    internal class ChoiceTemplateParameter : CliTemplateParameter
    {
        private Dictionary<string, ParameterChoice> _choices = new Dictionary<string, ParameterChoice>(StringComparer.OrdinalIgnoreCase);

        internal ChoiceTemplateParameter(ITemplateParameter parameter, HostSpecificTemplateData data) : base(parameter, data)
        {
            if (!parameter.DataType.Equals("choice", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"{nameof(parameter)} should have {nameof(parameter.Type)} {nameof(ParameterType.Choice)}");
            }
            if (parameter.Choices == null)
            {
                throw new ArgumentException($"{nameof(parameter)} should have {nameof(parameter.Choices)}");
            }
            _choices = parameter.Choices.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
        }

        internal ChoiceTemplateParameter(string name, IEnumerable<string>? shortNameOverrides = null, IEnumerable<string>? longNameOverrides = null)
            : base(name, ParameterType.Choice, shortNameOverrides, longNameOverrides)
        {
            _choices = new Dictionary<string, ParameterChoice>(StringComparer.OrdinalIgnoreCase);
        }

        internal ChoiceTemplateParameter(ChoiceTemplateParameter choiceTemplateParameter)
           : base(choiceTemplateParameter)
        {
            _choices = choiceTemplateParameter.Choices.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
        }

        internal override ParameterType Type => ParameterType.Choice;

        internal virtual IReadOnlyDictionary<string, ParameterChoice> Choices => _choices;

        internal override Func<HelpContext, string?>? GetCustomFirstColumnText(TemplateOption o)
        {
            return (context) =>
            {
                string standardUsage = HelpBuilder.Default.GetIdentifierSymbolUsageLabel(o.Option, context);
                if (standardUsage.Length > context.HelpBuilder.MaxWidth / 3)
                {
                    if (Choices.Count > 2)
                    {
                        o.Option.ArgumentHelpName = $"{string.Join("|", Choices.Keys.Take(2))}|...";
                        string updatedFirstColumn = HelpBuilder.Default.GetIdentifierSymbolUsageLabel(o.Option, context);
                        if (updatedFirstColumn.Length <= context.HelpBuilder.MaxWidth / 3)
                        {
                            return updatedFirstColumn;
                        }
                    }
                    o.Option.ArgumentHelpName = HelpStrings.Text_ChoiceArgumentHelpName;
                    return HelpBuilder.Default.GetIdentifierSymbolUsageLabel(o.Option, context);
                }
                return standardUsage;
            };
        }

        protected override Option GetBaseOption(IReadOnlyList<string> aliases)
        {
            Option option = new Option<string>(
                aliases.ToArray(),
                parseArgument: result => GetParseChoiceArgument(this)(result))
            {
                Arity = new ArgumentArity(DefaultIfOptionWithoutValue == null ? 1 : 0, 1)
            };

            option.FromAmong(Choices.Keys.ToArray());
            return option;
        }

        private static ParseArgument<string> GetParseChoiceArgument(ChoiceTemplateParameter parameter)
        {
            return (argumentResult) =>
            {
                if (argumentResult.Parent is not OptionResult or)
                {
                    throw new NotSupportedException("The method should be only used with option.");
                }

                if (argumentResult.Tokens.Count == 0)
                {
                    if (or.IsImplicit)
                    {
                        if (string.IsNullOrWhiteSpace(parameter.DefaultValue))
                        {
                            if (TryConvertValueToChoice(parameter.DefaultValue, parameter, out string defaultValue, out string error))
                            {
                                return defaultValue;
                            }
                            argumentResult.ErrorMessage = $"Cannot parse default value '{parameter.DefaultValue}' for option '{or.Token?.Value}' as expected type 'choice': {error}.";
                            return string.Empty;
                        }
                        argumentResult.ErrorMessage = $"Default value for argument missing for option: {or.Token?.Value}.";
                        return string.Empty;
                    }
                    if (parameter.DefaultIfOptionWithoutValue != null)
                    {
                        if (TryConvertValueToChoice(parameter.DefaultIfOptionWithoutValue, parameter, out string defaultValue, out string error))
                        {
                            return defaultValue;
                        }
                        argumentResult.ErrorMessage = $"Cannot parse default if option without value '{parameter.DefaultIfOptionWithoutValue}' for option '{or.Token?.Value}' as expected type 'choice': {error}.";
                        return string.Empty;
                    }
                    argumentResult.ErrorMessage = $"Required argument missing for option: {or.Token?.Value}.";
                    return string.Empty;
                }
                else if (argumentResult.Tokens.Count == 1)
                {
                    if (TryConvertValueToChoice(argumentResult.Tokens[0].Value, parameter, out string value, out string error))
                    {
                        return value;
                    }
                    argumentResult.ErrorMessage = $"Cannot parse argument '{argumentResult.Tokens[0].Value}' for option '{or.Token?.Value}' as expected type 'choice': {error}.";
                    return string.Empty;
                }
                else
                {
                    argumentResult.ErrorMessage = $"Using more than 1 argument is not allowed for '{or.Token?.Value}', used: {argumentResult.Tokens.Count}.";
                    return string.Empty;
                }
            };
        }

        private static bool TryConvertValueToChoice(string? value, ChoiceTemplateParameter parameter, out string parsedValue, out string error)
        {
            parsedValue = string.Empty;
            if (value == null)
            {
                error = "value is <null>";
                return false;
            }

            if (parameter.Choices == null)
            {
                error = "no choices are defined for parameter";
                return false;
            }

            foreach (string choiceValue in parameter.Choices.Keys)
            {
                if (string.Equals(choiceValue, value, StringComparison.OrdinalIgnoreCase))
                {
                    parsedValue = choiceValue;
                    error = string.Empty;
                    return true;
                }
            }
            error = $"value '{value}' is not allowed, allowed values are: {string.Join(",", parameter.Choices.Keys.Select(key => $"'{key}'"))}";
            return false;
        }
    }
}
