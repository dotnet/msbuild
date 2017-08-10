// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Edge.Template;
using Microsoft.TemplateEngine.Utils;
using Microsoft.TemplateEngine.Cli.CommandParsing;

namespace Microsoft.TemplateEngine.Cli
{
    internal class TemplateResolver
    {
        public TemplateResolver(Func<TemplateCache> templateCacheFunc, HostSpecificDataLoader hostDataLoader, INewCommandInput commandInput, string defaultLanguage)
        {
            _templateCacheFunc = templateCacheFunc;
            _hostDataLoader = hostDataLoader;
            _commandInput = commandInput;
            _defaultLanguage = defaultLanguage;

            _context = _commandInput.TypeFilter?.ToLowerInvariant();
            _templateName = _commandInput?.TemplateName;
            _inputLanguage = _commandInput?.Language;
        }

        private Func<TemplateCache> _templateCacheFunc;
        private readonly HostSpecificDataLoader _hostDataLoader;
        private readonly INewCommandInput _commandInput;

        private readonly string _defaultLanguage;
        private readonly string _context;
        private readonly string _templateName;
        private readonly string _inputLanguage;

        // Templates matched by the "core" query.
        private IReadOnlyList<IFilteredTemplateInfo> _coreMatchedTemplates;
        // If a single group can be resolved, it's stored here.
        private IReadOnlyList<IFilteredTemplateInfo> _unambiguousTemplateGroupToUse;
        // If a template group can be uniquely identified using secondary criteria, it's stored here.
        IReadOnlyList<IFilteredTemplateInfo> _secondaryFilteredUnambiguousTemplateGroupToUse;
        // The list of templates, including both primary and secondary criteria results.
        private IReadOnlyList<IFilteredTemplateInfo> _matchedTemplatesWithSecondaryMatchInfo;

        // When true, there were no core matches, so there can't be an unambiguous match.
        private bool _anyExactCoreMatches;

        private static readonly IReadOnlyCollection<MatchLocation> NameFields = new HashSet<MatchLocation>
        {
            MatchLocation.Name,
            MatchLocation.ShortName,
            MatchLocation.Alias
        };

        private bool IsTemplateHiddenByHostFile(ITemplateInfo templateInfo)
        {
            HostSpecificTemplateData hostData = _hostDataLoader.ReadHostSpecificTemplateData(templateInfo);
            return hostData.IsHidden;
        }

        private void ParseTemplateArgs(ITemplateInfo templateInfo)
        {
            HostSpecificTemplateData hostData = _hostDataLoader.ReadHostSpecificTemplateData(templateInfo);
            _commandInput.ReparseForTemplate(templateInfo, hostData);
        }

        // Lists all the templates, unfiltered - except the ones hidden by their host file.
        public IReadOnlyCollection<IFilteredTemplateInfo> PerformAllTemplatesQuery()
        {
            IReadOnlyCollection<IFilteredTemplateInfo> templates = _templateCacheFunc().List
            (
                false,
                WellKnownSearchFilters.NameFilter(string.Empty)
            )
            .Where(x => !IsTemplateHiddenByHostFile(x.Info)).ToList();

            return templates;
        }

        // Lists all the templates, filtered only by the context (item, project, etc)
        public IReadOnlyCollection<IFilteredTemplateInfo> PerformAllTemplatesInContextQuery()
        {
            IReadOnlyCollection<IFilteredTemplateInfo> templates = _templateCacheFunc().List
            (
                false,
                WellKnownSearchFilters.ContextFilter(_context),
                WellKnownSearchFilters.NameFilter(string.Empty)
            )
            .Where(x => !IsTemplateHiddenByHostFile(x.Info)).ToList();

            return templates;
        }

        public void PerformCoreTemplateQuery()
        {
            IReadOnlyCollection<IFilteredTemplateInfo> templates = _templateCacheFunc().List
            (
                false,
                WellKnownSearchFilters.NameFilter(_templateName),
                WellKnownSearchFilters.ClassificationsFilter(_templateName),
                WellKnownSearchFilters.LanguageFilter(_inputLanguage),
                WellKnownSearchFilters.ContextFilter(_context),
                WellKnownSearchFilters.BaselineFilter(_commandInput?.BaselineName)
            )
            .Where(x => !IsTemplateHiddenByHostFile(x.Info)).ToList();

            IReadOnlyList<IFilteredTemplateInfo> coreMatchedTemplates = templates.Where(x => x.IsMatch).ToList();

            if (coreMatchedTemplates.Count == 0)
            {
                coreMatchedTemplates = templates.Where(x => x.IsPartialMatch).ToList();
                _anyExactCoreMatches = false;
            }
            else
            {
                _anyExactCoreMatches = true;
                IReadOnlyList<IFilteredTemplateInfo> matchesWithExactDispositionsInNameFields = coreMatchedTemplates.Where(x => x.MatchDisposition.Any(y => NameFields.Contains(y.Location) && y.Kind == MatchKind.Exact)).ToList();

                if (matchesWithExactDispositionsInNameFields.Count > 0)
                {
                    coreMatchedTemplates = matchesWithExactDispositionsInNameFields;
                }
            }

            _coreMatchedTemplates = coreMatchedTemplates;
        }

