// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplateFiltering;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Cli.TemplateResolution
{
    /// <summary>
    /// Represents template group matched to command input done by the user.
    /// The template group is filtered by three groups of filters:
    ///  - group-based filters (short name/name and language at the moment)
    ///  - template info filters (type, baseline, tags)
    ///  - template option filters based on parameters defined in template.json.
    ///  The filters are executed in order above. If the filter provides no matched, the next groups of filters won't be executed.
    /// </summary>
    internal class TemplateGroupMatchInfo
    {
        private readonly TemplateGroup _templateGroup;
        private readonly IReadOnlyList<MatchInfo> _groupDispositions;
        private readonly IReadOnlyList<ITemplateMatchInfo> _templateMatchInfos;
        private bool _isTemplateMatchEvaluated;
        private bool _isTemplateParameterMatchEvaluated;

        private TemplateGroupMatchInfo(
            TemplateGroup templateGroup,
            IEnumerable<MatchInfo> groupDispositions,
            IEnumerable<ITemplateMatchInfo> templateDispositions,
            bool templateMatchEvaluated = true,
            bool templateParameterMatchEvaluated = true)
        {
            _templateGroup = templateGroup;
            _groupDispositions = groupDispositions.ToList();
            _templateMatchInfos = templateDispositions.ToList();
            _isTemplateMatchEvaluated = templateMatchEvaluated;
            _isTemplateParameterMatchEvaluated = templateParameterMatchEvaluated;
        }

        /// <summary>
        /// Returns information about the template group (without match information).
        /// </summary>
        internal TemplateGroup GroupInfo => _templateGroup;

        /// <summary>
        /// True when the group matches group filters.
        /// </summary>
        internal bool IsGroupMatch => _groupDispositions.All(x => x.Kind != MatchKind.Mismatch);

        /// <summary>
        /// True when the group matches group and template info filters.
        /// </summary>
        internal bool IsGroupAndTemplateInfoMatch => IsGroupMatch && IsGroupTemplateInfoMatch;

        /// <summary>
        /// True when the group matches group, template info, and template options filters.
        /// </summary>
        internal bool IsGroupAndTemplateInfoAndParametersMatch => IsGroupMatch && IsGroupTemplateInfoMatch && IsGroupTemplateParametersMatch;

        /// <summary>
        /// Returns the list of <see cref="MatchInfo"/> for the group-based filters.
        /// </summary>
        internal IReadOnlyList<MatchInfo> GroupMatchInfos
        {
            get
            {
                return _groupDispositions;
            }
        }

        /// <summary>
        /// Returns the list of <see cref="ITemplateMatchInfo"></see> for all templates in the group.
        /// </summary>
        internal IEnumerable<ITemplateMatchInfo> TemplateMatchInfos
        {
            get
            {
                if (!_isTemplateMatchEvaluated)
                {
                    throw new NotSupportedException("Template info matches were not evaluated.");
                }
                if (!IsGroupMatch)
                {
                    return Array.Empty<ITemplateMatchInfo>();
                }
                return _templateMatchInfos;
            }
        }

        /// <summary>
        /// Returns the list of <see cref="ITemplateMatchInfo"></see> for all templates that matches template info filters.
        /// </summary>
        internal IEnumerable<ITemplateMatchInfo> TemplateMatchInfosWithMatchingInfo
        {
            get
            {
                if (!_isTemplateMatchEvaluated)
                {
                    throw new NotSupportedException("Template info matches were not evaluated.");
                }
                if (!IsGroupMatch)
                {
                    return Array.Empty<ITemplateMatchInfo>();
                }
                return _templateMatchInfos.Where(template => TemplateInfoMatch(template));
            }
        }

        /// <summary>
        /// Returns the list of <see cref="ITemplateMatchInfo"></see> for all templates that matches template info filters and template options filters.
        /// </summary>
        internal IEnumerable<ITemplateMatchInfo> TemplateMatchInfosWithMatchingParameters
        {
            get
            {
                if (!_isTemplateMatchEvaluated)
                {
                    throw new NotSupportedException("Template info matches were not evaluated.");
                }
                if (!_isTemplateParameterMatchEvaluated)
                {
                    throw new NotSupportedException("Template parameters matches were not evaluated.");
                }
                if (!IsGroupMatch)
                {
                    return Array.Empty<ITemplateMatchInfo>();
                }
                return _templateMatchInfos.Where(template => TemplateInfoMatch(template) && TemplateParametersMatch(template));
            }
        }

        /// <summary>
        /// Returns the list of <see cref="ITemplateMatchInfo"></see> for all templates of preferred language that matches template info filters, template options filters.
        /// </summary>
        internal IEnumerable<ITemplateMatchInfo> TemplateMatchInfosWithMatchingParametersForPreferredLanguage
        {
            get
            {
                if (!_isTemplateMatchEvaluated)
                {
                    throw new NotSupportedException("Template info matches were not evaluated.");
                }
                if (!_isTemplateParameterMatchEvaluated)
                {
                    throw new NotSupportedException("Template parameters matches were not evaluated.");
                }
                if (!IsGroupMatch)
                {
                    return Array.Empty<ITemplateMatchInfo>();
                }
                return FilterTemplatesByPreferredLanguage(_templateMatchInfos.Where(template => TemplateInfoMatch(template) && TemplateParametersMatch(template)));
            }
        }

        /// <summary>
        /// Returns the list of <see cref="ITemplateInfo"></see> for all templates that matches template info filters.
        /// Same as <see cref="TemplateMatchInfosWithMatchingInfo"/>, but returns <see cref="ITemplateInfo"/> without match information.
        /// </summary>
        internal IEnumerable<ITemplateInfo> TemplatesWithMatchingInfo => TemplateMatchInfosWithMatchingInfo.Select(template => template.Info);

        /// <summary>
        /// Returns the list of <see cref="ITemplateInfo"></see> for all templates that matches template info filters and template options filters.
        /// Same as <see cref="TemplateMatchInfosWithMatchingParameters"/>, but returns <see cref="ITemplateInfo"/> without match information.
        /// </summary>
        internal IEnumerable<ITemplateInfo> TemplatesWithMatchingParameters => TemplateMatchInfosWithMatchingParameters.Select(template => template.Info);

        /// <summary>
        /// Returns the list of <see cref="ITemplateInfo"></see> for all templates of preferred language that matches template info filters, template options filters.
        /// Same as <see cref="TemplateMatchInfosWithMatchingParametersForPreferredLanguage"/>, but returns <see cref="ITemplateInfo"/> without match information.
        /// </summary>
        internal IEnumerable<ITemplateInfo> TemplatesWithMatchingParametersForPreferredLanguage
            => TemplateMatchInfosWithMatchingParametersForPreferredLanguage.Select(template => template.Info);

        private bool IsGroupTemplateInfoMatch => _isTemplateMatchEvaluated && _templateMatchInfos.Any(template => TemplateInfoMatch(template));

        private bool IsGroupTemplateParametersMatch => _isTemplateParameterMatchEvaluated && _templateMatchInfos.Any(template => TemplateParametersMatch(template));

        /// <summary>
        /// Applies filters to template group.
        /// </summary>
        /// <param name="group">The template group to apply filters to.</param>
        /// <param name="groupFilters">The group-based filters.</param>
        /// <param name="templateInfoFilters">The template info filters.</param>
        /// <param name="templateParametersFilter">The template parameters filters.</param>
        /// <returns><see cref="TemplateGroupMatchInfo"/> with match information for the filters applied.</returns>
        /// <remarks>
        /// Note that filters are applied in the order: group-based filters, template info filters and template parameters filters.
        /// If the filter group results in mismatch, the following filter groups won't be applied.
        /// </remarks>
        internal static TemplateGroupMatchInfo ApplyFilters(
            TemplateGroup group,
            IEnumerable<Func<TemplateGroup, MatchInfo?>> groupFilters,
            IEnumerable<Func<ITemplateInfo, MatchInfo?>> templateInfoFilters,
            Func<ITemplateInfo, IEnumerable<MatchInfo>>? templateParametersFilter = null)
        {
            List<MatchInfo> groupMatchDispositions = new List<MatchInfo>();
            foreach (Func<TemplateGroup, MatchInfo?>? filter in groupFilters)
            {
                MatchInfo? info = filter(group);
                if (info != null)
                {
                    groupMatchDispositions.Add(info);
                }
            }

            //if template group is not a match the further evaluation is skipped
            if (groupMatchDispositions.Any(x => x.Kind == MatchKind.Mismatch))
            {
                return new TemplateGroupMatchInfo(
                    group,
                    groupMatchDispositions,
                    Array.Empty<ITemplateMatchInfo>(),
                    templateMatchEvaluated: false,
                    templateParameterMatchEvaluated: false);
            }
#pragma warning disable CS0618 // Type or member is obsolete
            IReadOnlyCollection<ITemplateMatchInfo> templateMatchDispositions = TemplateListFilter.GetTemplateMatchInfo(group.Templates, x => true, templateInfoFilters.ToArray());
#pragma warning restore CS0618 // Type or member is obsolete

            //if template info is not a match the further evaluation is skipped
            if (templateMatchDispositions.All(template => !TemplateInfoMatch(template)) || templateParametersFilter == null)
            {
                return new TemplateGroupMatchInfo(
                    group,
                    groupMatchDispositions,
                    templateMatchDispositions,
                    templateParameterMatchEvaluated: false);
            }

            foreach (var template in templateMatchDispositions)
            {
                var parameterMatchInfos = templateParametersFilter(template.Info);
                foreach (var parameterMatchInfo in parameterMatchInfos)
                {
                    template.AddMatchDisposition(parameterMatchInfo);
                }
            }
            return new TemplateGroupMatchInfo(group, groupMatchDispositions, templateMatchDispositions.ToList());
        }

        /// <summary>
        /// Returns the template to be invoked based on the command input or <see langword="null"/> if template to invoke cannot be resolved.
        /// </summary>
        internal (ITemplateInfo Template, IReadOnlyDictionary<string, string?> Parameters)? GetTemplateToInvoke()
        {
            var templates = GetHighestPrecedenceTemplateMatchInfos();
            if (templates.Count() != 1)
            {
                return null;
            }
            return (templates.Single().Info, templates.Single().GetValidTemplateParameters());
        }

        /// <summary>
        /// Gets the list of valid choices for <paramref name="parameter"/>.
        /// </summary>
        /// <param name="parameter">parameter canonical name.</param>
        /// <returns>the dictionary of valid choices and descriptions.</returns>
        internal IDictionary<string, ParameterChoice> GetValidValuesForChoiceParameter(string parameter)
        {
            if (!_isTemplateParameterMatchEvaluated)
            {
                throw new NotSupportedException("Template parameter matches were not evaluated.");
            }
            Dictionary<string, ParameterChoice> validChoices = new Dictionary<string, ParameterChoice>();
            foreach (ITemplateMatchInfo template in _templateMatchInfos)
            {
                ITemplateParameter? choiceParameter = template.Info.GetChoiceParameter(parameter);
                if (choiceParameter != null && choiceParameter.Choices != null)
                {
                    foreach (var choice in choiceParameter.Choices)
                    {
                        validChoices[choice.Key] = choice.Value;
                    }
                }
            }
            return validChoices;
        }

        /// <summary>
        /// Gets the highest precedence templates in the group.
        /// The templates should match all filter groups: group-based, template-info and template parameters filters.
        /// The templates are filtered by preferred language.
        /// </summary>
        internal IEnumerable<ITemplateInfo> GetHighestPrecedenceTemplates()
        {
            return GetHighestPrecedenceTemplateMatchInfos().Select(matchInfo => matchInfo.Info);
        }

        private static bool TemplateInfoMatch(ITemplateMatchInfo templateMatchInfo)
        {
            return templateMatchInfo.MatchDisposition
                    .Where(match => match.GetType() != typeof(ParameterMatchInfo))
                    .All(match => match.Kind != MatchKind.Mismatch);
        }

        private static bool TemplateParametersMatch(ITemplateMatchInfo templateMatchInfo)
        {
            return templateMatchInfo.MatchDisposition
                .OfType<ParameterMatchInfo>()
                .All(match => match.Kind != MatchKind.Mismatch);
        }

        private IEnumerable<ITemplateMatchInfo> GetHighestPrecedenceTemplateMatchInfos()
        {
            if (!TemplateMatchInfosWithMatchingParameters.Any())
            {
                return new List<ITemplateMatchInfo>();
            }

            IEnumerable<ITemplateMatchInfo> templatesToCheck = FilterTemplatesByPreferredLanguage(TemplateMatchInfosWithMatchingParameters);

            int highestPrecedence = templatesToCheck.Max(t => t.Info.Precedence);
            return templatesToCheck.Where(t => t.Info.Precedence == highestPrecedence);
        }

        private IEnumerable<ITemplateMatchInfo> FilterTemplatesByPreferredLanguage(IEnumerable<ITemplateMatchInfo> templatesToFilter)
        {
            MatchInfo? languageMatch = _groupDispositions.SingleOrDefault(match => match.Name == MatchInfo.BuiltIn.Language);
            if ((languageMatch == null) || string.IsNullOrWhiteSpace(languageMatch.Value))
            {
                return templatesToFilter;
            }
            else
            {
                var templatesForPreferredLanguage = templatesToFilter.Where(
                    matchInfo => matchInfo.Info.GetLanguage()?.Equals(languageMatch.Value, StringComparison.OrdinalIgnoreCase) ?? false);
                if (!templatesForPreferredLanguage.Any())
                {
                    return templatesToFilter;
                }
                return templatesForPreferredLanguage;
            }
        }

        private class OrdinalIgnoreCaseMatchInfoComparer : IEqualityComparer<ParameterMatchInfo>
        {
            public bool Equals(ParameterMatchInfo? x, ParameterMatchInfo? y)
            {
                if (x is null && y is null)
                {
                    return true;
                }
                if (x is null || y is null)
                {
                    return false;
                }

                return x.Kind == y.Kind
                    && string.Equals(x.Name, y.Name, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(x.Value, y.Value, StringComparison.OrdinalIgnoreCase);
            }

            public int GetHashCode(ParameterMatchInfo obj)
            {
                return (obj.Name.ToLowerInvariant(), obj.Value?.ToLowerInvariant(), obj.Kind).GetHashCode();
            }
        }
    }
}
