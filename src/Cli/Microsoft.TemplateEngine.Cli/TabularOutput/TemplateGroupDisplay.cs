// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.Commands;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Cli.TabularOutput
{
    internal static class TemplateGroupDisplay
    {
        /// <summary>
        /// Displays the list of templates in a table, one row per template group.
        ///
        /// The columns displayed are as follows:
        /// Except where noted, the values are taken from the highest-precedence template in the group. The info could vary among the templates in the group, but shouldn't.
        /// (There is no check that the info doesn't vary.)
        /// - Template Name
        /// - Short Name: displays the all available short names for the group.
        /// - Language: All languages supported by any template in the group are displayed, with the default language in brackets, e.g.: [C#]
        /// - Tags
        /// The columns can be configured via the command args, see <see cref="ITabularOutputArgs"/>/>.
        /// </summary>
        internal static void DisplayTemplateList(
            IEngineEnvironmentSettings engineEnvironmentSettings,
            IEnumerable<TemplateGroup> templateGroups,
            TabularOutputSettings helpFormatterSettings,
            IReporter reporter,
            string? selectedLanguage = null)
        {
            IReadOnlyCollection<TemplateGroupTableRow> groupsForDisplay = GetTemplateGroupsForListDisplay(
                templateGroups,
                selectedLanguage,
                engineEnvironmentSettings.GetDefaultLanguage(),
                engineEnvironmentSettings.Environment);
            DisplayTemplateList(groupsForDisplay, helpFormatterSettings, reporter);
        }

        /// <summary>
        /// Displays the list of templates in a table, one row per template group.
        ///
        /// The columns displayed are as follows:
        /// Except where noted, the values are taken from the highest-precedence template in the group. The info could vary among the templates in the group, but shouldn't.
        /// (There is no check that the info doesn't vary.)
        /// - Template Name
        /// - Short Name: displays the all available short names for the group.
        /// - Language: All languages supported by any template in the group are displayed, with the default language in brackets, e.g.: [C#]
        /// - Tags
        /// The columns can be configured via the command args, see <see cref="ITabularOutputArgs"/>/>.
        /// </summary>
        internal static void DisplayTemplateList(
            IEngineEnvironmentSettings engineEnvironmentSettings,
            IEnumerable<ITemplateInfo> templates,
            TabularOutputSettings helpFormatterSettings,
            IReporter reporter,
            string? selectedLanguage = null)
        {
            IReadOnlyCollection<TemplateGroupTableRow> groupsForDisplay = GetTemplateGroupsForListDisplay(
                templates,
                selectedLanguage,
                engineEnvironmentSettings.GetDefaultLanguage(),
                engineEnvironmentSettings.Environment);
            DisplayTemplateList(groupsForDisplay, helpFormatterSettings, reporter);
        }

        /// <summary>
        /// Displays the template languages.
        /// </summary>
        internal static string GetLanguagesToDisplay(IEnumerable<ITemplateInfo> templateGroup, string? language, string? defaultLanguage, IEnvironment environment)
        {
            var groupedTemplates = GetAuthorBasedGroups(templateGroup);

            List<string> languageGroups = new();
            foreach (var templates in groupedTemplates)
            {
                List<string> languagesForDisplay = new();
                HashSet<string> uniqueLanguages = new(StringComparer.OrdinalIgnoreCase);
                string defaultLanguageDisplay = string.Empty;
                foreach (ITemplateInfo template in templates)
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
                languageGroups.Add(string.Join(",", languagesForDisplay));
            }
            return string.Join(environment.NewLine, languageGroups);
        }

        /// <summary>
        /// Displays the template authors.
        /// </summary>
        internal static string GetAuthorsToDisplay(IEnumerable<ITemplateInfo> templateGroup, IEnvironment environment)
        {
            return string.Join(environment.NewLine, GetAuthorBasedGroups(templateGroup).Select(group => group.Key));
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
        /// <param name="templateList">list of templates to be displayed.</param>
        /// <param name="language">language from the command input.</param>
        /// <param name="defaultLanguage">default language.</param>
        /// <param name="environment"><see cref="IEnvironment"/> settings to use.</param>
        /// <returns></returns>
        internal static IReadOnlyList<TemplateGroupTableRow> GetTemplateGroupsForListDisplay(
            IEnumerable<ITemplateInfo> templateList,
            string? language,
            string? defaultLanguage,
            IEnvironment environment)
        {
            List<TemplateGroupTableRow> templateGroupsForDisplay = new();
            IEnumerable<IGrouping<string?, ITemplateInfo>> groupedTemplateList = templateList.GroupBy(x => x.GroupIdentity, x => !string.IsNullOrEmpty(x.GroupIdentity), StringComparer.OrdinalIgnoreCase);
            foreach (IGrouping<string?, ITemplateInfo> templateGroup in groupedTemplateList)
            {
                ITemplateInfo highestPrecedenceTemplate = templateGroup.OrderByDescending(x => x.Precedence).First();
                string shortNames = string.Join(",", templateGroup.SelectMany(t => t.ShortNameList).Distinct(StringComparer.OrdinalIgnoreCase));

                TemplateGroupTableRow groupDisplayInfo = new()
                {
                    Name = highestPrecedenceTemplate.Name,
                    ShortNames = shortNames,
                    Languages = GetLanguagesToDisplay(templateGroup, language, defaultLanguage, environment),
                    Classifications = GetClassificationsToDisplay(templateGroup, environment),
                    Author = GetAuthorsToDisplay(templateGroup, environment),
                    Type = GetTypesToDisplay(templateGroup, environment),
                };
                templateGroupsForDisplay.Add(groupDisplayInfo);
            }

            return templateGroupsForDisplay;
        }

        /// <summary>
        /// Displays the template tags.
        /// </summary>
        internal static string GetClassificationsToDisplay(IEnumerable<ITemplateInfo> templateGroup, IEnvironment environment)
        {
            var groupedTemplates = GetAuthorBasedGroups(templateGroup);

            List<string> classificationGroups = new();
            foreach (var templates in groupedTemplates)
            {
                classificationGroups.Add(
                    string.Join(
                        "/",
                        templates
                            .SelectMany(template => template.Classifications)
                            .Where(classification => !string.IsNullOrWhiteSpace(classification))
                            .Distinct(StringComparer.OrdinalIgnoreCase)));
            }
            return string.Join(environment.NewLine, classificationGroups);
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
        /// <param name="environment"><see cref="IEnvironment"/> settings to use.</param>
        /// <returns></returns>
        private static IReadOnlyList<TemplateGroupTableRow> GetTemplateGroupsForListDisplay(
            IEnumerable<TemplateGroup> templateGroupList,
            string? language,
            string? defaultLanguage,
            IEnvironment environment)
        {
            List<TemplateGroupTableRow> templateGroupsForDisplay = new();
            foreach (TemplateGroup templateGroup in templateGroupList)
            {
                ITemplateInfo highestPrecedenceTemplate = templateGroup.Templates.OrderByDescending(x => x.Precedence).First();
                TemplateGroupTableRow groupDisplayInfo = new()
                {
                    Name = highestPrecedenceTemplate.Name,
                    ShortNames = string.Join(",", templateGroup.ShortNames),
                    Languages = GetLanguagesToDisplay(templateGroup.Templates, language, defaultLanguage, environment),
                    Classifications = GetClassificationsToDisplay(templateGroup.Templates, environment),
                    Author = GetAuthorsToDisplay(templateGroup.Templates, environment),
                    Type = GetTypesToDisplay(templateGroup.Templates, environment),
                };
                templateGroupsForDisplay.Add(groupDisplayInfo);
            }
            return templateGroupsForDisplay;
        }

        private static void DisplayTemplateList(
            IReadOnlyCollection<TemplateGroupTableRow> groupsForDisplay,
            TabularOutputSettings tabularOutputSettings,
            IReporter reporter)
        {
            TabularOutput<TemplateGroupTableRow> formatter =
                TabularOutput
                    .For(
                        tabularOutputSettings,
                        groupsForDisplay)
                    .DefineColumn(t => t.Name, out object? nameColumn, LocalizableStrings.ColumnNameTemplateName, shrinkIfNeeded: true, minWidth: 15, showAlways: true)
                    .DefineColumn(t => t.ShortNames, LocalizableStrings.ColumnNameShortName, showAlways: true)
                    .DefineColumn(t => t.Languages, out object? languageColumn, LocalizableStrings.ColumnNameLanguage, TabularOutputSettings.ColumnNames.Language, defaultColumn: true)
                    .DefineColumn(t => t.Type, LocalizableStrings.ColumnNameType, TabularOutputSettings.ColumnNames.Type, defaultColumn: false)
                    .DefineColumn(t => t.Author, LocalizableStrings.ColumnNameAuthor, TabularOutputSettings.ColumnNames.Author, defaultColumn: false, shrinkIfNeeded: true, minWidth: 10)
                    .DefineColumn(t => t.Classifications, out object? tagsColumn, LocalizableStrings.ColumnNameTags, TabularOutputSettings.ColumnNames.Tags, defaultColumn: true)
                    .OrderBy(nameColumn, StringComparer.OrdinalIgnoreCase);
            reporter.WriteLine(formatter.Layout());
        }

        private static IOrderedEnumerable<IGrouping<string, ITemplateInfo>> GetAuthorBasedGroups(IEnumerable<ITemplateInfo> templateGroup)
        {
            return templateGroup
                .GroupBy(template => string.IsNullOrWhiteSpace(template.Author) ? string.Empty : template.Author, StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase);

        }

        private static string GetTypesToDisplay(IEnumerable<ITemplateInfo> templateGroup, IEnvironment environment)
        {
            var groupedTemplates = GetAuthorBasedGroups(templateGroup);

            List<string> typesGroups = new();
            foreach (var templates in groupedTemplates)
            {
                typesGroups.Add(
                    string.Join(
                        ",",
                        templates
                            .Select(template => template.GetTemplateType())
                            .Where(type => !string.IsNullOrWhiteSpace(type))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .OrderBy(type => type, StringComparer.OrdinalIgnoreCase)));
            }
            return string.Join(environment.NewLine, typesGroups);
        }
    }
}
