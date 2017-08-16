using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.CommandParsing;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Edge.Template;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Cli
{
    public static class TemplateListResolver
    {
        private static readonly IReadOnlyCollection<MatchLocation> NameFields = new HashSet<MatchLocation>
        {
            MatchLocation.Name,
            MatchLocation.ShortName,
            MatchLocation.Alias
        };

        private static void ParseTemplateArgs(ITemplateInfo templateInfo, HostSpecificDataLoader hostDataLoader, INewCommandInput commandInput)
        {
            HostSpecificTemplateData hostData = hostDataLoader.ReadHostSpecificTemplateData(templateInfo);
            commandInput.ReparseForTemplate(templateInfo, hostData);
        }

        private static bool AreAllTemplatesSameGroupIdentity(IEnumerable<IFilteredTemplateInfo> templateList)
        {
            return templateList.AllAreTheSame((x) => x.Info.GroupIdentity, StringComparer.OrdinalIgnoreCase);
        }

        private static bool IsTemplateHiddenByHostFile(ITemplateInfo templateInfo, HostSpecificDataLoader hostDataLoader)
        {
            if (hostDataLoader == null)
            {
                return false;
            }

            HostSpecificTemplateData hostData = hostDataLoader.ReadHostSpecificTemplateData(templateInfo);
            return hostData.IsHidden;
        }

        public static IFilteredTemplateInfo FindHighestPrecedenceTemplateIfAllSameGroupIdentity(IReadOnlyList<IFilteredTemplateInfo> templateList)
        {
            if (!AreAllTemplatesSameGroupIdentity(templateList))
            {
                return null;
            }

            IFilteredTemplateInfo highestPrecedenceTemplate = null;

            foreach (IFilteredTemplateInfo template in templateList)
            {
                if (highestPrecedenceTemplate == null)
                {
                    highestPrecedenceTemplate = template;
                }
                else if (template.Info.Precedence > highestPrecedenceTemplate.Info.Precedence)
                {
                    highestPrecedenceTemplate = template;
                }
            }

            return highestPrecedenceTemplate;
        }

        // Lists all the templates, unfiltered - except the ones hidden by their host file.
        public static IReadOnlyCollection<IFilteredTemplateInfo> PerformAllTemplatesQuery(IReadOnlyList<ITemplateInfo> templateInfo, HostSpecificDataLoader hostDataLoader)
        {
            IReadOnlyCollection<IFilteredTemplateInfo> templates = TemplateListFilter.FilterTemplates
            (
                templateInfo,
                false,
                WellKnownSearchFilters.NameFilter(string.Empty)
            )
            .Where(x => !IsTemplateHiddenByHostFile(x.Info, hostDataLoader)).ToList();

            return templates;
        }

        // Lists all the templates, filtered only by the context (item, project, etc) - and the host file.
        public static IReadOnlyCollection<IFilteredTemplateInfo> PerformAllTemplatesInContextQuery(IReadOnlyList<ITemplateInfo> templateInfo, HostSpecificDataLoader hostDataLoader, string context)
        {
            IReadOnlyCollection<IFilteredTemplateInfo> templates = TemplateListFilter.FilterTemplates
            (
                templateInfo,
                false,
                WellKnownSearchFilters.ContextFilter(context),
                WellKnownSearchFilters.NameFilter(string.Empty)
            )
            .Where(x => !IsTemplateHiddenByHostFile(x.Info, hostDataLoader)).ToList();

            return templates;
        }

        // Query for template matches, filtered by everything available: name, language, context, parameters, and the host file.
        public static TemplateListResolutionResult PerformCoreTemplateQuery(IReadOnlyList<ITemplateInfo> templateInfo, HostSpecificDataLoader hostDataLoader, INewCommandInput commandInput, string defaultLanguage)
        {
            IReadOnlyCollection<IFilteredTemplateInfo> templates = TemplateListFilter.FilterTemplates
            (
                templateInfo,
                false,
                WellKnownSearchFilters.NameFilter(commandInput.TemplateName),
                WellKnownSearchFilters.ClassificationsFilter(commandInput.TemplateName),
                WellKnownSearchFilters.LanguageFilter(commandInput.Language),
                WellKnownSearchFilters.ContextFilter(commandInput.TypeFilter?.ToLowerInvariant()),
                WellKnownSearchFilters.BaselineFilter(commandInput.BaselineName)
            )
            .Where(x => !IsTemplateHiddenByHostFile(x.Info, hostDataLoader)).ToList();

            IReadOnlyList<IFilteredTemplateInfo> coreMatchedTemplates = templates.Where(x => x.IsMatch).ToList();
            bool anyExactCoreMatches;

            if (coreMatchedTemplates.Count == 0)
            {
                coreMatchedTemplates = templates.Where(x => x.IsPartialMatch).ToList();
                anyExactCoreMatches = false;
            }
            else
            {
                anyExactCoreMatches = true;
                IReadOnlyList<IFilteredTemplateInfo> matchesWithExactDispositionsInNameFields = coreMatchedTemplates.Where(x => x.MatchDisposition.Any(y => NameFields.Contains(y.Location) && y.Kind == MatchKind.Exact)).ToList();

                if (matchesWithExactDispositionsInNameFields.Count > 0)
                {
                    coreMatchedTemplates = matchesWithExactDispositionsInNameFields;
                }
            }

            TemplateListResolutionResult matchResults = new TemplateListResolutionResult()
            {
                CoreMatchedTemplates = coreMatchedTemplates
            };

            QueryForUnambiguousTemplateGroup(templateInfo, hostDataLoader, commandInput, matchResults, defaultLanguage, anyExactCoreMatches);

            return matchResults;
        }

        private static void QueryForUnambiguousTemplateGroup(IReadOnlyList<ITemplateInfo> templateInfo, HostSpecificDataLoader hostDataLoader, INewCommandInput commandInput, TemplateListResolutionResult matchResults, string defaultLanguage, bool anyExactCoreMatches)
        {
            if (!anyExactCoreMatches || matchResults.CoreMatchedTemplates == null || matchResults.CoreMatchedTemplates.Count == 0)
            {
                return;
            }

            if (matchResults.CoreMatchedTemplates.Count == 1)
            {
                matchResults.UnambiguousTemplateGroupToUse = matchResults.CoreMatchedTemplates;
                return;
            }

            if (string.IsNullOrEmpty(commandInput.Language) && !string.IsNullOrEmpty(defaultLanguage))
            {
                IReadOnlyList<IFilteredTemplateInfo> languageMatchedTemplates = FindTemplatesExplicitlyMatchingLanguage(matchResults.CoreMatchedTemplates, defaultLanguage);
                if (languageMatchedTemplates.Count == 1)
                {
                    matchResults.UnambiguousTemplateGroupToUse = languageMatchedTemplates;
                    return;
                }
            }

            UseSecondaryCriteriaToDisambiguateTemplateMatches(hostDataLoader, matchResults, commandInput, defaultLanguage);
        }

        // Returns the templates from the input list whose language is the input language.
        private static IReadOnlyList<IFilteredTemplateInfo> FindTemplatesExplicitlyMatchingLanguage(IEnumerable<IFilteredTemplateInfo> listToFilter, string language)
        {
            List<IFilteredTemplateInfo> languageMatches = new List<IFilteredTemplateInfo>();

            if (string.IsNullOrEmpty(language))
            {
                return languageMatches;
            }

            foreach (IFilteredTemplateInfo info in listToFilter)
            {
                // ChoicesAndDescriptions is invoked as case insensitive
                if (info.Info.Tags == null ||
                    info.Info.Tags.TryGetValue("language", out ICacheTag languageTag) &&
                    languageTag.ChoicesAndDescriptions.ContainsKey(language))
                {
                    languageMatches.Add(info);
                }
            }

            return languageMatches;
        }

        // coordinates filtering templates based on the parameters & language
        private static void UseSecondaryCriteriaToDisambiguateTemplateMatches(HostSpecificDataLoader hostDataLoader, TemplateListResolutionResult matchResults, INewCommandInput commandInput, string defaultLanguage)
        {
            if (string.IsNullOrEmpty(commandInput.TemplateName))
            {
                return;
            }

            matchResults.MatchedTemplatesWithSecondaryMatchInfo = FilterTemplatesOnParameters(matchResults.CoreMatchedTemplates, hostDataLoader, commandInput).Where(x => x.IsMatch).ToList();

            IReadOnlyList<IFilteredTemplateInfo> matchesAfterParameterChecks = matchResults.MatchedTemplatesWithSecondaryMatchInfo.Where(x => x.IsParameterMatch).ToList();
            if (matchResults.MatchedTemplatesWithSecondaryMatchInfo.Any(x => x.HasAmbiguousParameterMatch))
            {
                matchesAfterParameterChecks = matchResults.MatchedTemplatesWithSecondaryMatchInfo;
            }

            if (matchesAfterParameterChecks.Count == 0)
            {   // no param matches, continue additional matching with the list from before param checking (but with the param match dispositions)
                matchesAfterParameterChecks = matchResults.MatchedTemplatesWithSecondaryMatchInfo;
            }

            if (matchesAfterParameterChecks.Count == 1)
            {
                matchResults.UnambiguousTemplateGroupToUse = matchesAfterParameterChecks;
                return;
            }
            else if (string.IsNullOrEmpty(commandInput.Language) && !string.IsNullOrEmpty(defaultLanguage))
            {
                IReadOnlyList<IFilteredTemplateInfo> languageFiltered = FindTemplatesExplicitlyMatchingLanguage(matchesAfterParameterChecks, defaultLanguage);

                if (languageFiltered.Count == 1)
                {
                    matchResults.UnambiguousTemplateGroupToUse = languageFiltered;
                    return;
                }
                else if (AreAllTemplatesSameGroupIdentity(languageFiltered))
                {
                    IReadOnlyList<IFilteredTemplateInfo> languageFilteredMatchesAfterParameterChecks = languageFiltered.Where(x => x.IsParameterMatch).ToList();
                    if (languageFilteredMatchesAfterParameterChecks.Count > 0 && !languageFiltered.Any(x => x.HasAmbiguousParameterMatch))
                    {
                        matchResults.UnambiguousTemplateGroupToUse = languageFilteredMatchesAfterParameterChecks;
                        return;
                    }

                    matchResults.UnambiguousTemplateGroupToUse = languageFiltered;
                    return;
                }
            }
            else if (AreAllTemplatesSameGroupIdentity(matchesAfterParameterChecks))
            {
                matchResults.UnambiguousTemplateGroupToUse = matchesAfterParameterChecks;
                return;
            }
        }

        // filters templates based on matches between the input parameters & the template parameters.
        private static IReadOnlyList<IFilteredTemplateInfo> FilterTemplatesOnParameters(IReadOnlyList<IFilteredTemplateInfo> templatesToFilter, HostSpecificDataLoader hostDataLoader, INewCommandInput commandInput)
        {
            List<IFilteredTemplateInfo> filterResults = new List<IFilteredTemplateInfo>();

            foreach (IFilteredTemplateInfo templateWithFilterInfo in templatesToFilter)
            {
                List<MatchInfo> dispositionForTemplate = templateWithFilterInfo.MatchDisposition.ToList();

                try
                {
                    ParseTemplateArgs(templateWithFilterInfo.Info, hostDataLoader, commandInput);

                    // params are already parsed. But choice values aren't checked
                    foreach (KeyValuePair<string, string> matchedParamInfo in commandInput.AllTemplateParams)
                    {
                        string paramName = matchedParamInfo.Key;
                        string paramValue = matchedParamInfo.Value;

                        if (templateWithFilterInfo.Info.Tags.TryGetValue(paramName, out ICacheTag paramDetails))
                        {
                            // key is the value user should provide, value is description
                            if (paramDetails.ChoicesAndDescriptions.ContainsKey(paramValue))
                            {
                                dispositionForTemplate.Add(new MatchInfo { Location = MatchLocation.OtherParameter, Kind = MatchKind.Exact, ChoiceIfLocationIsOtherChoice = paramName });
                            }
                            else
                            {
                                int startsWithCount = paramDetails.ChoicesAndDescriptions.Count(x => x.Key.StartsWith(paramValue, StringComparison.OrdinalIgnoreCase));
                                if (startsWithCount == 1)
                                {
                                    dispositionForTemplate.Add(new MatchInfo { Location = MatchLocation.OtherParameter, Kind = MatchKind.Exact, ChoiceIfLocationIsOtherChoice = paramName });
                                }
                                else if (startsWithCount > 1)
                                {
                                    dispositionForTemplate.Add(new MatchInfo { Location = MatchLocation.OtherParameter, Kind = MatchKind.AmbiguousParameterValue, ChoiceIfLocationIsOtherChoice = paramName });
                                }
                                else
                                {
                                    dispositionForTemplate.Add(new MatchInfo { Location = MatchLocation.OtherParameter, Kind = MatchKind.InvalidParameterValue, ChoiceIfLocationIsOtherChoice = paramName });
                                }
                            }
                        }
                        else if (templateWithFilterInfo.Info.CacheParameters.ContainsKey(paramName))
                        {
                            dispositionForTemplate.Add(new MatchInfo { Location = MatchLocation.OtherParameter, Kind = MatchKind.Exact, ChoiceIfLocationIsOtherChoice = paramName });
                        }
                        else
                        {
                            dispositionForTemplate.Add(new MatchInfo { Location = MatchLocation.OtherParameter, Kind = MatchKind.InvalidParameterValue, ChoiceIfLocationIsOtherChoice = paramName });
                        }
                    }

                    foreach (string unmatchedParamName in commandInput.RemainingParameters.Keys.Where(x => !x.Contains(':')))   // filter debugging params
                    {
                        if (commandInput.TryGetCanonicalNameForVariant(unmatchedParamName, out string canonical))
                        {   // the name is a known template param, it must have not parsed due to an invalid value
                            //
                            // Note (scp 2017-02-27): This probably can't happen, the param parsing doesn't check the choice values.
                            dispositionForTemplate.Add(new MatchInfo { Location = MatchLocation.OtherParameter, Kind = MatchKind.InvalidParameterValue, ChoiceIfLocationIsOtherChoice = unmatchedParamName });
                        }
                        else
                        {   // the name is not known
                            dispositionForTemplate.Add(new MatchInfo { Location = MatchLocation.OtherParameter, Kind = MatchKind.InvalidParameterName, ChoiceIfLocationIsOtherChoice = unmatchedParamName });
                        }
                    }
                }
                catch
                {   // if we do actually throw, add a non-match
                    dispositionForTemplate.Add(new MatchInfo { Location = MatchLocation.Unspecified, Kind = MatchKind.Unspecified });
                }

                filterResults.Add(new FilteredTemplateInfo(templateWithFilterInfo.Info, dispositionForTemplate));
            }

            return filterResults;
        }
    }
}
