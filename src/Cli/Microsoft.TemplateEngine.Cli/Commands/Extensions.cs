// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Constraints;
using Microsoft.TemplateEngine.Edge;

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

        /// <summary>
        /// Case insensitive version for <see cref="System.CommandLine.Option{TOption}.AcceptOnlyFromAmong(string[])"/>.
        /// </summary>
        internal static void FromAmongCaseInsensitive(this Option<string> option, string[]? allowedValues = null, string? allowedHiddenValue = null)
        {
            allowedValues ??= Array.Empty<string>();
            option.AddValidator(optionResult => ValidateAllowedValues(optionResult, allowedValues, allowedHiddenValue));
            option.AddCompletions(allowedValues);
        }

        /// <summary>
        /// Gets the list of allowed templates from <paramref name="templateGroup"/> as the result of constraints evaluation.
        /// </summary>
        internal static async Task<IEnumerable<CliTemplateInfo>> GetAllowedTemplatesAsync(this TemplateGroup templateGroup, TemplateConstraintManager constraintManager, CancellationToken cancellationToken)
        {
            IReadOnlyList<(ITemplateInfo Template, IReadOnlyList<TemplateConstraintResult> Result)> results =
                await constraintManager.EvaluateConstraintsAsync(templateGroup.Templates, cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            return results.Where(r => r.Result.IsTemplateAllowed()).Select(r => r.Template).Cast<CliTemplateInfo>();
        }

        /// <summary>
        /// Returns true if the execution is allowed based on <paramref name="constraintResult"/>.
        /// </summary>
        internal static bool IsTemplateAllowed(this IEnumerable<TemplateConstraintResult> constraintResult)
        {
            return constraintResult.All(s => s.EvaluationStatus == TemplateConstraintResult.Status.Allowed);
        }

        private static void ValidateAllowedValues(OptionResult optionResult, string[] allowedValues, string? allowedHiddenValue = null)
        {
            var invalidArguments = optionResult.Tokens.Where(token => !allowedValues.Append(allowedHiddenValue).Contains(token.Value, StringComparer.OrdinalIgnoreCase)).ToList();
            if (invalidArguments.Any())
            {
                optionResult.ErrorMessage = string.Format(
                    LocalizableStrings.Commands_Validator_WrongArgumentValue,
                    string.Join(", ", invalidArguments.Select(arg => $"'{arg.Value}'")),
                    string.Join(", ", allowedValues.Select(allowedValue => $"'{allowedValue}'")));
            }
        }
    }
}
