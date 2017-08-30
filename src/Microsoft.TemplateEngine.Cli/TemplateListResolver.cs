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
            MatchLocation.ShortName,
            MatchLocation.Alias
        };

        public static void ParseTemplateArgs(ITemplateInfo templateInfo, IHostSpecificDataLoader hostDataLoader, INewCommandInput commandInput)
        {
            HostSpecificTemplateData hostData = hostDataLoader.ReadHostSpecificTemplateData(templateInfo);
            commandInput.ReparseForTemplate(templateInfo, hostData);
        }

        public static bool AreAllTemplatesSameGroupIdentity(IEnumerable<IFilteredTemplateInfo> templateList)
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
        public static bool ValidateRemainingParameters(IFilteredTemplateInfo template, out IReadOnlyList<string> invalidParams)
        {
            invalidParams = template.InvalidParameterNames;

            return !invalidParams.Any();
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
        public static IReadOnlyCollection<IFilteredTemplateInfo> PerformAllTemplatesQuery(IReadOnlyList<ITemplateInfo> templateInfo, IHostSpecificDataLoader hostDataLoader)
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
        public static IReadOnlyCollection<IFilteredTemplateInfo> PerformAllTemplatesInContextQuery(IReadOnlyList<ITemplateInfo> templateInfo, IHostSpecificDataLoader hostDataLoader, string context)
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
        public static TemplateListResolutionResult PerformCoreTemplateQuery(IReadOnlyList<ITemplateInfo> templateInfo, IHostSpecificDataLoader hostDataLoader, INewCommandInput commandInput, string defaultLanguage)
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
            TemplateListResolutionResult matchResults;

            if (coreMatchedTemplates.Count == 0)
            {
                // No exact matches, take the partial matches and be done.
                matchResults = new TemplateListResolutionResult(!string.IsNullOrEmpty(commandInput.Language))
                {
                    CoreMatchedTemplates = templates.Where(x => x.IsPartialMatch).ToList()
                };
            }
            else
            {
                IReadOnlyList<IFilteredTemplateInfo> matchesWithExactDispositionsInNameFields = coreMatchedTemplates.Where(x => x.MatchDisposition.Any(y => NameFields.Contains(y.Location) && y.Kind == MatchKind.Exact)).ToList();
                if (matchesWithExactDispositionsInNameFields.Count > 0)
                {
                    // Start the exact name matches, if there are any.
                    coreMatchedTemplates = matchesWithExactDispositionsInNameFields;
                }

                matchResults = new TemplateListResolutionResult(!string.IsNullOrEmpty(commandInput.Language))
                {
                    CoreMatchedTemplates = coreMatchedTemplates
                };

                if (string.IsNullOrEmpty(commandInput.Language) && !string.IsNullOrEmpty(defaultLanguage))
                {   // default language matching only makes sense if the user didn't specify a language.
                    AddDefaultLanguageMatchingToTemplates(matchResults.CoreMatchedTemplates, defaultLanguage);
                }

                AddParameterMatchingToTemplates(matchResults.CoreMatchedTemplates, hostDataLoader, commandInput);
            }

            return matchResults;
        }

        private static void AddDefaultLanguageMatchingToTemplates(IReadOnlyList<IFilteredTemplateInfo> listToFilter, string language)
        {
            if (string.IsNullOrEmpty(language))
            {
                return;
            }

            foreach (IFilteredTemplateInfo template in listToFilter)
            {
                MatchKind matchKind;

                if (template.Info.Tags == null ||
                    template.Info.Tags.TryGetValue("language", out ICacheTag languageTag) &&
                    languageTag.ChoicesAndDescriptions.ContainsKey(language))
                {
                    matchKind = MatchKind.Exact;
                }
                else
                {
                    matchKind = MatchKind.Mismatch;
                }

                template.AddDefaultDisposition(new MatchInfo
                {
                    Location = MatchLocation.DefaultLanguage,
                    Kind = matchKind
                });
            }
        }

        // adds dispositions to the templates based on matches between the input parameters & the template parameters.
        private static void AddParameterMatchingToTemplates(IReadOnlyList<IFilteredTemplateInfo> templatesToFilter, IHostSpecificDataLoader hostDataLoader, INewCommandInput commandInput)
        {
            foreach (IFilteredTemplateInfo template in templatesToFilter)
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
                            // key is the value user should provide, value is description
                            if (paramDetails.ChoicesAndDescriptions.ContainsKey(paramValue))
                            {
                                template.AddDisposition(new MatchInfo { Location = MatchLocation.OtherParameter, Kind = MatchKind.Exact, ChoiceIfLocationIsOtherChoice = paramName, ParameterValue = paramValue });
                            }
                            else
                            {
                                int startsWithCount = paramDetails.ChoicesAndDescriptions.Count(x => x.Key.StartsWith(paramValue, StringComparison.OrdinalIgnoreCase));
                                if (startsWithCount == 1)
                                {
                                    template.AddDisposition(new MatchInfo { Location = MatchLocation.OtherParameter, Kind = MatchKind.Exact, ChoiceIfLocationIsOtherChoice = paramName, ParameterValue = paramValue });
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
                        {   // the name is a known template param, it must have not parsed due to an invalid value
                            //
                            // Note (scp 2017-02-27): This probably can't happen, the param parsing doesn't check the choice values.
                            template.AddDisposition(new MatchInfo { Location = MatchLocation.OtherParameter, Kind = MatchKind.InvalidParameterValue, ChoiceIfLocationIsOtherChoice = unmatchedParamName });
                        }
                        else
                        {   // the name is not known
                            // TODO: reconsider storing the canonical in this situation. It's not really a canonical since the param is unknown.
                            template.AddDisposition(new MatchInfo { Location = MatchLocation.OtherParameter, Kind = MatchKind.InvalidParameterName, ChoiceIfLocationIsOtherChoice = unmatchedParamName });
                        }
                    }
                }
                catch (CommandParserException ex)
                {   // if we do actually throw, add a non-match
                    template.AddDisposition(new MatchInfo { Location = MatchLocation.Unspecified, Kind = MatchKind.Unspecified, AdditionalInformation = ex.Message });
                }
            }
        }
    }
}
