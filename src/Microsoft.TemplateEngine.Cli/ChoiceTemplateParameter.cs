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
    /// The class combines information from <see cref="ITemplateParameter"/> and <see cref="HostSpecificTemplateData"/> for choice parameter.
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

        /// <summary>
        /// Customization for first column of help, required to shorten the value in case choice parameter has too many choices.
        /// Otherwise the first column of help is getting too wide.
        /// </summary>
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

        /// <summary>
        /// Custom parse method for template option.
        /// It is mainly required to process default if no option value cases.
        /// </summary>
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
                        if (!string.IsNullOrWhiteSpace(parameter.DefaultValue))
                        {
                            if (TryConvertValueToChoice(parameter.DefaultValue, parameter, out string defaultValue, out string error))
                            {
                                return defaultValue;
                            }
                            //Cannot parse default value '{0}' for option '{1}' as expected type '{2}': {3}.
                            argumentResult.ErrorMessage = string.Format(
                                LocalizableStrings.ParseChoiceTemplateOption_Error_InvalidDefaultValue,
                                parameter.DefaultValue,
                                or.Token.Value,
                                "choice",
                                error);
                            return string.Empty;
                        }
                        //Default value for argument missing for option: '{0}'.
                        argumentResult.ErrorMessage = string.Format(LocalizableStrings.ParseTemplateOption_Error_MissingDefaultValue, or.Token.Value);
                        return string.Empty;
                    }
                    if (parameter.DefaultIfOptionWithoutValue != null)
                    {
                        if (TryConvertValueToChoice(parameter.DefaultIfOptionWithoutValue, parameter, out string defaultValue, out string error))
                        {
                            return defaultValue;
                        }
                        //Cannot parse default if option without value '{0}' for option '{1}' as expected type '{2}': {3}.
                        argumentResult.ErrorMessage = string.Format(
                            LocalizableStrings.ParseChoiceTemplateOption_Error_InvalidDefaultIfNoOptionValue,
                            parameter.DefaultIfOptionWithoutValue,
                            or.Token.Value,
                            "choice",
                            error);
                        return string.Empty;
                    }
                    //Required argument missing for option: '{0}'.
                    argumentResult.ErrorMessage = string.Format(LocalizableStrings.ParseTemplateOption_Error_MissingDefaultIfNoOptionValue, or.Token.Value);
                    return string.Empty;
                }
                else if (argumentResult.Tokens.Count == 1)
                {
                    if (TryConvertValueToChoice(argumentResult.Tokens[0].Value, parameter, out string value, out string error))
                    {
                        return value;
                    }
                    //Cannot parse argument '{0}' for option '{1}' as expected type '{2}': {3}.
                    argumentResult.ErrorMessage = string.Format(
                        LocalizableStrings.ParseChoiceTemplateOption_Error_InvalidArgument,
                        argumentResult.Tokens[0].Value,
                        or.Token.Value,
                        "choice",
                        error);
                    return string.Empty;
                }
                else
                {
                    //Using more than 1 argument is not allowed for '{0}', used: {1}.
                    argumentResult.ErrorMessage = string.Format(LocalizableStrings.ParseTemplateOption_Error_InvalidCount, or.Token.Value, argumentResult.Tokens.Count);
                    return string.Empty;
                }
            };
        }

        private static bool TryConvertValueToChoice(string value, ChoiceTemplateParameter parameter, out string parsedValue, out string error)
        {
            parsedValue = string.Empty;
            if (parameter.Choices == null)
            {
                //no choices are defined for parameter
                error = LocalizableStrings.ParseChoiceTemplateOption_ErrorText_NoChoicesDefined;
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
            //value '{0}' is not allowed, allowed values are: {1}
            error = string.Format(
                LocalizableStrings.ParseChoiceTemplateOption_ErrorText_InvalidChoiceValue,
                value,
                string.Join(",", parameter.Choices.Keys.Select(key => $"'{key}'")));
            return false;
        }
    }
}
