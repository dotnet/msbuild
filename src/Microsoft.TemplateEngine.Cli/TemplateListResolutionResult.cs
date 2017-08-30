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

        public IReadOnlyList<IFilteredTemplateInfo> CoreMatchedTemplates { get; set; }

        public bool HasCoreMatchedTemplatesWithDisposition(Func<IFilteredTemplateInfo, bool> filter)
        {
            return CoreMatchedTemplates != null
                    && CoreMatchedTemplates.Any(filter);
        }

        public bool TryGetUnambiguousTemplateGroupToUse(out IReadOnlyList<IFilteredTemplateInfo> unambiguousTemplateGroup)
        {
            if (CoreMatchedTemplates.Count == 0)
            {
                unambiguousTemplateGroup = null;
                return false;
            }

            if (CoreMatchedTemplates.Count == 1)
            {
                unambiguousTemplateGroup = new List<IFilteredTemplateInfo>(CoreMatchedTemplates);
                return true;
            }

            if (!_hasUserInputLanguage)
            {
                // only consider default language match dispositions if the user did not specify a language.
                List<IFilteredTemplateInfo> defaultLanguageMatchedTemplates = CoreMatchedTemplates.Where(x => x.DispositionOfDefaults
                                                                            .Any(y => y.Location == MatchLocation.DefaultLanguage && y.Kind == MatchKind.Exact))
                                                                            .ToList();

                if (TemplateListResolver.AreAllTemplatesSameGroupIdentity(defaultLanguageMatchedTemplates))
                {
                    if (defaultLanguageMatchedTemplates.Any(x => !x.HasParameterMismatch))
                    {
                        unambiguousTemplateGroup = defaultLanguageMatchedTemplates.Where(x => !x.HasParameterMismatch).ToList();
                        return true;
                    }
                    else
                    {
                        unambiguousTemplateGroup = defaultLanguageMatchedTemplates;
                        return true;
                    }
                }
            }

            List<IFilteredTemplateInfo> paramFiltered = CoreMatchedTemplates.Where(x => !x.HasParameterMismatch).ToList();
            if (TemplateListResolver.AreAllTemplatesSameGroupIdentity(paramFiltered))
            {
                unambiguousTemplateGroup = paramFiltered;
                return true;
            }

            if (TemplateListResolver.AreAllTemplatesSameGroupIdentity(CoreMatchedTemplates))
            {
                unambiguousTemplateGroup = new List<IFilteredTemplateInfo>(CoreMatchedTemplates);
                return true;
            }

            unambiguousTemplateGroup = null;
            return false;
        }

        public bool TryGetAllInvokableTemplates(out IReadOnlyList<IFilteredTemplateInfo> invokableTemplates)
        {
            IEnumerable<IFilteredTemplateInfo> invokableMatches = CoreMatchedTemplates.Where(x => x.IsInvokableMatch);

            if (invokableMatches.Any())
            {
                invokableTemplates = invokableMatches.ToList();
                return true;
            }

            invokableTemplates = null;
            return false;
        }

        public bool TryGetSingularInvokableMatch(out IFilteredTemplateInfo template)
        {
            IReadOnlyList<IFilteredTemplateInfo> invokableMatches = CoreMatchedTemplates.Where(x => x.IsInvokableMatch).ToList();
            if (invokableMatches.Count() == 1)
            {
                template = invokableMatches.First();
                return true;
            }

            IFilteredTemplateInfo highestInGroupIfSingleGroup = TemplateListResolver.FindHighestPrecedenceTemplateIfAllSameGroupIdentity(invokableMatches);
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
