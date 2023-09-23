// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.Commands;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Cli
{
    /// <summary>
    /// The class combines information from <see cref="ITemplateParameter"/> and <see cref="HostSpecificTemplateData"/> for choice parameter.
    /// Other parameters are implemented in base class <see cref="CliTemplateParameter"/>.
    /// </summary>
    internal class ChoiceTemplateParameter : CliTemplateParameter
    {
        private Dictionary<string, ParameterChoice> _choices = new(StringComparer.OrdinalIgnoreCase);

        internal ChoiceTemplateParameter(ITemplateParameter parameter, HostSpecificTemplateData data) : base(parameter, data)
        {
            if (!parameter.IsChoice())
            {
                throw new ArgumentException($"{nameof(parameter)} should have {nameof(parameter.Type)} {nameof(ParameterType.Choice)}");
            }
            if (parameter.Choices == null)
            {
                throw new ArgumentException($"{nameof(parameter)} should have {nameof(parameter.Choices)}");
            }
            _choices = parameter.Choices.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
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
                string standardUsage = HelpBuilder.Default.GetOptionUsageLabel(o.Option);
                if (standardUsage.Length > context.HelpBuilder.MaxWidth / 3)
                {
                    if (Choices.Count > 2)
                    {
                        o.Option.HelpName = $"{string.Join("|", Choices.Keys.Take(2))}|...";
                        string updatedFirstColumn = HelpBuilder.Default.GetOptionUsageLabel(o.Option);
                        if (updatedFirstColumn.Length <= context.HelpBuilder.MaxWidth / 3)
                        {
                            return updatedFirstColumn;
                        }
                    }
                    o.Option.HelpName = HelpStrings.Text_ChoiceArgumentHelpName;
                    return HelpBuilder.Default.GetOptionUsageLabel(o.Option);
                }
                return standardUsage;
            };
        }

        protected override CliOption GetBaseOption(IReadOnlySet<string> aliases)
        {
            string name = GetName(aliases);

            CliOption<string> option = new(name)
            {
                CustomParser = result => GetParseChoiceArgument(this)(result),
                Arity = new ArgumentArity(DefaultIfOptionWithoutValue == null ? 1 : 0, AllowMultipleValues ? _choices.Count : 1),
                AllowMultipleArgumentsPerToken = AllowMultipleValues
            };

            AddAliases(option, aliases);

            // empty string for the explicit unset option
            option.FromAmongCaseInsensitive(Choices.Keys.ToArray(), allowedHiddenValue: string.Empty);

            return option;
        }

        /// <summary>
        /// Custom parse method for template option.
        /// It is mainly required to process default if no option value cases.
        /// </summary>
        private static Func<ArgumentResult, string> GetParseChoiceArgument(ChoiceTemplateParameter parameter)
        {
            return (argumentResult) =>
            {
                if (argumentResult.Parent is not OptionResult or)
                {
                    throw new NotSupportedException("The method should be only used with option.");
                }

                if (argumentResult.Tokens.Count == 0)
                {
                    if (or.Implicit)
                    {
                        if (!string.IsNullOrWhiteSpace(parameter.DefaultValue))
                        {
                            if (TryConvertValueToChoice(parameter.DefaultValue, parameter, out string defaultValue, out string error))
                            {
                                return defaultValue;
                            }
                            //Cannot parse default value '{0}' for option '{1}' as expected type '{2}': {3}.
                            argumentResult.AddError(string.Format(
                                LocalizableStrings.ParseChoiceTemplateOption_Error_InvalidDefaultValue,
                                parameter.DefaultValue,
                                or.IdentifierToken?.Value,
                                "choice",
                                error));
                            return string.Empty;
                        }
                        //Default value for argument missing for option: '{0}'.
                        argumentResult.AddError(string.Format(LocalizableStrings.ParseTemplateOption_Error_MissingDefaultValue, or.IdentifierToken?.Value));
                        return string.Empty;
                    }
                    if (parameter.DefaultIfOptionWithoutValue != null)
                    {
                        if (TryConvertValueToChoice(parameter.DefaultIfOptionWithoutValue, parameter, out string defaultValue, out string error))
                        {
                            return defaultValue;
                        }
                        //Cannot parse default if option without value '{0}' for option '{1}' as expected type '{2}': {3}.
                        argumentResult.AddError(string.Format(
                            LocalizableStrings.ParseChoiceTemplateOption_Error_InvalidDefaultIfNoOptionValue,
                            parameter.DefaultIfOptionWithoutValue,
                            or.IdentifierToken?.Value,
                            "choice",
                            error));
                        return string.Empty;
                    }
                    //Required argument missing for option: '{0}'.
                    argumentResult.AddError(string.Format(LocalizableStrings.ParseTemplateOption_Error_MissingDefaultIfNoOptionValue, or.IdentifierToken?.Value));
                    return string.Empty;
                }
                else if (!parameter.AllowMultipleValues && argumentResult.Tokens.Count != 1)
                {
                    //Using more than 1 argument is not allowed for '{0}', used: {1}.
                    argumentResult.AddError(string.Format(LocalizableStrings.ParseTemplateOption_Error_InvalidCount, or.IdentifierToken?.Value, argumentResult.Tokens.Count));
                    return string.Empty;
                }
                else
                {
                    if (!TryConvertValueToChoice(argumentResult.Tokens.Select(t => t.Value), parameter, out string value, out string error))
                    {
                        //Cannot parse argument '{0}' for option '{1}' as expected type '{2}': {3}.
                        argumentResult.AddError(string.Format(
                            LocalizableStrings.ParseChoiceTemplateOption_Error_InvalidArgument,
                            argumentResult.Tokens[0].Value,
                            or.IdentifierToken?.Value,
                            "choice",
                            error));
                        return string.Empty;
                    }

                    return value;
                }
            };
        }

        private static bool TryConvertValueToChoice(string value, ChoiceTemplateParameter parameter, out string parsedValue, out string error)
        {
            return TryConvertValueToChoice(value.TokenizeMultiValueParameter(), parameter, out parsedValue, out error);
        }

        private static bool TryConvertValueToChoice(IEnumerable<string> values, ChoiceTemplateParameter parameter, out string parsedValue, out string error)
        {
            parsedValue = string.Empty;
            error = string.Empty;

            List<string> parsedValues = new();
            foreach (string val in values)
            {
                if (!TryConvertSingleValueToChoice(val, parameter, out string value, out error))
                {
                    return false;
                }
                parsedValues.Add(value);
            }

            // An empty value is not allowed when multiple choice values are specified.
            if (parsedValues.Count > 1 && parsedValues.Any(string.IsNullOrEmpty))
            {
                error = CreateParseError(string.Empty, parameter);
                return false;
            }

            parsedValue = string.Join(MultiValueParameter.MultiValueSeparator, parsedValues);
            return true;
        }

        private static bool TryConvertSingleValueToChoice(string value, ChoiceTemplateParameter parameter, out string parsedValue, out string error)
        {
            parsedValue = string.Empty;
            error = string.Empty;

            if (value.Equals(string.Empty))
            {
                return true;
            }

            foreach (string choiceValue in parameter.Choices.Keys)
            {
                if (string.Equals(choiceValue, value, StringComparison.OrdinalIgnoreCase))
                {
                    parsedValue = choiceValue;
                    return true;
                }
            }
            error = CreateParseError(value, parameter);
            return false;
        }

        private static string CreateParseError(string value, ChoiceTemplateParameter parameter)
        {
            //value '{0}' is not allowed, allowed values are: {1}
            return string.Format(
                LocalizableStrings.ParseChoiceTemplateOption_ErrorText_InvalidChoiceValue,
                value,
                string.Join(",", parameter.Choices.Keys.Select(key => $"'{key}'")));
        }
    }
}
