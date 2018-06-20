// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.CommandParsing;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Template;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Cli
{
    public static class TemplateListResolver
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

        public static bool AreAllTemplatesSameLanguage(IEnumerable<ITemplateMatchInfo> templateList)
        {
            if (!templateList.Any())
            {
                return false;
            }

            HashSet<string> languagesFound = new HashSet<string>();
            foreach (ITemplateMatchInfo template in templateList)
            {
                string language;

                if (template.Info.Tags != null && template.Info.Tags.TryGetValue("language", out ICacheTag languageTag))
                {
                    language = languageTag.ChoicesAndDescriptions.Keys.FirstOrDefault();
                }
                else
                {
                    language = string.Empty;
                }

                //add & check
                languagesFound.Add(language);

                if (languagesFound.Count > 1)
                {
                    return false;
                }
            }

            return true;
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
        public static ITemplateMatchInfo FindHighestPrecedenceTemplateIfAllSameGroupIdentity(IReadOnlyList<ITemplateMatchInfo> templateList)
        {
            return FindHighestPrecedenceTemplateIfAllSameGroupIdentity(templateList, out bool throwawayAmbiguousResult);
        }

        public static ITemplateMatchInfo FindHighestPrecedenceTemplateIfAllSameGroupIdentity(IReadOnlyList<ITemplateMatchInfo> templateList, out bool ambiguousResult)
        {
            ambiguousResult = false;

            if (!AreAllTemplatesSameGroupIdentity(templateList))
            {
                return null;
            }

            ITemplateMatchInfo highestPrecedenceTemplate = null;

            foreach (ITemplateMatchInfo template in templateList)
            {
                if (highestPrecedenceTemplate == null)
                {
                    highestPrecedenceTemplate = template;
                }
                else if (template.Info.Precedence > highestPrecedenceTemplate.Info.Precedence)
                {
                    highestPrecedenceTemplate = template;
                }
                else if (template.Info.Precedence == highestPrecedenceTemplate.Info.Precedence)
                {
                    ambiguousResult = true;
                    highestPrecedenceTemplate = null;
                    break;
                }
            }

            return highestPrecedenceTemplate;
        }

        // Lists all the templates, unfiltered - except the ones hidden by their host file.
        public static IReadOnlyCollection<ITemplateMatchInfo> PerformAllTemplatesQuery(IReadOnlyList<ITemplateInfo> templateInfo, IHostSpecificDataLoader hostDataLoader)
        {
            IReadOnlyList<FilterableTemplateInfo> filterableTemplateInfo = SetupFilterableTemplateInfoFromTemplateInfo(templateInfo);

            IReadOnlyCollection<ITemplateMatchInfo> templates = TemplateListFilter.GetTemplateMatchInfo
            (
                filterableTemplateInfo,
                TemplateListFilter.PartialMatchFilter,
                WellKnownSearchFilters.NameFilter(string.Empty)
            )
            .Where(x => !IsTemplateHiddenByHostFile(x.Info, hostDataLoader)).ToList();

            return templates;
        }

        // Lists all the templates, filtered only by the context (item, project, etc) - and the host file.
        public static IReadOnlyCollection<ITemplateMatchInfo> PerformAllTemplatesInContextQuery(IReadOnlyList<ITemplateInfo> templateInfo, IHostSpecificDataLoader hostDataLoader, string context)
        {
            IReadOnlyList<FilterableTemplateInfo> filterableTemplateInfo = SetupFilterableTemplateInfoFromTemplateInfo(templateInfo);

            // If there is a context match, it must be exact. Other dispositions are irrelevant.
            Func<ITemplateMatchInfo, bool> contextFilter = x => x.MatchDisposition.All(d => d.Location != MatchLocation.Context || d.Kind == MatchKind.Exact);
            IReadOnlyCollection<ITemplateMatchInfo> templates = TemplateListFilter.GetTemplateMatchInfo
            (
                filterableTemplateInfo,
                contextFilter,
                WellKnownSearchFilters.ContextFilter(context),
                WellKnownSearchFilters.NameFilter(string.Empty)
            )
            .Where(x => !IsTemplateHiddenByHostFile(x.Info, hostDataLoader)).ToList();

            return templates;
        }

        public static TemplateListResolutionResult GetTemplateResolutionResult(IReadOnlyList<ITemplateInfo> templateInfo, IHostSpecificDataLoader hostDataLoader, INewCommandInput commandInput, string defaultLanguage)
        {
            IReadOnlyCollection<ITemplateMatchInfo> coreMatchedTemplates = PerformCoreTemplateQuery(templateInfo, hostDataLoader, commandInput, defaultLanguage);
            IReadOnlyCollection<ITemplateMatchInfo> allTemplatesInContext = PerformAllTemplatesInContextQuery(templateInfo, hostDataLoader, commandInput.TypeFilter);
            return new TemplateListResolutionResult(commandInput.TemplateName, commandInput.Language, coreMatchedTemplates, allTemplatesInContext);
        }

        // Query for template matches, filtered by everything available: name, language, context, parameters, and the host file.
        public static IReadOnlyCollection<ITemplateMatchInfo> PerformCoreTemplateQuery(IReadOnlyList<ITemplateInfo> templateInfo, IHostSpecificDataLoader hostDataLoader, INewCommandInput commandInput, string defaultLanguage)
        {
            IReadOnlyList<FilterableTemplateInfo> filterableTemplateInfo = SetupFilterableTemplateInfoFromTemplateInfo(templateInfo);

            IReadOnlyCollection<ITemplateMatchInfo> templates = TemplateListFilter.GetTemplateMatchInfo
            (
                filterableTemplateInfo,
                TemplateListFilter.PartialMatchFilter,
                WellKnownSearchFilters.NameFilter(commandInput.TemplateName),
                WellKnownSearchFilters.ClassificationsFilter(commandInput.TemplateName),
                WellKnownSearchFilters.LanguageFilter(commandInput.Language),
                WellKnownSearchFilters.ContextFilter(commandInput.TypeFilter),
                WellKnownSearchFilters.BaselineFilter(commandInput.BaselineName)
            )
            .Where(x => !IsTemplateHiddenByHostFile(x.Info, hostDataLoader)).ToList();

            IReadOnlyList<ITemplateMatchInfo> coreMatchedTemplates = templates.Where(x => x.IsMatch).ToList();

            if (coreMatchedTemplates.Count == 0)
            {
                // No exact matches, take the partial matches and be done.
                coreMatchedTemplates = templates.Where(x => x.IsPartialMatch).ToList();
            }
            else
            {
                IReadOnlyList<ITemplateMatchInfo> matchesWithExactDispositionsInNameFields = coreMatchedTemplates.Where(x => x.MatchDisposition.Any(y => NameFields.Contains(y.Location) && y.Kind == MatchKind.Exact)).ToList();

                if (matchesWithExactDispositionsInNameFields.Count > 0)
                {
                    // Start with the exact name matches, if there are any.
                    coreMatchedTemplates = matchesWithExactDispositionsInNameFields;
                }
            }

            if (string.IsNullOrEmpty(commandInput.Language) && !string.IsNullOrEmpty(defaultLanguage))
            {
                // default language matching only makes sense if the user didn't specify a language.
                AddDefaultLanguageMatchingToTemplates(coreMatchedTemplates, defaultLanguage);
            }

            AddParameterMatchingToTemplates(coreMatchedTemplates, hostDataLoader, commandInput);

            return coreMatchedTemplates;
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

        private static void AddDefaultLanguageMatchingToTemplates(IReadOnlyList<ITemplateMatchInfo> listToFilter, string language)
        {
            if (string.IsNullOrEmpty(language))
            {
                return;
            }

            foreach (ITemplateMatchInfo template in listToFilter)
            {
                if (template.Info.Tags != null)
                {
                    if (template.Info.Tags.TryGetValue("language", out ICacheTag languageTag))
                    {
                        MatchKind matchKind;

                        if (languageTag.ChoicesAndDescriptions.ContainsKey(language))
                        {
                            matchKind = MatchKind.Exact;
                        }
                        else
                        {
                            matchKind = MatchKind.Mismatch;
                        }

                        // only add default language disposition when there is a language specified for the template.
                        template.AddDisposition(new MatchInfo
                        {
                            Location = MatchLocation.DefaultLanguage,
                            Kind = matchKind
                        });
                    }
                }
            }
        }

        // adds dispositions to the templates based on matches between the input parameters & the template parameters.
        private static void AddParameterMatchingToTemplates(IReadOnlyList<ITemplateMatchInfo> templatesToFilter, IHostSpecificDataLoader hostDataLoader, INewCommandInput commandInput)
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
                                template.AddDisposition(new MatchInfo { Location = MatchLocation.OtherParameter, Kind = MatchKind.InvalidParameterValue, ChoiceIfLocationIsOtherChoice = paramName, ParameterValue = paramValue });
                            }
                            else if (paramDetails.ChoicesAndDescriptions.ContainsKey(paramValue))
                            {
                                template.AddDisposition(new MatchInfo { Location = MatchLocation.OtherParameter, Kind = MatchKind.Exact, ChoiceIfLocationIsOtherChoice = paramName, ParameterValue = paramValue });
                            }
                            else
                            {
                                int startsWithCount = paramDetails.ChoicesAndDescriptions.Count(x => x.Key.StartsWith(paramValue, StringComparison.OrdinalIgnoreCase));
                                if (startsWithCount == 1)
                                {
                                    template.AddDisposition(new MatchInfo { Location = MatchLocation.OtherParameter, Kind = MatchKind.SingleStartsWith, ChoiceIfLocationIsOtherChoice = paramName, ParameterValue = paramValue });
                                }
                                else if (startsWithCount > 1)
                                {
                                    template.AddDisposition(new MatchInfo { Location = MatchLocation.OtherParameter, Kind = MatchKind.AmbiguousParameterValue, ChoiceIfLocationIsOtherChoice = paramName, ParameterValue = paramValue });
                                }
                                else
                                {
                                    template.AddDisposition(new MatchInfo { Location = MatchLocation.OtherParameter, Kind = MatchKind.InvalidParameterValue, ChoiceIfLocationIsOtherChoice = paramName, ParameterValue = paramValue });
                                }
                            }
                        }
                        else if (template.Info.CacheParameters.ContainsKey(paramName))
                        {
                            template.AddDisposition(new MatchInfo { Location = MatchLocation.OtherParameter, Kind = MatchKind.Exact, ChoiceIfLocationIsOtherChoice = paramName, ParameterValue = paramValue });
                        }
                        else
                        {
                            template.AddDisposition(new MatchInfo { Location = MatchLocation.OtherParameter, Kind = MatchKind.InvalidParameterValue, ChoiceIfLocationIsOtherChoice = paramName, ParameterValue = paramValue });
                        }
                    }

                    foreach (string unmatchedParamName in commandInput.RemainingParameters.Keys.Where(x => !x.Contains(':')))   // filter debugging params
                    {
                        if (commandInput.TryGetCanonicalNameForVariant(unmatchedParamName, out string canonical))
                        {
                            // the name is a known template param, it must have not parsed due to an invalid value
                            // Note (scp 2017-02-27): This probably can't happen, the param parsing doesn't check the choice values.
                            template.AddDisposition(new MatchInfo { Location = MatchLocation.OtherParameter, Kind = MatchKind.InvalidParameterValue, ChoiceIfLocationIsOtherChoice = unmatchedParamName });
                        }
                        else
                        {
                            // the name is not known
                            // TODO: reconsider storing the canonical in this situation. It's not really a canonical since the param is unknown.
                            template.AddDisposition(new MatchInfo { Location = MatchLocation.OtherParameter, Kind = MatchKind.InvalidParameterName, ChoiceIfLocationIsOtherChoice = unmatchedParamName });
                        }
                    }
                }
                catch (CommandParserException ex)
                {
                    // if we do actually throw, add a non-match
                    template.AddDisposition(new MatchInfo { Location = MatchLocation.Unspecified, Kind = MatchKind.Unspecified, AdditionalInformation = ex.Message });
                }
                catch (Exception ex)
                {
                    template.AddDisposition(new MatchInfo { Location = MatchLocation.Unspecified, Kind = MatchKind.Unspecified, AdditionalInformation = $"Unexpected error: {ex.Message}" });
                }
            }
        }
    }
}
