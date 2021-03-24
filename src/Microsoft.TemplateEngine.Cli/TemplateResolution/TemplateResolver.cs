// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.CommandParsing;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Template;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Cli.TemplateResolution
{
    public static class TemplateResolver
    {
        private static readonly IReadOnlyCollection<MatchLocation> NameFields = new HashSet<MatchLocation>
        {
            MatchLocation.Name,
            MatchLocation.ShortName
        };

        public static void ParseTemplateArgs(ITemplateInfo templateInfo, IHostSpecificDataLoader hostDataLoader, INewCommandInput commandInput)
        {
            HostSpecificTemplateData hostData = hostDataLoader.ReadHostSpecificTemplateData(templateInfo);
            commandInput.ReparseForTemplate(templateInfo, hostData);
        }

        public static bool AreAllTemplatesSameGroupIdentity(IEnumerable<ITemplateMatchInfo> templateList)
        {
            if (!templateList.Any())
            {
                return false;
            }

            return templateList.AllAreTheSame((x) => x.Info.GroupIdentity, StringComparer.OrdinalIgnoreCase);
        }

        private static bool IsTemplateHiddenByHostFile(ITemplateInfo templateInfo, IHostSpecificDataLoader hostDataLoader)
        {
            HostSpecificTemplateData hostData = hostDataLoader.ReadHostSpecificTemplateData(templateInfo);
            return hostData.IsHidden;
        }

        // This version relies on the commandInput being in the context desired - so the most recent parse would have to have been
        // for what wants to be validated, either:
        //  - not in the context of any template
        //  - in the context of a specific template.
        public static bool ValidateRemainingParameters(INewCommandInput commandInput, out IReadOnlyList<string> invalidParams)
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

        // This version is preferred, its clear which template the results are in the context of.
        public static bool ValidateRemainingParameters(ITemplateMatchInfo template, out IReadOnlyList<string> invalidParams)
        {
            invalidParams = template.GetInvalidParameterNames();

            return !invalidParams.Any();
        }

        // Lists all the templates, unfiltered - except the ones hidden by their host file.
        public static IReadOnlyCollection<ITemplateMatchInfo> PerformAllTemplatesQuery(IReadOnlyList<ITemplateInfo> templateInfo, IHostSpecificDataLoader hostDataLoader)
        {
            IReadOnlyList<FilterableTemplateInfo> filterableTemplateInfo = SetupFilterableTemplateInfoFromTemplateInfo(templateInfo);

            IReadOnlyCollection<ITemplateMatchInfo> templates = TemplateListFilter.GetTemplateMatchInfo(
                filterableTemplateInfo,
                TemplateListFilter.PartialMatchFilter,
                WellKnownSearchFilters.NameFilter(string.Empty)
            )
            .Where(x => !IsTemplateHiddenByHostFile(x.Info, hostDataLoader)).ToList();

            return templates;
        }

        public static TemplateResolutionResult GetTemplateResolutionResult(IReadOnlyList<ITemplateInfo> templateInfo, IHostSpecificDataLoader hostDataLoader, INewCommandInput commandInput, string defaultLanguage)
        {
            IReadOnlyCollection<ITemplateMatchInfo> coreMatchedTemplates = PerformCoreTemplateQuery(templateInfo, hostDataLoader, commandInput, defaultLanguage);
            return new TemplateResolutionResult(commandInput.Language, coreMatchedTemplates);
        }

        public static TemplateListResolutionResult GetTemplateResolutionResultForListOrHelp(IReadOnlyList<ITemplateInfo> templateInfo, IHostSpecificDataLoader hostDataLoader, INewCommandInput commandInput, string defaultLanguage)
        {
            IReadOnlyCollection<ITemplateMatchInfo> coreMatchedTemplates;

            //we need different set of templates for help and list
            //for list we need to show all exact and partial names by name
            //for help if there is an exact match by shortname or name we need to show help for that exact template and also apply default language mapping in case language is not specified
            if (commandInput.IsListFlagSpecified)
            {
                coreMatchedTemplates = PerformCoreTemplateQueryForList(templateInfo, hostDataLoader, commandInput, defaultLanguage);
            }
            else
            {
                coreMatchedTemplates = PerformCoreTemplateQueryForHelp(templateInfo, hostDataLoader, commandInput, defaultLanguage);
            }
            return new TemplateListResolutionResult(coreMatchedTemplates);
        }

        public static IReadOnlyCollection<ITemplateMatchInfo> PerformCoreTemplateQueryForList(IReadOnlyList<ITemplateInfo> templateInfo, IHostSpecificDataLoader hostDataLoader, INewCommandInput commandInput, string defaultLanguage)
        {
            IReadOnlyList<FilterableTemplateInfo> filterableTemplateInfo = SetupFilterableTemplateInfoFromTemplateInfo(templateInfo);

            // for list we also try to get match on template name in classification (tags). These matches only will be used if short name and name has a mismatch.
            // filter below only sets the exact or partial match if name matches the tag. If name doesn't match the tag, no match disposition is added to collection.

            var listFilters = new List<Func<ITemplateInfo, MatchInfo?>>()
            {
                WellKnownSearchFilters.NameFilter(commandInput.TemplateName)
            };
            listFilters.AddRange(SupportedFilterOptions.SupportedListFilters
                                    .OfType<TemplateFilterOption>()
                                    .Select(filter => filter.TemplateMatchFilter(commandInput)));

            IReadOnlyList<ITemplateMatchInfo> coreMatchedTemplates = TemplateListFilter.GetTemplateMatchInfo(
                filterableTemplateInfo,
                TemplateListFilter.PartialMatchFilter,
                listFilters.ToArray()
            )
            .Where(x => !IsTemplateHiddenByHostFile(x.Info, hostDataLoader)).ToList();

            AddParameterMatchingToTemplates(coreMatchedTemplates, hostDataLoader, commandInput);
            return coreMatchedTemplates;
        }

        public static IReadOnlyCollection<ITemplateMatchInfo> PerformCoreTemplateQueryForHelp(IReadOnlyList<ITemplateInfo> templateInfo, IHostSpecificDataLoader hostDataLoader, INewCommandInput commandInput, string defaultLanguage)
        {
            IReadOnlyList<FilterableTemplateInfo> filterableTemplateInfo = SetupFilterableTemplateInfoFromTemplateInfo(templateInfo);
            IReadOnlyList<ITemplateMatchInfo> coreMatchedTemplates = TemplateListFilter.GetTemplateMatchInfo(
                filterableTemplateInfo,
                TemplateListFilter.PartialMatchFilter,
                WellKnownSearchFilters.NameFilter(commandInput.TemplateName),
                WellKnownSearchFilters.LanguageFilter(commandInput.Language),
                WellKnownSearchFilters.ContextFilter(commandInput.TypeFilter),
                WellKnownSearchFilters.BaselineFilter(commandInput.BaselineName)
            )
            .Where(x => !IsTemplateHiddenByHostFile(x.Info, hostDataLoader)).ToList();

            //for help if template name from CLI exactly matches the template name we should consider only that template
            IReadOnlyList<ITemplateMatchInfo> matchesWithExactDispositionsInNameFields = coreMatchedTemplates.Where(x => x.MatchDisposition.Any(y => NameFields.Contains(y.Location) && y.Kind == MatchKind.Exact)).ToList();
            if (matchesWithExactDispositionsInNameFields.Count > 0)
            {
                coreMatchedTemplates = matchesWithExactDispositionsInNameFields;
            }

            //for help we also need to match on default language if language was not specified as parameter
            if (string.IsNullOrEmpty(commandInput.Language) && !string.IsNullOrEmpty(defaultLanguage))
            {
                // default language matching only makes sense if the user didn't specify a language.
                AddDefaultLanguageMatchingToTemplates(coreMatchedTemplates, defaultLanguage);
            }
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
        public static IReadOnlyCollection<ITemplateMatchInfo> PerformCoreTemplateQueryForSearch(IEnumerable<ITemplateInfo> templateInfo, IHostSpecificDataLoader hostDataLoader, INewCommandInput commandInput)
        {
            IReadOnlyList<FilterableTemplateInfo> filterableTemplateInfo = SetupFilterableTemplateInfoFromTemplateInfo(templateInfo.ToList());
            List<Func<ITemplateInfo, MatchInfo?>> searchFilters = new List<Func<ITemplateInfo, MatchInfo?>>()
            {
                WellKnownSearchFilters.NameFilter(commandInput.TemplateName),
            };
            searchFilters.AddRange(SupportedFilterOptions.SupportedSearchFilters
                                    .OfType<TemplateFilterOption>()
                                    .Select(filter => filter.TemplateMatchFilter(commandInput)));

            IReadOnlyCollection<ITemplateMatchInfo> matchedTemplates = TemplateListFilter.GetTemplateMatchInfo(filterableTemplateInfo, TemplateListFilter.ExactMatchFilter, searchFilters.ToArray());

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
        public static IReadOnlyCollection<ITemplateMatchInfo> PerformCoreTemplateQuery(IReadOnlyList<ITemplateInfo> templateInfo, IHostSpecificDataLoader hostDataLoader, INewCommandInput commandInput, string defaultLanguage)
        {
            IReadOnlyList<FilterableTemplateInfo> filterableTemplateInfo = SetupFilterableTemplateInfoFromTemplateInfo(templateInfo);

            IReadOnlyCollection<ITemplateMatchInfo> templates = TemplateListFilter.GetTemplateMatchInfo(
                filterableTemplateInfo,
                TemplateListFilter.ExactMatchFilter,
                WellKnownSearchFilters.NameFilter(commandInput.TemplateName),
                WellKnownSearchFilters.LanguageFilter(commandInput.Language),
                WellKnownSearchFilters.ContextFilter(commandInput.TypeFilter),
                WellKnownSearchFilters.BaselineFilter(commandInput.BaselineName)
            )
            .Where(x => !IsTemplateHiddenByHostFile(x.Info, hostDataLoader)).ToList();

            //select only the templates which do not have mismatches
            //if any template has exact match for name - use those; otherwise partial name matches are also considered when resolving templates
            IReadOnlyList<ITemplateMatchInfo> matchesWithExactDispositionsInNameFields = templates.Where(x => x.MatchDisposition.Any(y => NameFields.Contains(y.Location) && y.Kind == MatchKind.Exact)).ToList();
            if (matchesWithExactDispositionsInNameFields.Count > 0)
            {
                templates = matchesWithExactDispositionsInNameFields;
            }

            if (string.IsNullOrEmpty(commandInput.Language) && !string.IsNullOrEmpty(defaultLanguage))
            {
                // add default language matches to the list
                // default language matching only makes sense if the user didn't specify a language.
                AddDefaultLanguageMatchingToTemplates(templates, defaultLanguage);
            }

            //add specific template parameters matches to the list
            AddParameterMatchingToTemplates(templates, hostDataLoader, commandInput);

            return templates;
        }

        private static IReadOnlyList<FilterableTemplateInfo> SetupFilterableTemplateInfoFromTemplateInfo(IReadOnlyList<ITemplateInfo> templateList)
        {
            Dictionary<string, HashSet<string>> shortNamesByGroup = new Dictionary<string, HashSet<string>>();

            // get the short names lists for the groups
            foreach (ITemplateInfo template in templateList)
            {
                string effectiveGroupIdentity = !string.IsNullOrEmpty(template.GroupIdentity)
                    ? template.GroupIdentity
                    : template.Identity;

                if (!shortNamesByGroup.TryGetValue(effectiveGroupIdentity, out HashSet<string> shortNames))
                {
                    shortNames = new HashSet<string>();
                    shortNamesByGroup[effectiveGroupIdentity] = shortNames;
                }

                if (template is IShortNameList templateWithShortNameList)
                {
                    shortNames.UnionWith(templateWithShortNameList.ShortNameList);
                }
                else
                {
                    shortNames.Add(template.ShortName);
                }
            }

            // create the FilterableTemplateInfo with the group short names
            List<FilterableTemplateInfo> filterableTemplateList = new List<FilterableTemplateInfo>();

            foreach (ITemplateInfo template in templateList)
            {
                string effectiveGroupIdentity = !string.IsNullOrEmpty(template.GroupIdentity)
                    ? template.GroupIdentity
                    : template.Identity;

                FilterableTemplateInfo filterableTemplate = FilterableTemplateInfo.FromITemplateInfo(template);
                filterableTemplate.GroupShortNameList = shortNamesByGroup[effectiveGroupIdentity].ToList();
                filterableTemplateList.Add(filterableTemplate);
            }

            return filterableTemplateList;
        }

        /// <summary>
        /// Adds match dispositions to the templates based on matches between the default language and the language defined in template.
        /// </summary>
        /// <param name="listToFilter">the templates to match.</param>
        /// <param name="language">default language.</param>
        private static void AddDefaultLanguageMatchingToTemplates(IReadOnlyCollection<ITemplateMatchInfo> listToFilter, string language)
        {
            if (string.IsNullOrEmpty(language))
            {
                return;
            }

            foreach (ITemplateMatchInfo template in listToFilter)
            {
                MatchKind matchKind;

                string templateLanguage = template.Info.GetLanguage();
                // only add default language disposition when there is a language specified for the template.
                if (string.IsNullOrWhiteSpace(templateLanguage))
                {
                    continue;
                }

                if (templateLanguage.Equals(language, StringComparison.OrdinalIgnoreCase))
                {
                    matchKind = MatchKind.Exact;
                }
                else
                {
                    matchKind = MatchKind.Mismatch;
                }

                template.AddDisposition(new MatchInfo
                {
                    Location = MatchLocation.DefaultLanguage,
                    Kind = matchKind
                });
            }
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

                    // params are already parsed. But choice values aren't checked
                    foreach (KeyValuePair<string, string> matchedParamInfo in commandInput.InputTemplateParams)
                    {
                        string paramName = matchedParamInfo.Key;
                        string paramValue = matchedParamInfo.Value;

                        if (template.Info.Tags.TryGetValue(paramName, out ICacheTag paramDetails))
                        {
                            if (string.IsNullOrEmpty(paramValue)
                                && paramDetails is IAllowDefaultIfOptionWithoutValue paramDetailsWithNoValueDefault
                                && !string.IsNullOrEmpty(paramDetailsWithNoValueDefault.DefaultIfOptionWithoutValue))
                            {
                                // The user provided the param switch on the command line, without a value.
                                // In this case, the DefaultIfOptionWithoutValue is the effective value.
                                paramValue = paramDetailsWithNoValueDefault.DefaultIfOptionWithoutValue;
                            }

                            // key is the value user should provide, value is description
                            if (string.IsNullOrEmpty(paramValue))
                            {
                                template.AddDisposition(new MatchInfo
                                {
                                    Location = MatchLocation.OtherParameter,
                                    Kind = MatchKind.InvalidParameterValue,
                                    InputParameterName = paramName,
                                    ParameterValue = paramValue,
                                    InputParameterFormat = commandInput.TemplateParamInputFormat(paramName)
                                });
                            }
                            else if (paramDetails.Choices.ContainsKey(paramValue))
                            {
                                template.AddDisposition(new MatchInfo
                                {
                                    Location = MatchLocation.OtherParameter,
                                    Kind = MatchKind.Exact,
                                    InputParameterName = paramName,
                                    ParameterValue = paramValue,
                                    InputParameterFormat = commandInput.TemplateParamInputFormat(paramName)
                                });
                            }
                            else
                            {
                                int startsWithCount = paramDetails.Choices.Count(x => x.Key.StartsWith(paramValue, StringComparison.OrdinalIgnoreCase));
                                if (startsWithCount == 1)
                                {
                                    template.AddDisposition(new MatchInfo
                                    {
                                        Location = MatchLocation.OtherParameter,
                                        Kind = MatchKind.SingleStartsWith,
                                        InputParameterName = paramName,
                                        ParameterValue = paramValue,
                                        InputParameterFormat = commandInput.TemplateParamInputFormat(paramName)
                                    });
                                }
                                else if (startsWithCount > 1)
                                {
                                    template.AddDisposition(new MatchInfo
                                    {
                                        Location = MatchLocation.OtherParameter,
                                        Kind = MatchKind.AmbiguousParameterValue,
                                        InputParameterName = paramName,
                                        ParameterValue = paramValue,
                                        InputParameterFormat = commandInput.TemplateParamInputFormat(paramName)
                                    });
                                }
                                else
                                {
                                    template.AddDisposition(new MatchInfo
                                    {
                                        Location = MatchLocation.OtherParameter,
                                        Kind = MatchKind.InvalidParameterValue,
                                        InputParameterName = paramName,
                                        ParameterValue = paramValue,
                                        InputParameterFormat = commandInput.TemplateParamInputFormat(paramName)
                                    });
                                }
                            }
                        }
                        else if (template.Info.CacheParameters.ContainsKey(paramName))
                        {
                            template.AddDisposition(new MatchInfo
                            {
                                Location = MatchLocation.OtherParameter,
                                Kind = MatchKind.Exact,
                                InputParameterName = paramName,
                                ParameterValue = paramValue,
                                InputParameterFormat = commandInput.TemplateParamInputFormat(paramName)
                            });
                        }
                        else
                        {
                            template.AddDisposition(new MatchInfo
                            {
                                Location = MatchLocation.OtherParameter,
                                Kind = MatchKind.InvalidParameterName,
                                InputParameterName = paramName,
                                ParameterValue = paramValue,
                                InputParameterFormat = commandInput.TemplateParamInputFormat(paramName)
                            });
                        }
                    }

                    foreach (string unmatchedParamName in commandInput.RemainingParameters.Keys.Where(x => !x.Contains(':'))) // filter debugging params
                    {
                        if (commandInput.TryGetCanonicalNameForVariant(unmatchedParamName, out string canonical))
                        {
                            // the name is a known template param, it must have not parsed due to an invalid value
                            // Note (scp 2017-02-27): This probably can't happen, the param parsing doesn't check the choice values.
                            template.AddDisposition(new MatchInfo
                            {
                                Location = MatchLocation.OtherParameter,
                                Kind = MatchKind.InvalidParameterName,
                                InputParameterName = canonical,
                                InputParameterFormat = commandInput.TemplateParamInputFormat(unmatchedParamName)
                            });
                        }
                        else
                        {
                            // the name is not known
                            // TODO: reconsider storing the canonical in this situation. It's not really a canonical since the param is unknown.
                            template.AddDisposition(new MatchInfo
                            {
                                Location = MatchLocation.OtherParameter,
                                Kind = MatchKind.InvalidParameterName,
                                InputParameterName = unmatchedParamName,
                                InputParameterFormat = unmatchedParamName
                            });
                        }
                    }
                }
                catch (CommandParserException ex)
                {
                    // if we do actually throw, add a non-match
                    template.AddDisposition(new MatchInfo
                    {
                        Location = MatchLocation.Unspecified,
                        Kind = MatchKind.Unspecified,
                        AdditionalInformation = ex.Message
                    });
                }
                catch (Exception ex)
                {
                    template.AddDisposition(new MatchInfo
                    {
                        Location = MatchLocation.Unspecified,
                        Kind = MatchKind.Unspecified,
                        AdditionalInformation = $"Unexpected error: {ex.Message}"
                    });
                }
            }
        }
    }
}