        private static bool AreAllTemplatesSameGroupIdentity(IEnumerable<IFilteredTemplateInfo> templateList)
        {
            return templateList.AllAreTheSame((x) => x.Info.GroupIdentity, StringComparer.OrdinalIgnoreCase);
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

        public IReadOnlyList<IFilteredTemplateInfo> CoreMatchedTemplates
        {
            get
            {
                return _coreMatchedTemplates;
            }
        }

        // If a template group & language can be unambiguously determined, return those templates.
        // Otherwise return null.
        public IReadOnlyList<IFilteredTemplateInfo> UnambiguousTemplateGroupToUse
        {
            get
            {
                if (_unambiguousTemplateGroupToUse != null)
                {
                    return _unambiguousTemplateGroupToUse;
                }

                if (!_anyExactCoreMatches || _coreMatchedTemplates == null || _coreMatchedTemplates.Count == 0)
                {
                    return null;
                }

                if (_coreMatchedTemplates.Count == 1)
                {
                    return _unambiguousTemplateGroupToUse = _coreMatchedTemplates;
                }
                else if (string.IsNullOrEmpty(_inputLanguage) && !string.IsNullOrEmpty(_defaultLanguage))
                {
                    IReadOnlyList<IFilteredTemplateInfo> languageMatchedTemplates = FindTemplatesExplicitlyMatchingLanguage(_coreMatchedTemplates, _defaultLanguage);

                    if (languageMatchedTemplates.Count == 1)
                    {
                        return _unambiguousTemplateGroupToUse = languageMatchedTemplates;
                    }
                }

                return _unambiguousTemplateGroupToUse = UseSecondaryCriteriaToDisambiguateTemplateMatches();
            }
        }

        public IReadOnlyList<IFilteredTemplateInfo> MatchedTemplatesWithSecondaryMatchInfo
        {
            get
            {
                return _matchedTemplatesWithSecondaryMatchInfo;
            }
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

        // Does additional checks on the _coreMatchedTemplates.
        // Stores the match details in _matchedTemplatesWithSecondaryMatchInfo
        // If there is exactly 1 template group & language that matches on secondary info, return it. Return null otherwise.
        private IReadOnlyList<IFilteredTemplateInfo> UseSecondaryCriteriaToDisambiguateTemplateMatches()
        {
            if (_secondaryFilteredUnambiguousTemplateGroupToUse == null && !string.IsNullOrEmpty(_templateName))
            {
                if (_matchedTemplatesWithSecondaryMatchInfo == null)
                {
                    _matchedTemplatesWithSecondaryMatchInfo = FilterTemplatesOnParameters(_coreMatchedTemplates).Where(x => x.IsMatch).ToList();

                    IReadOnlyList<IFilteredTemplateInfo> matchesAfterParameterChecks = _matchedTemplatesWithSecondaryMatchInfo.Where(x => x.IsParameterMatch).ToList();
                    if (_matchedTemplatesWithSecondaryMatchInfo.Any(x => x.HasAmbiguousParameterMatch))
                    {
                        matchesAfterParameterChecks = _matchedTemplatesWithSecondaryMatchInfo;
                    }

                    if (matchesAfterParameterChecks.Count == 0)
                    {   // no param matches, continue additional matching with the list from before param checking (but with the param match dispositions)
                        matchesAfterParameterChecks = _matchedTemplatesWithSecondaryMatchInfo;
                    }

                    if (matchesAfterParameterChecks.Count == 1)
                    {
                        return _secondaryFilteredUnambiguousTemplateGroupToUse = matchesAfterParameterChecks;
                    }
                    else if (string.IsNullOrEmpty(_inputLanguage) && !string.IsNullOrEmpty(_defaultLanguage))
                    {
                        IReadOnlyList<IFilteredTemplateInfo> languageFiltered = FindTemplatesExplicitlyMatchingLanguage(matchesAfterParameterChecks, _defaultLanguage);

                        if (languageFiltered.Count == 1)
                        {
                            return _secondaryFilteredUnambiguousTemplateGroupToUse = languageFiltered;
                        }
                        else if (AreAllTemplatesSameGroupIdentity(languageFiltered))
                        {
                            IReadOnlyList<IFilteredTemplateInfo> languageFilteredMatchesAfterParameterChecks = languageFiltered.Where(x => x.IsParameterMatch).ToList();
                            if (languageFilteredMatchesAfterParameterChecks.Count > 0 && !languageFiltered.Any(x => x.HasAmbiguousParameterMatch))
                            {
                                return _secondaryFilteredUnambiguousTemplateGroupToUse = languageFilteredMatchesAfterParameterChecks;
                            }

                            return _secondaryFilteredUnambiguousTemplateGroupToUse = languageFiltered;
                        }
                    }
                    else if (AreAllTemplatesSameGroupIdentity(matchesAfterParameterChecks))
                    {
                        return _secondaryFilteredUnambiguousTemplateGroupToUse = matchesAfterParameterChecks;
                    }
                }
            }

            return _secondaryFilteredUnambiguousTemplateGroupToUse;
        }

        private IReadOnlyList<IFilteredTemplateInfo> FilterTemplatesOnParameters(IReadOnlyList<IFilteredTemplateInfo> templatesToFilter)
        {
            List<IFilteredTemplateInfo> filterResults = new List<IFilteredTemplateInfo>();

            foreach (IFilteredTemplateInfo templateWithFilterInfo in templatesToFilter)
            {
                List<MatchInfo> dispositionForTemplate = templateWithFilterInfo.MatchDisposition.ToList();

                try
                {
                    ParseTemplateArgs(templateWithFilterInfo.Info);

                    // params are already parsed. But choice values aren't checked
                    foreach (KeyValuePair<string, string> matchedParamInfo in _commandInput.AllTemplateParams)
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

                    foreach (string unmatchedParamName in _commandInput.RemainingParameters.Keys.Where(x => !x.Contains(':')))   // filter debugging params
                    {
                        if (_commandInput.TryGetCanonicalNameForVariant(unmatchedParamName, out string canonical))
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
