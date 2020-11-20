using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Template;
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
        /// - Type
        /// </summary>
        /// <param name="templateList">list of templates to be displayed</param>
        /// <param name="language">language from the command input</param>
        /// <param name="defaultLanguage">default language</param>
        /// <returns></returns>
        internal static IReadOnlyList<TemplateGroupTableRow> GetTemplateGroupsForListDisplay(IReadOnlyCollection<ITemplateMatchInfo> templateList, string language, string defaultLanguage)
        {
            List<TemplateGroupTableRow> templateGroupsForDisplay = new List<TemplateGroupTableRow>();
            IEnumerable<IGrouping<string, ITemplateMatchInfo>> groupedTemplateList = templateList.GroupBy(x => x.Info.GroupIdentity, x => !string.IsNullOrEmpty(x.Info.GroupIdentity), StringComparer.OrdinalIgnoreCase);
            foreach (IGrouping<string, ITemplateMatchInfo> grouping in groupedTemplateList)
            {
                List<string> languageForDisplay = new List<string>();
                HashSet<string> uniqueLanguages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                string defaultLanguageDisplay = string.Empty;

                foreach (ITemplateMatchInfo template in grouping)
                {
                    string lang = template.Info.GetLanguage();
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

                ITemplateMatchInfo highestPrecedenceTemplate = grouping.OrderByDescending(x => x.Info.Precedence).First();
                string shortName;
                if (highestPrecedenceTemplate.Info is IShortNameList highestWithShortNameList)
                {
                    shortName = highestWithShortNameList.ShortNameList[0];
                }
                else
                {
                    shortName = highestPrecedenceTemplate.Info.ShortName;
                }

                TemplateGroupTableRow groupDisplayInfo = new TemplateGroupTableRow()
                {
                    Name = highestPrecedenceTemplate.Info.Name,
                    ShortName = shortName,
                    Languages = string.Join(",", languageForDisplay),
                    Classifications = highestPrecedenceTemplate.Info.Classifications != null ? string.Join("/", highestPrecedenceTemplate.Info.Classifications) : null,
                    Author = highestPrecedenceTemplate.Info.Author,
                    Type = highestPrecedenceTemplate.Info.GetTemplateType() ?? string.Empty
                };
                templateGroupsForDisplay.Add(groupDisplayInfo);
            }

            return templateGroupsForDisplay;
        }
    }
}
