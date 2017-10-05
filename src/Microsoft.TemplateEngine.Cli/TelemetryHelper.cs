// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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

        public static string GetHash(string toHash)
        {
            if (toHash == null)
            {
                return null;
            }

            byte[] bytesToHash = Encoding.UTF8.GetBytes(toHash);
            using (HMACSHA256 hmac = new HMACSHA256(new byte[64]))
            {
                byte[] hashedBytes = hmac.ComputeHash(bytesToHash);
                return BitConverter.ToString(hashedBytes).Replace("-", "");
            }
        }
    }
}
