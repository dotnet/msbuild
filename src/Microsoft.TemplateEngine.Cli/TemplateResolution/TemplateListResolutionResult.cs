// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions.TemplateFiltering;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Cli.TemplateResolution
{
    /// <summary>
    /// The class represents the template resolution result for listing purposes.
    /// The resolution result is the list of templates to use.
    /// </summary>
    internal sealed class TemplateListResolutionResult
    {
        internal TemplateListResolutionResult(IReadOnlyCollection<ITemplateMatchInfo> coreMatchedTemplates)
        {
            _coreMatchedTemplates = coreMatchedTemplates;
        }

        private readonly IReadOnlyCollection<ITemplateMatchInfo> _coreMatchedTemplates;
        private IReadOnlyCollection<ITemplateMatchInfo> _exactMatchedTemplates;
        private IReadOnlyCollection<ITemplateMatchInfo> _partiallyMatchedTemplates;
        private IReadOnlyCollection<TemplateGroup> _exactMatchedTemplateGroups;
        private IReadOnlyCollection<TemplateGroup> _partiallyMatchedTemplateGroups;

        /// <summary>
        /// Returns list of exact or partially matched templates by name and exact match by language, filter, baseline (if specified in command paramaters).
        /// </summary>
        internal IReadOnlyCollection<ITemplateMatchInfo> ExactMatchedTemplates
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
        /// Returns list of exact or partially matched template groups by name and exact match by language, filter, baseline (if specified in command paramaters).
        /// </summary>
        internal IReadOnlyCollection<TemplateGroup> ExactMatchedTemplateGroups
        {
            get
            {
                if (_exactMatchedTemplateGroups == null)
                {
                    _exactMatchedTemplateGroups = ExactMatchedTemplates
                        .GroupBy(x => x.Info.GroupIdentity, x => !string.IsNullOrEmpty(x.Info.GroupIdentity), StringComparer.OrdinalIgnoreCase)
                        .Select(group => new TemplateGroup(group.ToList()))
                        .ToList();
                }
                return _exactMatchedTemplateGroups;
            }
        }

        /// <summary>
        /// Returns list of exact or partially matched templates by name and mismatch in any of the following: language, filter, baseline (if specified in command paramaters).
        /// </summary>
        internal IReadOnlyCollection<ITemplateMatchInfo> PartiallyMatchedTemplates
        {
            get
            {
                if (_partiallyMatchedTemplates == null)
                {
                    _partiallyMatchedTemplates = _coreMatchedTemplates.Where(t => (t.HasNameMatch() || t.HasShortNameMatch())
                                                                                  && t.MatchDisposition.Any(m => m.Kind == MatchKind.Mismatch)).ToList();
                }
                return _partiallyMatchedTemplates;
            }
        }

        /// <summary>
        ///  Returns list of exact or partially matched template groups by name and mismatch in any of the following: language, filter, baseline (if specified in command paramaters.
        /// </summary>
        internal IReadOnlyCollection<TemplateGroup> PartiallyMatchedTemplateGroups
        {
            get
            {
                if (_partiallyMatchedTemplateGroups == null)
                {
                    _partiallyMatchedTemplateGroups = PartiallyMatchedTemplates
                        .GroupBy(x => x.Info.GroupIdentity, x => !string.IsNullOrEmpty(x.Info.GroupIdentity), StringComparer.OrdinalIgnoreCase)
                        .Select(group => new TemplateGroup(group.ToList()))
                        .ToList();
                }
                return _partiallyMatchedTemplateGroups;
            }
        }

        /// <summary>
        /// Returns true when at least one template in unambiguous matches default language.
        /// </summary>
        internal bool HasUnambiguousTemplateGroupForDefaultLanguage => UnambiguousTemplatesForDefaultLanguage.Any();

        /// <summary>
        /// Returns collecion of templates from unamgibuous group that matches default language.
        /// </summary>
        internal IReadOnlyCollection<ITemplateMatchInfo> UnambiguousTemplatesForDefaultLanguage => UnambiguousTemplateGroup.Where(t => t.HasDefaultLanguageMatch()).ToList();

        /// <summary>
        /// Returns true when at least one template exactly or partially matched templates by name and exactly matched language, filter, baseline (if specified in command paramaters).
        /// </summary>
        internal bool HasExactMatches => ExactMatchedTemplates.Any();

        /// <summary>
        /// Returns true when at least one template exactly or partially matched templates by name but has mismatch in any of the following: language, filter, baseline (if specified in command paramaters).
        /// </summary>
        internal bool HasPartialMatches => PartiallyMatchedTemplates.Any();

        /// <summary>
        /// Returns true when at least one template has mismatch in language.
        /// </summary>
        // as we need to count errors per template group: for language we need to check template groups as templates in single group may have different language
        // and if one of them can match the filter, then the whole group matches the filter
        // if all templates in the group has language mismatch, then group has language mismatch
        internal bool HasLanguageMismatch => PartiallyMatchedTemplateGroups.Any(g => g.Templates.All(t => t.HasLanguageMismatch()));

        /// <summary>
        /// Returns true when at least one template has mismatch in context (type).
        /// </summary>
        internal bool HasTypeMismatch => PartiallyMatchedTemplates.Any(t => t.HasTypeMismatch());

        /// <summary>
        /// Returns true when at least one template has mismatch in baseline.
        /// </summary>
        internal bool HasBaselineMismatch => PartiallyMatchedTemplates.Any(t => t.HasBaselineMismatch());

        /// <summary>
        /// Returns true when at least one template has mismatch in author.
        /// </summary>
        internal bool HasAuthorMismatch => PartiallyMatchedTemplates.Any(t => t.HasAuthorMismatch());

        /// <summary>
        /// Returns true when at least one template has mismatch in tags.
        /// </summary>
        internal bool HasClassificationMismatch => PartiallyMatchedTemplates.Any(t => t.HasClassificationMismatch());

        /// <summary>
        /// Returns true when one and only one template has exact match.
        /// </summary>
        internal bool HasUnambiguousTemplateGroup => ExactMatchedTemplateGroups.Count == 1;

        /// <summary>
        /// Returns list of templates for unambiguous template group, otherwise empty list.
        /// </summary>
        internal IReadOnlyCollection<ITemplateMatchInfo> UnambiguousTemplateGroup => HasUnambiguousTemplateGroup ? ExactMatchedTemplates : new List<ITemplateMatchInfo>();

        /// <summary>
        /// Returns true if all the templates in unambiguous group have templates in same language.
        /// </summary>
        internal bool AllTemplatesInUnambiguousTemplateGroupAreSameLanguage
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
                    string language = template.Info.GetLanguage();
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
