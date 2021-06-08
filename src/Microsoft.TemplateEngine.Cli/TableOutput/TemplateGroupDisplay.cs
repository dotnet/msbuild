// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
        internal static IReadOnlyList<TemplateGroupTableRow> GetTemplateGroupsForListDisplay(IEnumerable<ITemplateInfo> templateList, string language, string defaultLanguage)
        {
            List<TemplateGroupTableRow> templateGroupsForDisplay = new List<TemplateGroupTableRow>();
            IEnumerable<IGrouping<string, ITemplateInfo>> groupedTemplateList = templateList.GroupBy(x => x.GroupIdentity, x => !string.IsNullOrEmpty(x.GroupIdentity), StringComparer.OrdinalIgnoreCase);
            foreach (IGrouping<string, ITemplateInfo> grouping in groupedTemplateList)
            {
                templateGroupsForDisplay.Add(GetTemplateGroupRow(grouping, language, defaultLanguage));
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
        internal static IReadOnlyList<TemplateGroupTableRow> GetTemplateGroupsForListDisplay(IReadOnlyCollection<TemplateGroup> templateGroupList, string language, string defaultLanguage)
        {
            List<TemplateGroupTableRow> templateGroupsForDisplay = new List<TemplateGroupTableRow>();
            foreach (TemplateGroup templateGroup in templateGroupList)
            {
                templateGroupsForDisplay.Add(GetTemplateGroupRow(templateGroup.Templates.Select(mi => mi.Info), language, defaultLanguage));
            }
            return templateGroupsForDisplay;
        }

        private static TemplateGroupTableRow GetTemplateGroupRow(IEnumerable<ITemplateInfo> templateGroup, string language, string defaultLanguage)
        {
            List<string> languageForDisplay = new List<string>();
            HashSet<string> uniqueLanguages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string defaultLanguageDisplay = string.Empty;
            foreach (ITemplateInfo template in templateGroup)
            {
                string lang = template.GetLanguage();
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
                    languageForDisplay.Add(lang);
                }
            }

            languageForDisplay.Sort(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(defaultLanguageDisplay))
            {
                languageForDisplay.Insert(0, defaultLanguageDisplay);
            }

            ITemplateInfo highestPrecedenceTemplate = templateGroup.OrderByDescending(x => x.Precedence).First();
            string shortName = highestPrecedenceTemplate.ShortNameList[0];

            TemplateGroupTableRow groupDisplayInfo = new TemplateGroupTableRow
            {
                Name = highestPrecedenceTemplate.Name,
                ShortName = shortName,
                Languages = string.Join(",", languageForDisplay),
                Classifications = highestPrecedenceTemplate.Classifications != null ? string.Join("/", highestPrecedenceTemplate.Classifications) : null,
                Author = highestPrecedenceTemplate.Author,
                Type = highestPrecedenceTemplate.GetTemplateType() ?? string.Empty
            };
            return groupDisplayInfo;
        }
    }
}
