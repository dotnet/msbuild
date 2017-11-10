// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Edge.Template;

namespace Microsoft.TemplateEngine.Cli
{
    public class TemplateListResolutionResult
    {
        public TemplateListResolutionResult(string templateName, string userInputLanguage, IReadOnlyCollection<ITemplateMatchInfo> coreMatchedTemplates, IReadOnlyCollection<ITemplateMatchInfo> allTemplatesInContext)
        {
            _templateName = templateName;
            _hasUserInputLanguage = !string.IsNullOrEmpty(userInputLanguage);
            _coreMatchedTemplates = coreMatchedTemplates;
            _allTemplatesInContext = allTemplatesInContext;
            _bestTemplateMatchList = null;
            _usingContextMatches = false;
        }

        private readonly string _templateName;
        private readonly bool _hasUserInputLanguage;

        private readonly IReadOnlyCollection<ITemplateMatchInfo> _coreMatchedTemplates;
        private readonly IReadOnlyCollection<ITemplateMatchInfo> _allTemplatesInContext;

        private bool _usingContextMatches;

        public bool TryGetCoreMatchedTemplatesWithDisposition(Func<ITemplateMatchInfo, bool> filter, out IReadOnlyList<ITemplateMatchInfo> matchingTemplates)
        {
            matchingTemplates = _coreMatchedTemplates.Where(filter).ToList();
            return matchingTemplates.Count != 0;
        }

        // If a single template group can be resolved, return it.
        // If the user input a language, default language results are not considered.
        // ignoreDefaultLanguageFiltering = true will also cause default language filtering to be ignored. Be careful when using this option.
        public bool TryGetUnambiguousTemplateGroupToUse(out IReadOnlyList<ITemplateMatchInfo> unambiguousTemplateGroup, bool ignoreDefaultLanguageFiltering = false)
        {
            if (_coreMatchedTemplates.Count == 0)
            {
                unambiguousTemplateGroup = null;
                return false;
            }

            if (_coreMatchedTemplates.Count == 1)
            {
                unambiguousTemplateGroup = new List<ITemplateMatchInfo>(_coreMatchedTemplates);
                return true;
            }

            // maybe: only use default language if we're trying to invoke
            if (!_hasUserInputLanguage && !ignoreDefaultLanguageFiltering)
            {
                // only consider default language match dispositions if the user did not specify a language.
                List<ITemplateMatchInfo> defaultLanguageMatchedTemplates = _coreMatchedTemplates.Where(x =>
                                                                x.DispositionOfDefaults.Any(y => y.Location == MatchLocation.DefaultLanguage && y.Kind == MatchKind.Exact)
                                                                && !x.MatchDisposition.Any(z => z.Location == MatchLocation.Context && z.Kind == MatchKind.Mismatch))
                                                                            .ToList();

                if (TemplateListResolver.AreAllTemplatesSameGroupIdentity(defaultLanguageMatchedTemplates))
                {
                    if (defaultLanguageMatchedTemplates.Any(x => !x.HasParameterMismatch() && !x.HasContextMismatch()))
                    {
                        unambiguousTemplateGroup = defaultLanguageMatchedTemplates.Where(x => !x.HasParameterMismatch() && !x.HasContextMismatch()).ToList();
                        return true;
                    }
                    else
                    {
                        unambiguousTemplateGroup = defaultLanguageMatchedTemplates;
                        return true;
                    }
                }
            }

            List<ITemplateMatchInfo> paramFiltered = _coreMatchedTemplates.Where(x => !x.HasParameterMismatch() && !x.HasContextMismatch()).ToList();
            if (TemplateListResolver.AreAllTemplatesSameGroupIdentity(paramFiltered))
            {
                unambiguousTemplateGroup = paramFiltered;
                return true;
            }

            if (TemplateListResolver.AreAllTemplatesSameGroupIdentity(_coreMatchedTemplates))
            {
                unambiguousTemplateGroup = new List<ITemplateMatchInfo>(_coreMatchedTemplates);
                return true;
            }

            unambiguousTemplateGroup = null;
            return false;
        }

        // Note: This method does not consider default language matches / mismatches
        public bool TryGetAllInvokableTemplates(out IReadOnlyList<ITemplateMatchInfo> invokableTemplates)
        {
            IEnumerable<ITemplateMatchInfo> invokableMatches = _coreMatchedTemplates.Where(x => x.IsInvokableMatch());

            if (invokableMatches.Any())
            {
                invokableTemplates = invokableMatches.ToList();
                return true;
            }

            invokableTemplates = null;
            return false;
        }

