// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplateFiltering;
using Microsoft.TemplateEngine.Cli.CommandParsing;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Cli.TemplateResolution
{
    internal static class TemplateResolver
    {
        internal const string DefaultLanguageMatchParameterName = "DefaultLanguage";

        internal static void ParseTemplateArgs(ITemplateInfo templateInfo, IHostSpecificDataLoader hostDataLoader, INewCommandInput commandInput)
        {
            HostSpecificTemplateData hostData = hostDataLoader.ReadHostSpecificTemplateData(templateInfo);
            commandInput.ReparseForTemplate(templateInfo, hostData);
        }

        internal static bool AreAllTemplatesSameGroupIdentity(IEnumerable<ITemplateMatchInfo> templateList)
        {
            if (!templateList.Any())
            {
                return false;
            }

            return templateList.AllAreTheSame((x) => x.Info.GroupIdentity, StringComparer.OrdinalIgnoreCase);
        }

        // This version relies on the commandInput being in the context desired - so the most recent parse would have to have been
        // for what wants to be validated, either:
        //  - not in the context of any template
        //  - in the context of a specific template.
        internal static bool ValidateRemainingParameters(INewCommandInput commandInput, out IReadOnlyList<string> invalidParams)
        {
            List<string> badParams = new List<string>();

            if (commandInput.RemainingParameters.Any())
            {
                foreach (string flag in commandInput.RemainingParameters.Keys)
                {
                    badParams.Add(flag);
                }
            }

            invalidParams = badParams;
            return !invalidParams.Any();
        }

        // Lists all the templates, unfiltered - except the ones hidden by their host file.
        internal static IReadOnlyCollection<ITemplateMatchInfo> PerformAllTemplatesQuery(IReadOnlyList<ITemplateInfo> templateInfo, IHostSpecificDataLoader hostDataLoader)
        {
            IReadOnlyList<ITemplateInfo> filterableTemplateInfo = SetupTemplateInfoWithGroupShortNames(templateInfo);
            // once template resolution refactoring is complete we would no longer use this method, but use GetTemplatesAsync instead as overriding group names should not be needed
#pragma warning disable CS0618 // Type or member is obsolete
            IReadOnlyCollection<ITemplateMatchInfo> templates = TemplateListFilter.GetTemplateMatchInfo(
                filterableTemplateInfo,
                WellKnownSearchFilters.MatchesAtLeastOneCriteria,
                CliNameFilter(string.Empty)
            )
#pragma warning restore CS0618 // Type or member is obsolete
            .Where(x => !IsTemplateHiddenByHostFile(x.Info, hostDataLoader)).ToList();

            return templates;
        }

        internal static TemplateResolutionResult GetTemplateResolutionResult(IReadOnlyList<ITemplateInfo> templateInfo, IHostSpecificDataLoader hostDataLoader, INewCommandInput commandInput, string? defaultLanguage)
        {
            IReadOnlyCollection<ITemplateMatchInfo> coreMatchedTemplates = PerformCoreTemplateQuery(templateInfo, hostDataLoader, commandInput, defaultLanguage);
            return new TemplateResolutionResult(commandInput.Language, coreMatchedTemplates);
        }

        internal static TemplateListResolutionResult GetTemplateResolutionResultForList(IReadOnlyList<ITemplateInfo> templateInfo, IHostSpecificDataLoader hostDataLoader, INewCommandInput commandInput, string? defaultLanguage)
        {
            IReadOnlyCollection<ITemplateMatchInfo> coreMatchedTemplates = PerformCoreTemplateQueryForList(templateInfo, hostDataLoader, commandInput);
            return new TemplateListResolutionResult(coreMatchedTemplates);
        }

        internal static TemplateResolutionResult GetTemplateResolutionResultForHelp(IReadOnlyList<ITemplateInfo> templateInfo, IHostSpecificDataLoader hostDataLoader, INewCommandInput commandInput, string? defaultLanguage)
        {
            IReadOnlyCollection<ITemplateMatchInfo> coreMatchedTemplates = PerformCoreTemplateQueryForHelp(templateInfo, hostDataLoader, commandInput, defaultLanguage);
            return new TemplateResolutionResult(commandInput.Language, coreMatchedTemplates);
        }

        internal static IReadOnlyCollection<ITemplateMatchInfo> PerformCoreTemplateQueryForList(IReadOnlyList<ITemplateInfo> templateInfo, IHostSpecificDataLoader hostDataLoader, INewCommandInput commandInput)
        {
            IReadOnlyList<ITemplateInfo> filterableTemplateInfo = SetupTemplateInfoWithGroupShortNames(templateInfo);

            // for list we also try to get match on template name in classification (tags). These matches only will be used if short name and name has a mismatch.
            // filter below only sets the exact or partial match if name matches the tag. If name doesn't match the tag, no match disposition is added to collection.

            var listFilters = new List<Func<ITemplateInfo, MatchInfo?>>()
            {
                CliNameFilter(commandInput.TemplateName)
            };
            listFilters.AddRange(SupportedFilterOptions.SupportedListFilters
                                    .OfType<TemplateFilterOption>()
                                    .Select(filter => filter.TemplateMatchFilter(commandInput)));
            // once template resolution refactoring is complete we would no longer use this method, but use GetTemplatesAsync instead as overriding group names should not be needed
#pragma warning disable CS0618 // Type or member is obsolete
            IReadOnlyList<ITemplateMatchInfo> coreMatchedTemplates = TemplateListFilter.GetTemplateMatchInfo(
                filterableTemplateInfo,
                WellKnownSearchFilters.MatchesAtLeastOneCriteria,
                listFilters.ToArray()
            )
#pragma warning restore CS0618 // Type or member is obsolete

            .Where(x => !IsTemplateHiddenByHostFile(x.Info, hostDataLoader)).ToList();

            AddParameterMatchingToTemplates(coreMatchedTemplates, hostDataLoader, commandInput);
            return coreMatchedTemplates;
        }

        internal static IReadOnlyCollection<ITemplateMatchInfo> PerformCoreTemplateQueryForHelp(IReadOnlyList<ITemplateInfo> templateInfo, IHostSpecificDataLoader hostDataLoader, INewCommandInput commandInput, string? defaultLanguage)
        {
            IReadOnlyList<ITemplateInfo> filterableTemplateInfo = SetupTemplateInfoWithGroupShortNames(templateInfo);
            // once template resolution refactoring is complete we would no longer use this method, but use GetTemplatesAsync instead as overriding group names should not be needed
#pragma warning disable CS0618 // Type or member is obsolete
            IReadOnlyList<ITemplateMatchInfo> coreMatchedTemplates = TemplateListFilter.GetTemplateMatchInfo(
                filterableTemplateInfo,
                WellKnownSearchFilters.MatchesAllCriteria,
                CliExactShortNameFilter(commandInput.TemplateName),
                WellKnownSearchFilters.LanguageFilter(commandInput.Language),
                WellKnownSearchFilters.TypeFilter(commandInput.TypeFilter),
                WellKnownSearchFilters.BaselineFilter(commandInput.BaselineName),
                CliDefaultLanguageFilter(defaultLanguage)
            )
#pragma warning restore CS0618 // Type or member is obsolete
            .Where(x => !IsTemplateHiddenByHostFile(x.Info, hostDataLoader)).ToList();
            AddParameterMatchingToTemplates(coreMatchedTemplates, hostDataLoader, commandInput);
            return coreMatchedTemplates;
        }

        /// <summary>
        /// Performs filtering of provided template list for --search option. Filters applied: template name filter, --search option filters, template parameters filter.
        /// Only templates that exactly match the filters are returned.
        /// </summary>
        /// <param name="templateInfo">the list of templates to be filtered.</param>
        /// <param name="hostDataLoader">data of the host.</param>
        /// <param name="commandInput">new command data used in CLI.</param>
        /// <returns>filtered list of templates.</returns>
        internal static IReadOnlyCollection<ITemplateMatchInfo> PerformCoreTemplateQueryForSearch(IEnumerable<ITemplateInfo> templateInfo, IHostSpecificDataLoader hostDataLoader, INewCommandInput commandInput)
        {
            IReadOnlyList<ITemplateInfo> filterableTemplateInfo = SetupTemplateInfoWithGroupShortNames(templateInfo.ToList());
            List<Func<ITemplateInfo, MatchInfo?>> searchFilters = new List<Func<ITemplateInfo, MatchInfo?>>()
            {
                CliNameFilter(commandInput.TemplateName),
            };
            searchFilters.AddRange(SupportedFilterOptions.SupportedSearchFilters
                                    .OfType<TemplateFilterOption>()
                                    .Select(filter => filter.TemplateMatchFilter(commandInput)));
            // once template resolution refactoring is complete we would no longer use this method, but use GetTemplatesAsync instead as overriding group names should not be needed
#pragma warning disable CS0618 // Type or member is obsolete
            IReadOnlyCollection<ITemplateMatchInfo> matchedTemplates = TemplateListFilter.GetTemplateMatchInfo(filterableTemplateInfo, WellKnownSearchFilters.MatchesAllCriteria, searchFilters.ToArray());
#pragma warning restore CS0618 // Type or member is obsolete

            AddParameterMatchingToTemplates(matchedTemplates, hostDataLoader, commandInput);
            return matchedTemplates.Where(t => t.IsInvokableMatch()).ToList();
        }

        /// <summary>
        /// Performs the filtering of installed templates for template instantiated.
        /// Filters applied: template name filter; language, type, classification and baseline filters. Only templates that match the filters are returned, no partial matches allowed.
        /// In case any templates in match above are matching name or short name exactly, only they are returned.
        /// The matches for default language and template specific parameters are added to the result.
        /// </summary>
        /// <param name="templateInfo">the list of templates to be filtered.</param>
        /// <param name="hostDataLoader">data of the host.</param>
        /// <param name="commandInput">new command data used in CLI.</param>
        /// <param name="defaultLanguage"></param>
        /// <returns>the collection of the templates with their match dispositions (<seealso cref="ITemplateMatchInfo"/>). The templates that do not match are not added to the collection.</returns>
        internal static IReadOnlyCollection<ITemplateMatchInfo> PerformCoreTemplateQuery(IReadOnlyList<ITemplateInfo> templateInfo, IHostSpecificDataLoader hostDataLoader, INewCommandInput commandInput, string? defaultLanguage)
        {
            IReadOnlyList<ITemplateInfo> filterableTemplateInfo = SetupTemplateInfoWithGroupShortNames(templateInfo);

#pragma warning disable CS0618 // Type or member is obsolete
            IReadOnlyCollection<ITemplateMatchInfo> templates = TemplateListFilter.GetTemplateMatchInfo(
                filterableTemplateInfo,
                WellKnownSearchFilters.MatchesAllCriteria,
                CliExactShortNameFilter(commandInput.TemplateName),
                WellKnownSearchFilters.LanguageFilter(commandInput.Language),
                WellKnownSearchFilters.TypeFilter(commandInput.TypeFilter),
                WellKnownSearchFilters.BaselineFilter(commandInput.BaselineName),
                CliDefaultLanguageFilter(defaultLanguage)
            )
#pragma warning restore CS0618 // Type or member is obsolete
            .Where(x => !IsTemplateHiddenByHostFile(x.Info, hostDataLoader)).ToList();

            //add specific template parameters matches to the list
            AddParameterMatchingToTemplates(templates, hostDataLoader, commandInput);
            return templates;
        }

        /// <summary>
        /// <see cref="TemplateListFilter.GetTemplateMatchInfo"/> filter for default language. The disposition is set only for <see cref="MatchKind.Exact"/> ; otherwise ignored - the disposition for <see cref="MatchKind.Mismatch"/> is not set.
        /// </summary>
        /// <param name="defaultLanguage"></param>
        /// <returns></returns>
        private static Func<ITemplateInfo, MatchInfo?> CliDefaultLanguageFilter(string? defaultLanguage)
        {
            return (template) =>
            {
                if (string.IsNullOrWhiteSpace(defaultLanguage))
                {
                    return null;
                }
                string? templateLanguage = template.GetLanguage();
                // only add default language disposition when there is a language specified for the template.
                if (string.IsNullOrWhiteSpace(templateLanguage))
                {
                    return null;
                }

                if (templateLanguage.Equals(defaultLanguage, StringComparison.OrdinalIgnoreCase))
                {
                    return new MatchInfo(DefaultLanguageMatchParameterName, defaultLanguage, MatchKind.Exact);
                }
                return null;
            };
        }

        /// <summary>
        /// Adds match dispositions to the templates based on matches between the input parameters and the template specific parameters.
        /// </summary>
        /// <param name="templatesToFilter">the templates to match.</param>
        /// <param name="hostDataLoader"></param>
        /// <param name="commandInput">the command input used in CLI.</param>
        private static void AddParameterMatchingToTemplates(IReadOnlyCollection<ITemplateMatchInfo> templatesToFilter, IHostSpecificDataLoader hostDataLoader, INewCommandInput commandInput)
        {
            foreach (ITemplateMatchInfo template in templatesToFilter)
            {
                try
                {
                    ParseTemplateArgs(template.Info, hostDataLoader, commandInput);
                    Dictionary<string, ITemplateParameter> templateParameters =
                        template.Info.Parameters.ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);

                    // parameters are already parsed. But choice values aren't checked
                    foreach (KeyValuePair<string, string> matchedParamInfo in commandInput.InputTemplateParams)
                    {
                        string paramName = matchedParamInfo.Key;
                        string paramValue = matchedParamInfo.Value;
                        MatchKind matchKind;

                        if (templateParameters.TryGetValue(paramName, out ITemplateParameter? paramDetails))
                        {
                            if (paramDetails.IsChoice() && paramDetails.Choices != null)
                            {
                                if (string.IsNullOrEmpty(paramValue)
                                && !string.IsNullOrEmpty(paramDetails.DefaultIfOptionWithoutValue))
                                {
                                    // The user provided the parameter switch on the command line, without a value.
                                    // In this case, the DefaultIfOptionWithoutValue is the effective value.
                                    paramValue = paramDetails.DefaultIfOptionWithoutValue;
                                }

                                // key is the value user should provide, value is description
                                if (string.IsNullOrEmpty(paramValue))
                                {
                                    matchKind = MatchKind.InvalidValue;
                                }
                                else if (paramDetails.Choices.ContainsKey(paramValue))
                                {
                                    matchKind = MatchKind.Exact;
                                }
                                //https://github.com/dotnet/templating/issues/2494
                                //after tab completion is implemented we no longer will be using this match kind - only exact matches will be allowed
                                else
                                {
                                    int startsWithCount = paramDetails.Choices.Count(x => x.Key.StartsWith(paramValue, StringComparison.OrdinalIgnoreCase));
                                    if (startsWithCount == 1)
                                    {
#pragma warning disable CS0618 // Type or member is obsolete
                                        matchKind = MatchKind.SingleStartsWith;
#pragma warning restore CS0618 // Type or member is obsolete
                                    }
                                    else if (startsWithCount > 1)
                                    {
#pragma warning disable CS0618 // Type or member is obsolete
                                        matchKind = MatchKind.AmbiguousValue;
#pragma warning restore CS0618 // Type or member is obsolete
                                    }
                                    else
                                    {
                                        matchKind = MatchKind.InvalidValue;
                                    }
                                }
                            }
                            else // other parameter
                            {
                                matchKind = MatchKind.Exact;
                            }
                        }
                        else
                        {
                            matchKind = MatchKind.InvalidName;
                        }
                        template.AddMatchDisposition(new ParameterMatchInfo(paramName, paramValue, matchKind, commandInput.TemplateParamInputFormat(paramName)));
                    }

                    foreach (string unmatchedParamName in commandInput.RemainingParameters.Keys.Where(x => !x.Contains(':'))) // filter debugging params
                    {
                        if (commandInput.TryGetCanonicalNameForVariant(unmatchedParamName, out string canonical))
                        {
                            // the name is a known template param, it must have not parsed due to an invalid value
                            // Note (scp 2017-02-27): This probably can't happen, the param parsing doesn't check the choice values.
                            template.AddMatchDisposition(new ParameterMatchInfo(canonical, null, MatchKind.InvalidName, commandInput.TemplateParamInputFormat(unmatchedParamName)));
                        }
                        else
                        {
                            // the name is not known
                            // TODO: reconsider storing the canonical in this situation. It's not really a canonical since the param is unknown.
                            template.AddMatchDisposition(new ParameterMatchInfo(unmatchedParamName, null, MatchKind.InvalidName, unmatchedParamName));
                        }
                    }
                }
                catch (CommandParserException ex)
                {
                    string shortname = template.Info.ShortNameList.Any() ? template.Info.ShortNameList[0] : $"'{template.Info.Name}'";
                    // if we do actually throw, add a non-match
                    Reporter.Error.WriteLine(
                        string.Format(
                            LocalizableStrings.TemplateResolver_Warning_FailedToReparseTemplate,
                            $"{template.Info.Identity} ({shortname})"));
                    Reporter.Verbose.WriteLine(string.Format(LocalizableStrings.Generic_Details, ex.ToString()));
                }
            }
        }

        /// <summary>
        /// The method makes all short names in template group available for matching for given template (using <see cref="TemplateInfoWithGroupShortNames"/> <see cref="ITemplateInfo"/> implementation).
        /// TODO: double-check if that is needed. If needed that should be replaced by TemplateGroup logic as:
        /// - group templates by template groups
        /// - match templates through filters by template group including this logic of sharing short names
        /// - create resolution result afterwards.
        /// </summary>
        /// <param name="templateList">the list of <see cref="ITemplateInfo"/> to process.</param>
        /// <returns></returns>
        private static IReadOnlyList<ITemplateInfo> SetupTemplateInfoWithGroupShortNames(IReadOnlyList<ITemplateInfo> templateList)
        {
            Dictionary<string, HashSet<string>> shortNamesByGroup = new Dictionary<string, HashSet<string>>();

            // get the short names lists for the groups
            foreach (ITemplateInfo template in templateList)
            {
                string effectiveGroupIdentity = !string.IsNullOrEmpty(template.GroupIdentity)
                    ? template.GroupIdentity
                    : template.Identity;

                if (!shortNamesByGroup.TryGetValue(effectiveGroupIdentity, out HashSet<string>? shortNames))
                {
                    shortNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    shortNamesByGroup[effectiveGroupIdentity] = shortNames;
                }
                shortNames.UnionWith(template.ShortNameList);
            }

            // create the TemplateInfoWithGroupShortNames with the group short names
            List<TemplateInfoWithGroupShortNames> templateListWithGroupNames = new List<TemplateInfoWithGroupShortNames>();

            foreach (ITemplateInfo template in templateList)
            {
                string effectiveGroupIdentity = !string.IsNullOrEmpty(template.GroupIdentity)
                    ? template.GroupIdentity
                    : template.Identity;

                templateListWithGroupNames.Add(new TemplateInfoWithGroupShortNames(template, shortNamesByGroup[effectiveGroupIdentity]));
            }

            return templateListWithGroupNames;
        }

        /// <summary>
        /// Filter for <see cref="TemplateListFilter.GetTemplateMatchInfo"></see>.
        /// Filters <see cref="ITemplateInfo"/> by name. Requires <see cref="TemplateInfoWithGroupShortNames"/> implementation.
        /// The fields to be compared are <see cref="ITemplateInfo.Name"/> and <see cref="TemplateInfoWithGroupShortNames.GroupShortNameList"/>.
        /// Unlike <see cref="WellKnownSearchFilters.NameFilter(string)"/> the filter also matches other template group short names the template belongs to.
        /// </summary>
        /// <param name="name">the name to match with template name or short name.</param>
        /// <returns></returns>
        private static Func<ITemplateInfo, MatchInfo?> CliNameFilter(string name)
        {
            return (template) =>
            {
                var groupAwareTemplate = template as TemplateInfoWithGroupShortNames ?? throw new ArgumentException("CliNameFilter filter supports only TemplateInfoWithGroupShortNames templates");

                if (string.IsNullOrEmpty(name))
                {
                    return new MatchInfo(MatchInfo.BuiltIn.Name, name, MatchKind.Partial);
                }

                int nameIndex = groupAwareTemplate.Name.IndexOf(name, StringComparison.OrdinalIgnoreCase);

                if (nameIndex == 0 && groupAwareTemplate.Name.Length == name.Length)
                {
                    return new MatchInfo(MatchInfo.BuiltIn.Name, name, MatchKind.Exact);
                }

                bool hasShortNamePartialMatch = false;

                foreach (string shortName in groupAwareTemplate.GroupShortNameList)
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

        /// <summary>
        /// Filter for <see cref="TemplateListFilter.GetTemplateMatchInfo"></see>.
        /// Filters <see cref="ITemplateInfo"/> by short name. Requires <see cref="TemplateInfoWithGroupShortNames"/> implementation.
        /// The fields to be compared are <see cref="TemplateInfoWithGroupShortNames.GroupShortNameList"/> and they should exactly match user input.
        /// Unlike <see cref="WellKnownSearchFilters.NameFilter(string)"/> the filter also matches other template group short names the template belongs to.
        /// </summary>
        /// <param name="name">the name to match with short name.</param>
        /// <returns></returns>
        private static Func<ITemplateInfo, MatchInfo?> CliExactShortNameFilter(string name)
        {
            return (template) =>
            {
                var groupAwareTemplate = template as TemplateInfoWithGroupShortNames ?? throw new ArgumentException("CliNameFilter filter supports only TemplateInfoWithGroupShortNames templates");

                if (string.IsNullOrEmpty(name))
                {
                    return new MatchInfo(MatchInfo.BuiltIn.ShortName, name, MatchKind.Mismatch);
                }
                foreach (string shortName in groupAwareTemplate.GroupShortNameList)
                {
                    if (shortName.Equals(name, StringComparison.OrdinalIgnoreCase))
                    {
                        return new MatchInfo(MatchInfo.BuiltIn.ShortName, name, MatchKind.Exact);
                    }
                }
                return new MatchInfo(MatchInfo.BuiltIn.ShortName, name, MatchKind.Mismatch);
            };
        }

        private static bool IsTemplateHiddenByHostFile(ITemplateInfo templateInfo, IHostSpecificDataLoader hostDataLoader)
        {
            HostSpecificTemplateData hostData = hostDataLoader.ReadHostSpecificTemplateData(templateInfo);
            return hostData.IsHidden;
        }
    }
}
