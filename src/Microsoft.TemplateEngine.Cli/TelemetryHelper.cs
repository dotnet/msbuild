using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli
{
    public static class TelemetryHelper
    {
        // Checks if the input parameter value is a valid choice for the parameter, and returns the canonical value, or defaultValue if there is no appropriate canonical value.
        // If the parameter or value is null, return defaultValue.
        // For this to return other than the defaultValue, one of these must occur:
        //  - the input value must either exactly match one of the choices (case-insensitive)
        //  - there must be exactly one choice value starting with the input value (case-insensitive).
        public static string GetCanonicalValueForChoiceParamOrDefault(ITemplateInfo template, string paramName, string inputParamValue, string defaultValue = null)
        {
            if (string.IsNullOrEmpty(paramName) || string.IsNullOrEmpty(inputParamValue))
            {
                return defaultValue;
            }

            ITemplateParameter parameter = template.Parameters.FirstOrDefault(x => string.Equals(x.Name, paramName, StringComparison.Ordinal));
            if (parameter == null || parameter.Choices == null || parameter.Choices.Count == 0)
            {
                return defaultValue;
            }

            // This is a case-insensitive key lookup, because that is how Choices is initialized.
            if (parameter.Choices.ContainsKey(inputParamValue))
            {
                return inputParamValue;
            }

            IReadOnlyList<string> startsWithChoices = parameter.Choices.Where(x => x.Key.StartsWith(inputParamValue, StringComparison.OrdinalIgnoreCase)).Select(x => x.Key).ToList();

            if (startsWithChoices.Count == 1)
            {
                return startsWithChoices[0];
            }

            return defaultValue;
        }
    }
}
