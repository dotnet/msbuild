// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.TemplateResolution;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Cli.TableOutput
{
    internal static class TemplateGroupDisplay
    {
        /// <summary>
        /// Generates the list of template groups for table display.
        /// Except where noted, the values are taken from the highest-precedence template in the group. The info could vary among the templates in the group, but shouldn't. (There is no check that the info doesn't vary.)
        /// - Template Name
        /// - Short Name: displays the first short name from the highest precedence template in the group.
        /// - Language: All languages supported by any template in the group are displayed, with the default language in brackets, e.g.: [C#]
        /// - Tags
        /// - Author
        /// - Type.
        /// </summary>
        /// <param name="templateList">list of templates to be displayed.</param>
        /// <param name="language">language from the command input.</param>
        /// <param name="defaultLanguage">default language.</param>
        /// <returns></returns>
        internal static IReadOnlyList<TemplateGroupTableRow> GetTemplateGroupsForListDisplay(IEnumerable<ITemplateInfo> templateList, string? language, string? defaultLanguage)
        {
            List<TemplateGroupTableRow> templateGroupsForDisplay = new List<TemplateGroupTableRow>();
            IEnumerable<IGrouping<string?, ITemplateInfo>> groupedTemplateList = templateList.GroupBy(x => x.GroupIdentity, x => !string.IsNullOrEmpty(x.GroupIdentity), StringComparer.OrdinalIgnoreCase);
            foreach (IGrouping<string?, ITemplateInfo> templateGroup in groupedTemplateList)
            {
                ITemplateInfo highestPrecedenceTemplate = templateGroup.OrderByDescending(x => x.Precedence).First();
                string shortNames = string.Join(",", templateGroup.SelectMany(t => t.ShortNameList).Distinct(StringComparer.OrdinalIgnoreCase));

                TemplateGroupTableRow groupDisplayInfo = new TemplateGroupTableRow
                {
                    Name = highestPrecedenceTemplate.Name,
                    ShortNames = shortNames,
                    Languages = string.Join(",", GetLanguagesToDisplay(templateGroup, language, defaultLanguage)),
                    Classifications = highestPrecedenceTemplate.Classifications != null ? string.Join("/", highestPrecedenceTemplate.Classifications) : string.Empty,
                    Author = highestPrecedenceTemplate.Author ?? string.Empty,
                    Type = highestPrecedenceTemplate.GetTemplateType() ?? string.Empty
                };
                templateGroupsForDisplay.Add(groupDisplayInfo);
            }

            return templateGroupsForDisplay;
        }

        /// <summary>
        /// Generates the list of template groups for table display.
        /// Except where noted, the values are taken from the highest-precedence template in the group. The info could vary among the templates in the group, but shouldn't. (There is no check that the info doesn't vary.)
        /// - Template Name
        /// - Short Name: displays the first short name from the highest precedence template in the group.
        /// - Language: All languages supported by any template in the group are displayed, with the default language in brackets, e.g.: [C#]
        /// - Tags
        /// - Author
        /// - Type.
        /// </summary>
        /// <param name="templateGroupList">list of template groups to be displayed.</param>
        /// <param name="language">language from the command input.</param>
        /// <param name="defaultLanguage">default language.</param>
        /// <returns></returns>
        internal static IReadOnlyList<TemplateGroupTableRow> GetTemplateGroupsForListDisplay(IReadOnlyCollection<TemplateGroup> templateGroupList, string? language, string? defaultLanguage)
        {
            List<TemplateGroupTableRow> templateGroupsForDisplay = new List<TemplateGroupTableRow>();
            foreach (TemplateGroup templateGroup in templateGroupList)
            {
                ITemplateInfo highestPrecedenceTemplate = templateGroup.Templates.OrderByDescending(x => x.Info.Precedence).First().Info;
                TemplateGroupTableRow groupDisplayInfo = new TemplateGroupTableRow
                {
                    Name = highestPrecedenceTemplate.Name,
                    ShortNames = string.Join(",", templateGroup.ShortNames),
                    Languages = string.Join(",", GetLanguagesToDisplay(templateGroup.Templates.Select(t => t.Info), language, defaultLanguage)),
                    Classifications = highestPrecedenceTemplate.Classifications != null ? string.Join("/", highestPrecedenceTemplate.Classifications) : string.Empty,
                    Author = highestPrecedenceTemplate.Author ?? string.Empty,
                    Type = highestPrecedenceTemplate.GetTemplateType() ?? string.Empty
                };
                templateGroupsForDisplay.Add(groupDisplayInfo);
            }
            return templateGroupsForDisplay;
        }

        private static IEnumerable<string> GetLanguagesToDisplay(IEnumerable<ITemplateInfo> templateGroup, string? language, string? defaultLanguage)
        {
            List<string> languagesForDisplay = new List<string>();
            HashSet<string> uniqueLanguages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string defaultLanguageDisplay = string.Empty;
            foreach (ITemplateInfo template in templateGroup)
            {
                string? lang = template.GetLanguage();
                if (string.IsNullOrWhiteSpace(lang))
                {
                    continue;
                }

                if (!uniqueLanguages.Add(lang))
                {
                    continue;
                }
                if (string.IsNullOrEmpty(language) && string.Equals(defaultLanguage, lang, StringComparison.OrdinalIgnoreCase))
                {
                    defaultLanguageDisplay = $"[{lang}]";
                }
                else
                {
                    languagesForDisplay.Add(lang);
                }
            }

            languagesForDisplay.Sort(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(defaultLanguageDisplay))
            {
                languagesForDisplay.Insert(0, defaultLanguageDisplay);
            }
            return languagesForDisplay;
        }
    }
}
