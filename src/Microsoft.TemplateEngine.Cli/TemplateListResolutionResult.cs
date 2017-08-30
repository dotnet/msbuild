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
        public TemplateListResolutionResult(bool hasUserInputLanguage)
        {
            _hasUserInputLanguage = hasUserInputLanguage;
        }

        private readonly bool _hasUserInputLanguage;

        public IReadOnlyList<ITemplateMatchInfo> CoreMatchedTemplates { get; set; }

        public bool HasCoreMatchedTemplatesWithDisposition(Func<ITemplateMatchInfo, bool> filter)
        {
            return CoreMatchedTemplates != null
                    && CoreMatchedTemplates.Any(filter);
        }

        public bool TryGetUnambiguousTemplateGroupToUse(out IReadOnlyList<ITemplateMatchInfo> unambiguousTemplateGroup)
        {
            if (CoreMatchedTemplates.Count == 0)
            {
                unambiguousTemplateGroup = null;
                return false;
            }

            if (CoreMatchedTemplates.Count == 1)
            {
                unambiguousTemplateGroup = new List<ITemplateMatchInfo>(CoreMatchedTemplates);
                return true;
            }

            if (!_hasUserInputLanguage)
            {
                // only consider default language match dispositions if the user did not specify a language.
                List<ITemplateMatchInfo> defaultLanguageMatchedTemplates = CoreMatchedTemplates.Where(x => x.DispositionOfDefaults
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

            List<ITemplateMatchInfo> paramFiltered = CoreMatchedTemplates.Where(x => !x.HasParameterMismatch()).ToList();

            if (TemplateListResolver.AreAllTemplatesSameGroupIdentity(paramFiltered))
            {
                unambiguousTemplateGroup = paramFiltered;
                return true;
            }

            if (TemplateListResolver.AreAllTemplatesSameGroupIdentity(CoreMatchedTemplates))
            {
                unambiguousTemplateGroup = new List<ITemplateMatchInfo>(CoreMatchedTemplates);
                return true;
            }

            unambiguousTemplateGroup = null;
            return false;
        }

        public bool TryGetAllInvokableTemplates(out IReadOnlyList<ITemplateMatchInfo> invokableTemplates)
        {
            IEnumerable<ITemplateMatchInfo> invokableMatches = CoreMatchedTemplates.Where(x => x.IsInvokableMatch());

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
            IReadOnlyList<ITemplateMatchInfo> invokableMatches = CoreMatchedTemplates.Where(x => x.IsInvokableMatch()).ToList();
            if (invokableMatches.Count == 1)
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
    }
}
