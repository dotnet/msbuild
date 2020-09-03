// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.TemplateEngine.Edge.Template;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Utils;
using System.Collections.ObjectModel;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli.TemplateResolution
{
    /// <summary>
    /// Class represents the template list resolution result to be used for listing or displaying help purposes
    /// </summary>
    public sealed class ListOrHelpTemplateListResolutionResult
    {
        public ListOrHelpTemplateListResolutionResult(IReadOnlyCollection<ITemplateMatchInfo> coreMatchedTemplates)
        {
            _coreMatchedTemplates = coreMatchedTemplates;
        }

        private readonly IReadOnlyCollection<ITemplateMatchInfo> _coreMatchedTemplates;
        private IReadOnlyCollection<ITemplateMatchInfo> _exactMatchedTemplates;
        private IReadOnlyCollection<ITemplateMatchInfo> _partiallyMatchedTemplates;

        /// <summary>
        /// Returns list of exact or partially matched templates by name and exact match by language, filter, baseline (if specified in command paramaters)
        /// </summary>
        public IReadOnlyCollection<ITemplateMatchInfo> ExactMatchedTemplates
        {
            get
            {
                if (_exactMatchedTemplates == null)
                {
                    // condition is required to filter matches on custom parameters
                    // TODO: when matching on custom parameters is refactored keep IsMatch condition only
                    if (_coreMatchedTemplates.Where(t => t.IsInvokableMatch()).Any())
                    {
                        _exactMatchedTemplates = _coreMatchedTemplates.Where(t => t.IsInvokableMatch()).ToList();
                    }
                    else
                    {
                        _exactMatchedTemplates = _coreMatchedTemplates.Where(t => t.IsMatch).ToList();
                    }
                }
                return _exactMatchedTemplates;
            }
        }

        /// <summary>
        /// Returns list of exact or partially matched templates by name and exact match by language, filter, baseline (if specified in command paramaters) grouped by group identity
        /// </summary>
        public IReadOnlyCollection<IGrouping<string, ITemplateMatchInfo>> ExactMatchedTemplatesGrouped =>
            ExactMatchedTemplates.GroupBy(x => x.Info.GroupIdentity, x => !string.IsNullOrEmpty(x.Info.GroupIdentity)).ToList();

        /// <summary>
        /// Returns list of exact or partially matched templates by name and mismatch in any of the following: language, filter, baseline (if specified in command paramaters)
        /// </summary>
        public IReadOnlyCollection<ITemplateMatchInfo> PartiallyMatchedTemplates
        {
            get
            {
                if (_partiallyMatchedTemplates == null)
                {
                    _partiallyMatchedTemplates = _coreMatchedTemplates.Where(t => t.HasNameMatchOrPartialMatch() && t.HasAnyMismatch()).ToList();
                }
                return _partiallyMatchedTemplates;
            }
        }

        /// <summary>
        ///  Returns list of exact or partially matched templates by name and mismatch in any of the following: language, filter, baseline (if specified in command paramaters); grouped by group identity
        /// </summary>
        public IReadOnlyCollection<IGrouping<string, ITemplateMatchInfo>> PartiallyMatchedTemplatesGrouped =>
            PartiallyMatchedTemplates.GroupBy(x => x.Info.GroupIdentity, x => !string.IsNullOrEmpty(x.Info.GroupIdentity)).ToList();


        /// <summary>
        /// Returns true when at least one template in unambiguous matches default language
        /// </summary>
        public bool HasUnambiguousTemplateGroupForDefaultLanguage => UnambiguousTemplatesForDefaultLanguage.Any();

        /// <summary>
        /// Returns collecion of templates from unamgibuous group that matches default language
        /// </summary>
        public IReadOnlyCollection<ITemplateMatchInfo> UnambiguousTemplatesForDefaultLanguage => UnambiguousTemplateGroup.Where(t => t.HasDefaultLanguageMatch()).ToList();

        /// <summary>
        /// Returns true when at least one template exactly or partially matched templates by name and exactly matched language, filter, baseline (if specified in command paramaters)
        /// </summary>
        public bool HasExactMatches => ExactMatchedTemplates.Any();

        /// <summary>
        /// Returns true when at least one template exactly or partially matched templates by name but has mismatch in any of the following: language, filter, baseline (if specified in command paramaters)
        /// </summary>
        public bool HasPartialMatches => PartiallyMatchedTemplates.Any();

        /// <summary>
        /// Returns true when at least one template has mismatch in language
        /// </summary>
        // as we need to count errors per template group: for language we need to check template groups as templates in single group may have different language
        // and if one of them can match the filter, then the whole group matches the filter
        // if all templates in the group has language mismatch, then group has language mismatch
        public bool HasLanguageMismatch => PartiallyMatchedTemplatesGrouped.Any(g => g.All(t => t.HasLanguageMismatch()));

        /// <summary>
        /// Returns true when at least one template has mismatch in context (type)
        /// </summary>
        public bool HasContextMismatch => PartiallyMatchedTemplates.Any(t => t.HasContextMismatch());

        /// <summary>
        /// Returns true when at least one template has mismatch in baseline
        /// </summary>
        public bool HasBaselineMismatch => PartiallyMatchedTemplates.Any(t => t.HasBaselineMismatch());

        /// <summary>
        /// Returns true when one and only one template has exact match
        /// </summary>
        public bool HasUnambiguousTemplateGroup => ExactMatchedTemplatesGrouped.Count == 1;

        /// <summary>
        /// Returns list of templates for unambiguous template group, otherwise empty list
        /// </summary>
        public IReadOnlyCollection<ITemplateMatchInfo> UnambiguousTemplateGroup => HasUnambiguousTemplateGroup ? ExactMatchedTemplates : new List<ITemplateMatchInfo>();

        /// <summary>
        /// Returns true if all the templates in unambiguous group have templates in same language
        /// </summary>
        public bool AllTemplatesInUnambiguousTemplateGroupAreSameLanguage
        {
            get
            {
                if (UnambiguousTemplateGroup.Count == 0)
                {
                    return false;
                }

                if (UnambiguousTemplateGroup.Count == 1)
                {
                    return true;
                }
                    
                HashSet<string> languagesFound = new HashSet<string>();
                foreach (ITemplateMatchInfo template in UnambiguousTemplateGroup)
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

                    if (!string.IsNullOrEmpty(language))
                    {
                        languagesFound.Add(language);
                    }

                    if (languagesFound.Count > 1)
                    {
                        return false;
                    }
                }
                return true;
            }
        }

    }
}