        public bool TryGetSingularInvokableMatch(out ITemplateMatchInfo template)
        {
            IReadOnlyList<ITemplateMatchInfo> invokableMatches = _coreMatchedTemplates.Where(x => x.IsInvokableMatch() 
                                                && (_hasUserInputLanguage
                                                    || x.DispositionOfDefaults.Count == 0
                                                    || x.DispositionOfDefaults.Any(y => y.Location == MatchLocation.DefaultLanguage && y.Kind == MatchKind.Exact))
                                                    )
                                                    .ToList();
            
            if (invokableMatches.Count == 1)
            {
                template = invokableMatches[0];
                return true;
            }

            // if multiple templates in the group have single starts with matches on the same parameter, it's ambiguous.
            // For the case where one template has single starts with, and another has ambiguous - on the same param:
            //      The one with single starts with is chosen as invokable because if the template with an ambiguous match
            //      was not installed, the one with the singluar invokable would be chosen.
            HashSet<string> singleStartsWithParamNames = new HashSet<string>();
            foreach (ITemplateMatchInfo checkTemplate in invokableMatches)
            {
                IList<string> singleStartParamNames = checkTemplate.MatchDisposition.Where(x => x.Location == MatchLocation.OtherParameter && x.Kind == MatchKind.SingleStartsWith).Select(x => x.ChoiceIfLocationIsOtherChoice).ToList();
                foreach (string paramName in singleStartParamNames)
                {
                    if (!singleStartsWithParamNames.Add(paramName))
                    {
                        template = null;
                        return false;
                    }
                }
            }

            ITemplateMatchInfo highestInGroupIfSingleGroup = TemplateListResolver.FindHighestPrecedenceTemplateIfAllSameGroupIdentity(invokableMatches);
            if (highestInGroupIfSingleGroup != null)
            {
                template = highestInGroupIfSingleGroup;
                return true;
            }

            template = null;
            return false;
        }

        private IReadOnlyList<ITemplateMatchInfo> _bestTemplateMatchList;
        private IReadOnlyList<ITemplateMatchInfo> _bestTemplateMatchListIgnoringDefaultLanguageFiltering;

        public IReadOnlyList<ITemplateMatchInfo> GetBestTemplateMatchList(bool ignoreDefaultLanguageFiltering = false)
        {
            if (ignoreDefaultLanguageFiltering)
            {
                if (_bestTemplateMatchList == null)
                {
                    _bestTemplateMatchList = BaseGetBestTemplateMatchList(ignoreDefaultLanguageFiltering);
                }

                return _bestTemplateMatchList;
            }
            else
            {
                if (_bestTemplateMatchListIgnoringDefaultLanguageFiltering == null)
                {
                    _bestTemplateMatchListIgnoringDefaultLanguageFiltering = BaseGetBestTemplateMatchList(ignoreDefaultLanguageFiltering);
                }

                return _bestTemplateMatchListIgnoringDefaultLanguageFiltering;
            }
        }

        // The core matched templates should not need additioanl default language filtering.
        // The default language dispositions are stored in a different place than the other dispositions,
        // and are not considered for most match filtering.
        private IReadOnlyList<ITemplateMatchInfo> BaseGetBestTemplateMatchList(bool ignoreDefaultLanguageFiltering)
        {
            IReadOnlyList<ITemplateMatchInfo> templateList;

            if (TryGetUnambiguousTemplateGroupToUse(out templateList, ignoreDefaultLanguageFiltering))
            {
                return templateList;
            }
            else if (!string.IsNullOrEmpty(_templateName) && TryGetAllInvokableTemplates(out templateList))
            {
                return templateList;
            }
            else if (TryGetCoreMatchedTemplatesWithDisposition(x => x.IsMatch, out templateList))
            {
                return templateList;
            }
            else if (TryGetCoreMatchedTemplatesWithDisposition(x => x.IsMatchExceptContext(), out templateList))
            {
                return templateList;
            }
            else if (TryGetCoreMatchedTemplatesWithDisposition(x => x.IsPartialMatch, out templateList))
            {
                return templateList;
            }
            else if (TryGetCoreMatchedTemplatesWithDisposition(x => x.IsPartialMatchExceptContext(), out templateList))
            {
                return templateList;
            }
            else
            {
                templateList = _allTemplatesInContext.ToList();
                _usingContextMatches = true;
                return templateList;
            }
        }

        // If BaseGetBestTemplateMatchList returned a list from _allTemplatesInContext, this is true.
        // false otherwise.
        public bool UsingContextMatches
        {
            get
            {
                return _usingContextMatches;
            }
        }
    }
}
