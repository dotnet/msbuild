// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    /// <summary>
    /// The class represents the information about the invalid template option used when executing the command.
    /// </summary>
    internal class InvalidTemplateOptionResult : TemplateOptionResult, IEquatable<InvalidTemplateOptionResult>
    {
        internal InvalidTemplateOptionResult(
            TemplateOption? templateOption,
            Kind kind,
            string inputFormat,
            string? specifiedValue,
            string? errorMessage) : base(templateOption, inputFormat, specifiedValue)
        {
            ErrorKind = kind;
            ErrorMessage = errorMessage;
        }

        /// <summary>
        /// Defines the possible reason for the parameter to be invalid.
        /// </summary>
        internal enum Kind
        {
            /// <summary>
            /// The name is invalid.
            /// </summary>
            InvalidName,

            /// <summary>
            /// The value is invalid.
            /// </summary>
            InvalidValue,
        }

        /// <summary>
        /// The reason why the parameter is invalid.
        /// </summary>
        internal Kind ErrorKind { get; }

        internal string? ErrorMessage { get; private set; }

        internal bool IsChoice => TemplateOption?.TemplateParameter is ChoiceTemplateParameter;

        public override bool Equals(object? obj)
        {
            if (obj is InvalidTemplateOptionResult info)
            {
                if (InputFormat != info.InputFormat || info.ErrorKind != ErrorKind)
                {
                    return false;
                }

                if (TemplateOption == null && info.TemplateOption == null)
                {
                    return true;
                }

                if (TemplateOption == null || info.TemplateOption == null)
                {
                    return false;
                }
                return TemplateOption.TemplateParameter.Name.Equals(info.TemplateOption?.TemplateParameter.Name, StringComparison.OrdinalIgnoreCase);
            }
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return new { a = TemplateOption?.TemplateParameter.Name?.ToLowerInvariant(), b = ErrorKind, c = InputFormat }.GetHashCode();
        }

        public bool Equals(InvalidTemplateOptionResult? other)
        {
            return Equals(other as object);
        }

        internal static InvalidTemplateOptionResult FromParseError(TemplateOption option, ParseResult parseResult, ParseError error)
        {
            OptionResult? optionResult = parseResult.GetResult(option.Option);
            if (optionResult == null)
            {
                //option is not specified
                throw new ArgumentException($"{nameof(option)}  is not used in {nameof(parseResult)}");
            }

            string? optionValue = null;
            if (optionResult.Tokens.Any())
            {
                optionValue = string.Join(", ", optionResult.Tokens.Select(t => t.Value));
            }

            return new InvalidTemplateOptionResult(
                    option,
                    Kind.InvalidValue,
                    optionResult.IdentifierToken?.Value ?? string.Empty,
                    optionValue,
                    error.Message);
        }

        /// <summary>
        /// Corrects the error message for choice parameter. It should include possible choice values from other templates in the group (passed via <paramref name="templates"/>).
        /// </summary>
        /// <param name="templates"></param>
        internal void CorrectErrorMessageForChoice(IEnumerable<TemplateResult> templates)
        {
            if (TemplateOption is null)
            {
                throw new NotSupportedException($"Method is not invokable when {nameof(TemplateOption)} is null");
            }

            StringBuilder error = new();
            error.AppendFormat(LocalizableStrings.InvalidParameterDetail, InputFormat, SpecifiedValue);
            ErrorMessage = AppendAllowedValues(error, GetValidValuesForChoiceParameter(templates, TemplateOption.TemplateParameter)).ToString();
        }

        /// <summary>
        /// Gets the list of valid choices for <paramref name="parameter"/>.
        /// </summary>
        /// <returns>the dictionary of valid choices and descriptions.</returns>
        private static IDictionary<string, ParameterChoice> GetValidValuesForChoiceParameter(
            IEnumerable<TemplateResult> templates,
            CliTemplateParameter parameter)
        {
            Dictionary<string, ParameterChoice> validChoices = new();
            foreach (CliTemplateInfo template in templates.Select(template => template.TemplateInfo))
            {
                if (template.CliParameters.TryGetValue(parameter.Name, out CliTemplateParameter? param))
                {
                    if (param is ChoiceTemplateParameter choiceParam)
                    {
                        foreach (var choice in choiceParam.Choices)
                        {
                            validChoices[choice.Key] = choice.Value;
                        }
                    }
                }
            }
            return validChoices;
        }

        private static StringBuilder AppendAllowedValues(StringBuilder text, IDictionary<string, ParameterChoice> possibleValues)
        {
            if (!possibleValues.Any())
            {
                return text;
            }

            text.Append(' ').Append(LocalizableStrings.PossibleValuesHeader);
            int longestChoiceLength = possibleValues.Keys.Max(x => x.Length);
            foreach (KeyValuePair<string, ParameterChoice> choiceInfo in possibleValues.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                text.AppendLine();
                text.Indent(1).Append(choiceInfo.Key.PadRight(longestChoiceLength));
                if (!string.IsNullOrWhiteSpace(choiceInfo.Value.Description))
                {
                    text.Indent().Append("- " + choiceInfo.Value.Description);
                }
            }
            return text;
        }
    }
}
