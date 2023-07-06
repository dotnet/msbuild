// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplateFiltering;

namespace Microsoft.TemplateEngine.Cli.TemplateResolution
{
    internal static class CliFilters
    {
        /// <summary>
        /// Filters <see cref="TemplateGroup"/> by short name.
        /// The fields to be compared are <see cref="TemplateGroup.ShortNames"/> and they should exactly match user input.
        /// </summary>
        /// <param name="name">the name to match with group short names.</param>
        /// <returns></returns>
        internal static Func<TemplateGroup, MatchInfo?> ExactShortNameTemplateGroupFilter(string name)
        {
            return (templateGroup) =>
            {
                if (string.IsNullOrEmpty(name))
                {
                    return new MatchInfo(MatchInfo.BuiltIn.ShortName, name, MatchKind.Mismatch);
                }
                foreach (string shortName in templateGroup.ShortNames)
                {
                    if (shortName.Equals(name, StringComparison.OrdinalIgnoreCase))
                    {
                        return new MatchInfo(MatchInfo.BuiltIn.ShortName, name, MatchKind.Exact);
                    }
                }
                return new MatchInfo(MatchInfo.BuiltIn.ShortName, name, MatchKind.Mismatch);
            };
        }

        /// <summary>
        /// Filters <see cref="TemplateGroup"/> by name.
        /// The fields to be compared are <see cref="TemplateGroup.Name"/> and <see cref="TemplateGroup.ShortNames"/>.
        /// </summary>
        /// <param name="name">the name to match with template group name or short name.</param>
        /// <returns></returns>
        internal static Func<TemplateGroup, MatchInfo?> NameTemplateGroupFilter(string? name)
        {
            return (templateGroup) =>
            {
                if (string.IsNullOrEmpty(name))
                {
                    return new MatchInfo(MatchInfo.BuiltIn.Name, name, MatchKind.Partial);
                }

                int nameIndex = templateGroup.Name.IndexOf(name, StringComparison.CurrentCultureIgnoreCase);

                if (nameIndex == 0 && templateGroup.Name.Length == name.Length)
                {
                    return new MatchInfo(MatchInfo.BuiltIn.Name, name, MatchKind.Exact);
                }

                bool hasShortNamePartialMatch = false;

                foreach (string shortName in templateGroup.ShortNames)
                {
                    int shortNameIndex = shortName.IndexOf(name, StringComparison.OrdinalIgnoreCase);

                    if (shortNameIndex == 0 && shortName.Length == name.Length)
                    {
                        return new MatchInfo(MatchInfo.BuiltIn.ShortName, name, MatchKind.Exact);
                    }

                    hasShortNamePartialMatch |= shortNameIndex > -1;
                }

                if (nameIndex > -1)
                {
                    return new MatchInfo(MatchInfo.BuiltIn.Name, name, MatchKind.Partial);
                }

                if (hasShortNamePartialMatch)
                {
                    return new MatchInfo(MatchInfo.BuiltIn.ShortName, name, MatchKind.Partial);
                }

                return new MatchInfo(MatchInfo.BuiltIn.Name, name, MatchKind.Mismatch);
            };
        }

        internal static Func<ITemplateInfo, IEnumerable<MatchInfo>>? EmptyTemplateParameterFilter() => (templateInfo) => Array.Empty<MatchInfo>();

        /// <summary>
        /// Filters <see cref="TemplateGroup"/> by language.
        /// </summary>
        /// <param name="language">the language from command input.</param>
        /// <param name="defaultLanguage">the default language.</param>
        /// <returns></returns>
        internal static Func<TemplateGroup, MatchInfo?> LanguageGroupFilter(string? language, string? defaultLanguage)
        {
            return (templateGroup) =>
            {
                if (string.IsNullOrWhiteSpace(language) && string.IsNullOrWhiteSpace(defaultLanguage))
                {
                    return null;
                }
                IEnumerable<string?> templateLanguages = templateGroup.Languages;

                if (!string.IsNullOrWhiteSpace(language))
                {
                    // only add default language disposition when there is a language specified for the template.
                    if (templateLanguages.Any(lang => string.IsNullOrWhiteSpace(lang)))
                    {
                        return null;
                    }
                    if (templateLanguages.Contains(language, StringComparer.OrdinalIgnoreCase))
                    {
                        return new MatchInfo(MatchInfo.BuiltIn.Language, language, MatchKind.Exact);
                    }
                    else
                    {
                        return new MatchInfo(MatchInfo.BuiltIn.Language, language, MatchKind.Mismatch);
                    }
                }

                if (!string.IsNullOrWhiteSpace(defaultLanguage))
                {
                    if (templateLanguages.Contains(defaultLanguage, StringComparer.OrdinalIgnoreCase))
                    {
                        return new MatchInfo(MatchInfo.BuiltIn.Language, defaultLanguage, MatchKind.Exact);
                    }
                }
                if (templateLanguages.Count() == 1)
                {
                    //if only one language is defined, this is the language to be taken
                    return new MatchInfo(MatchInfo.BuiltIn.Language, language, MatchKind.Exact);
                }
                return null;
            };
        }
    }
}
