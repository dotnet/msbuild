// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Constraints;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Cli
{
    internal static class Extensions
    {
        /// <summary>
        /// Gets default language configured for the host.
        /// </summary>
        internal static string? GetDefaultLanguage(this IEngineEnvironmentSettings settings)
        {
            if (!settings.Host.TryGetHostParamDefault("prefs:language", out string? defaultLanguage))
            {
                return null;
            }
            return defaultLanguage;
        }

        /// <summary>
        /// Checks if the template is hidden by host specific template settings.
        /// </summary>
        internal static bool IsHiddenByHostFile(this ITemplateInfo template, IHostSpecificDataLoader hostSpecificDataLoader)
        {
            HostSpecificTemplateData hostData = hostSpecificDataLoader.ReadHostSpecificTemplateData(template);
            return hostData.IsHidden;
        }

        /// <summary>
        /// Gets display name for the template:
        /// Template Name (short-name)
        /// Template Name (short-name) Language
        /// Template Name (short-name) Language (identity: identity).
        /// </summary>
        internal static string GetDisplayName(this ITemplateInfo template, bool showIdentity = false)
        {
            StringBuilder stringBuilder = new();
            string? templateLanguage = template.GetLanguage();
            string shortNames = string.Join(",", template.ShortNameList);
            stringBuilder.Append(template.Name);
            if (!string.IsNullOrWhiteSpace(shortNames))
            {
                stringBuilder.Append($" ({shortNames})");
            }
            if (!string.IsNullOrWhiteSpace(templateLanguage))
            {
                stringBuilder.Append($" {templateLanguage}");
            }
            if (showIdentity)
            {
                stringBuilder.Append($"(identity: {template.Identity})");
            }
            return stringBuilder.ToString();
        }

        /// <summary>
        /// Gets display string for constraint evaluation result.
        /// </summary>
        internal static string ToDisplayString(this TemplateConstraintResult constraintResult)
        {
            StringBuilder stringBuilder = new();

            string? constraintDisplayName = constraintResult.Constraint?.DisplayName;
            if (string.IsNullOrWhiteSpace(constraintDisplayName))
            {
                constraintDisplayName = constraintResult.ConstraintType;
            }

            stringBuilder.Append($"{constraintDisplayName}: {constraintResult.LocalizedErrorMessage}");
            if (!string.IsNullOrWhiteSpace(constraintResult.CallToAction))
            {
                stringBuilder.Append($" {constraintResult.CallToAction}");
            }
            return stringBuilder.ToString();
        }
    }
}
