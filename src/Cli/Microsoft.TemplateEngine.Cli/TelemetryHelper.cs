// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Cli
{
    internal static class TelemetryHelper
    {
        private const string ChoiceValueSeparator = ";";

        /// <summary>
        ///  Checks if the <paramref name="parameterValues"/> contains a valid value for choice parameter name <paramref name="parameterName"/>.
        ///  If so, returns hashed value of parameter value, otherwise <see langword="null"/>.
        /// </summary>
        internal static string? PrepareHashedChoiceValue(ITemplateInfo template, IReadOnlyDictionary<string, string?> parameterValues, string parameterName)
        {
            if (!parameterValues.TryGetValue(parameterName, out string? parameterValue))
            {
                return null;
            }
            if (parameterValue == null)
            {
                return null;
            }
            ITemplateParameter? parameter = template.ParameterDefinitions.FirstOrDefault(x => string.Equals(x.Name, parameterName, StringComparison.Ordinal));
            if (parameter == null || !parameter.IsChoice())
            {
                //not a choice parameter
                return null;
            }
            IEnumerable<string> choiceValues = parameterValue.TokenizeMultiValueParameter();
            List<string> hashedValues = new();
            foreach (string choiceValue in choiceValues)
            {
                if (parameter.Choices?.ContainsKey(choiceValue) ?? false)
                {
                    //if value is a valid choice value for parameter hash it and return
                    hashedValues.Add(Sha256Hasher.HashWithNormalizedCasing(choiceValue));
                }
            }
            if (hashedValues.Count > 0)
            {
                return string.Join(ChoiceValueSeparator, hashedValues);
            }
            return null;
        }
    }
}
