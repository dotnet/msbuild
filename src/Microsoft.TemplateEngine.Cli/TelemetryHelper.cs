// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli
{
    internal static class TelemetryHelper
    {
        // Checks if the input parameter value is a valid choice for the parameter, and returns the canonical value, or defaultValue if there is no appropriate canonical value.
        // If the parameter or value is null, return defaultValue.
        // For this to return other than the defaultValue, one of these must occur:
        //  - the input value must either exactly match one of the choices (case-insensitive)
        //  - there must be exactly one choice value starting with the input value (case-insensitive).
        internal static string GetCanonicalValueForChoiceParamOrDefault(ITemplateInfo template, string paramName, string inputParamValue, string defaultValue = null)
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

        /// <summary>
        /// // The hashed mac address needs to be the same hashed value as produced by the other distinct sources given the same input. (e.g. VsCode).
        /// </summary>
        internal static string Hash(string text)
        {
            var sha256 = SHA256.Create();
            return HashInFormat(sha256, text);
        }

        internal static string HashWithNormalizedCasing(string text)
        {
            if (text == null)
            {
                return null;
            }

            return Hash(text.ToUpper());
        }

        private static string HashInFormat(SHA256 sha256, string text)
        {
            if (text == null)
            {
                return null;
            }

            byte[] bytes = Encoding.UTF8.GetBytes(text);
            byte[] hash = sha256.ComputeHash(bytes);
            StringBuilder hashString = new StringBuilder();
            foreach (byte x in hash)
            {
                hashString.AppendFormat("{0:x2}", x);
            }
            return hashString.ToString();
        }
    }
}
