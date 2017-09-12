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

        private IReadOnlyList<ITemplateMatchInfo> _bestTemplateMatchList;
        private bool _usingContextMatches;

        public bool TryGetCoreMatchedTemplatesWithDisposition(Func<ITemplateMatchInfo, bool> filter, out IReadOnlyList<ITemplateMatchInfo> matchingTemplates)
        {
            matchingTemplates = _coreMatchedTemplates.Where(filter).ToList();
            return matchingTemplates.Count != 0;
        }

        public bool TryGetUnambiguousTemplateGroupToUse(out IReadOnlyList<ITemplateMatchInfo> unambiguousTemplateGroup)
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
            if (!_hasUserInputLanguage)
            {
                // only consider default language match dispositions if the user did not specify a language.
                List<ITemplateMatchInfo> defaultLanguageMatchedTemplates = _coreMatchedTemplates.Where(x => x.DispositionOfDefaults
                                                                            .Any(y => y.Location == MatchLocation.DefaultLanguage && y.Kind == MatchKind.Exact))
                                                                            .ToList();

                if (TemplateListResolver.AreAllTemplatesSameGroupIdentity(defaultLanguageMatchedTemplates))
                {
                    if (defaultLanguageMatchedTemplates.Any(x => !x.HasParameterMismatch()))
                    {
                        unambiguousTemplateGroup = defaultLanguageMatchedTemplates.Where(x => !x.HasParameterMismatch()).ToList();
                        return true;
                    }
                    else
                    {
                        unambiguousTemplateGroup = defaultLanguageMatchedTemplates;
                        return true;
                    }
                }
            }

            List<ITemplateMatchInfo> paramFiltered = _coreMatchedTemplates.Where(x => !x.HasParameterMismatch()).ToList();
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
            IReadOnlyList<ITemplateMatchInfo> invokableMatches = _coreMatchedTemplates.Where(x => x.IsInvokableMatch()).ToList();
            if (invokableMatches.Count() == 1)
            {
                template = invokableMatches[0];
                return true;
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

        public IReadOnlyList<ITemplateMatchInfo> BestTemplateMatchList
        {
            get
            {
                if (_bestTemplateMatchList == null)
                {
                    IReadOnlyList<ITemplateMatchInfo> templateList;

                    Console.Write("*** GetBestTemplateMatchList()... ");
                    if (TryGetUnambiguousTemplateGroupToUse(out templateList))
                    {
                        Console.WriteLine("Unambiguous");
                        _bestTemplateMatchList = templateList;
                    }
                    else if (!string.IsNullOrEmpty(_templateName) && TryGetAllInvokableTemplates(out templateList))
                    {
                        Console.WriteLine("All Invokable");
                        _bestTemplateMatchList = templateList;
                    }
                    else if (TryGetCoreMatchedTemplatesWithDisposition(x => x.IsMatch, out templateList))
                    {
                        Console.WriteLine("IsMatch");
                        _bestTemplateMatchList = templateList;
                    }
                    else if (TryGetCoreMatchedTemplatesWithDisposition(x => x.IsMatchExceptContext(), out templateList))
                    {
                        Console.WriteLine("IsMatchExceptContext");
                        _bestTemplateMatchList = templateList;
                    }
                    else if (TryGetCoreMatchedTemplatesWithDisposition(x => x.IsPartialMatch, out templateList))
                    {
                        Console.WriteLine("IsPartialMatch");
                        _bestTemplateMatchList = templateList;
                    }
                    else if (TryGetCoreMatchedTemplatesWithDisposition(x => x.IsPartialMatchExceptContext(), out templateList))
                    {
                        Console.WriteLine("IsPartialMatchExceptContext");
                        _bestTemplateMatchList = templateList;
                    }
                    else
                    {
                        Console.WriteLine("all in context");
                        _bestTemplateMatchList = _allTemplatesInContext.ToList();
                        _usingContextMatches = true;
                    }
                }

                return _bestTemplateMatchList;
            }
        }

        public bool UsingContextMatches
        {
            get
            {
                return _usingContextMatches;
            }
        }
    }
}
