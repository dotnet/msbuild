// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplateFiltering;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Cli.TemplateResolution
{
    /// <summary>
    /// The class represents the template resolution result done by <see cref="BaseTemplateResolver"/> based on command input.
    /// Before template is resolved all installed templates are grouped by template group ID. Templates in single group:<br/>
    /// - should have different template identity <br/>
    /// - same short name (however different short names are also supported) <br/>
    /// - the templates may have different languages and types <br/>
    /// - the templates should have different precedence value in case same language is used <br/>
    /// - the templates in the group may have different parameters and different choices for parameter symbols defined.<br/>
    /// </summary>
    internal class TemplateResolutionResult
    {
        private readonly IReadOnlyList<TemplateGroupMatchInfo> _matchInformation;
        private readonly IReadOnlyList<TemplateGroupMatchInfo> _matchedTemplateGroups;
        private readonly BaseTemplateResolver _resolver;
        private Status _singularInvokableMatchStatus = Status.NotEvaluated;
        private (ITemplateInfo Template, IReadOnlyDictionary<string, string?> Parameters)? _templateToInvoke;
        private TemplateGroupMatchInfo? _unambiguousTemplateGroup;
        private TemplateGroupStatus _unambigiousTemplateGroupStatus = TemplateGroupStatus.NotEvaluated;

        internal TemplateResolutionResult(IEnumerable<TemplateGroupMatchInfo> matchInformation, BaseTemplateResolver resolver)
        {
            _matchInformation = matchInformation.ToList();
            _matchedTemplateGroups = _matchInformation.Where(groupMatchInfo => groupMatchInfo.IsGroupMatch).ToList();
            _resolver = resolver;
        }

        /// <summary>
        /// <see cref="Enum"/> defines possible statuses for resolving template to invoke.<br />
        /// </summary>
        internal enum Status
        {
            /// <summary>
            /// the status is not evaluated yet.
            /// </summary>
            NotEvaluated,

            /// <summary>
            /// no matched template groups were resolved.
            /// </summary>
            NoMatch,

            /// <summary>
            /// single template group and single template to use in the group is resolved.
            /// </summary>
            SingleMatch,

            /// <summary>
            /// multiple template groups were resolved; not possible to determine the group to use.
            /// </summary>
            AmbiguousTemplateGroupChoice,

            /// <summary>
            /// single template group was resolved, but there is an ambiguous choice for template inside the group and the templates are of same language. Usually means that the installed templates are conflicting and the conflict should be resolved by uninistalling some of templates.
            /// </summary>
            AmbiguousTemplateChoice,

            /// <summary>
            /// single template group was resolved, but there is an ambiguous choice for template inside the group with templates having different languages and the language was not selected by user and no default language match.
            /// </summary>
            AmbiguousLanguageChoice,

            /// <summary>
            /// single template group was resolved, but parameters or choice parameter values provided are invalid for all templates in the group.
            /// </summary>
            InvalidParameter
        }

        /// <summary>
        /// <see cref="Enum"/> defines possible statuses for unambiguous template group resolution.
        /// </summary>
        internal enum TemplateGroupStatus
        {
            /// <summary>
            /// the status is not evaluated yet.
            /// </summary>
            NotEvaluated,

            /// <summary>
            /// no matched template groups were resolved.
            /// </summary>
            NoMatch,

            /// <summary>
            /// single template group is resolved.
            /// </summary>
            SingleMatch,

            /// <summary>
            /// multiple template groups were resolved; not possible to determining the group to use.
            /// </summary>
            Ambiguous
        }

        /// <summary>
        /// Returns status of template resolution. <br />
        /// </summary>
        internal Status ResolutionStatus
        {
            get
            {
                if (_singularInvokableMatchStatus == Status.NotEvaluated)
                {
                    EvaluateTemplateToInvoke();
                }
                return _singularInvokableMatchStatus;
            }
        }

        /// <summary>
        /// Returns the template to invoke and parameters to be used or <see langword="null"/> if the template to invoke cannot be determined.
        /// Has value only when <see cref="Status" /> is <see cref="Status.SingleMatch"/>.
        /// </summary>
        internal (ITemplateInfo Template, IReadOnlyDictionary<string, string?> Parameters)? TemplateToInvoke
        {
            get
            {
                if (_singularInvokableMatchStatus == Status.NotEvaluated)
                {
                    EvaluateTemplateToInvoke();
                }
                return _templateToInvoke;
            }
        }

        /// <summary>
        /// Returns template groups that matches command input based on group filters applied (template info and template parameter filters are not considered in the match).
        /// </summary>
        internal IEnumerable<TemplateGroup> TemplateGroups
        {
            get
            {
                return _matchInformation.Where(groupMatchInfo => groupMatchInfo.IsGroupMatch).Select(groupMatchInfo => groupMatchInfo.GroupInfo);
            }
        }

        /// <summary>
        /// Returns template groups that matches command input based on group filters and template info filters applied (template parameters matches are not considered in the match).
        /// </summary>
        internal IEnumerable<TemplateGroup> TemplateGroupsWithMatchingTemplateInfo
        {
            get
            {
                return _matchInformation.Where(groupMatchInfo => groupMatchInfo.IsGroupAndTemplateInfoMatch).Select(groupMatchInfo => groupMatchInfo.GroupInfo);
            }
        }

        /// <summary>
        /// Returns template groups that matches command input based on group filters, template info filters and template parameters.
        /// </summary>
        internal IEnumerable<TemplateGroup> TemplateGroupsWithMatchingTemplateInfoAndParameters
        {
            get
            {
                return _matchInformation.Where(groupMatchInfo => groupMatchInfo.IsGroupAndTemplateInfoAndParametersMatch).Select(groupMatchInfo => groupMatchInfo.GroupInfo);
            }
        }

        /// <summary>
        /// Returns status of  template group resolution.
        /// </summary>
        internal TemplateGroupStatus GroupResolutionStatus
        {
            get
            {
                if (_unambigiousTemplateGroupStatus == TemplateGroupStatus.NotEvaluated)
                {
                    EvaluateUnambiguousTemplateGroup();
                }
                return _unambigiousTemplateGroupStatus;
            }
        }

        /// <summary>
        /// Returns unambiguous template group resolved; <c>null</c> if group cannot be resolved based on command input
        /// Has value only when <see cref="GroupResolutionStatus" /> is <see cref="TemplateGroupStatus.SingleMatch"/>.
        /// </summary>
        internal TemplateGroup? UnambiguousTemplateGroup
        {
            get
            {
                if (_unambigiousTemplateGroupStatus == TemplateGroupStatus.NotEvaluated)
                {
                    EvaluateUnambiguousTemplateGroup();
                }
                return _unambiguousTemplateGroup?.GroupInfo;
            }
        }

        /// <summary>
        /// Returns unambiguous template group resolved; <c>null</c> if group cannot be resolved based on command input
        /// Has value only when <see cref="GroupResolutionStatus" /> is <see cref="TemplateGroupStatus.SingleMatch"/>.
        /// </summary>
        internal TemplateGroupMatchInfo? UnambiguousTemplateGroupMatchInfo
        {
            get
            {
                if (_unambigiousTemplateGroupStatus == TemplateGroupStatus.NotEvaluated)
                {
                    EvaluateUnambiguousTemplateGroup();
                }
                return _unambiguousTemplateGroup;
            }
        }

        /// <summary>
        /// Returns true when at least one template has mismatch in language.
        /// </summary>
        internal bool HasLanguageMismatch => _matchedTemplateGroups.Any(groupMatchInfo => groupMatchInfo.TemplateMatchInfos.All(mi => mi.HasLanguageMismatch()));

        /// <summary>
        /// Returns true when at least one template has mismatch in context (type).
        /// </summary>
        internal bool HasTypeMismatch => _matchedTemplateGroups.Any(groupMatchInfo => groupMatchInfo.TemplateMatchInfos.Any(mi => mi.HasTypeMismatch()));

        /// <summary>
        /// Returns true when at least one template has mismatch in baseline.
        /// </summary>
        internal bool HasBaselineMismatch => _matchedTemplateGroups.Any(groupMatchInfo => groupMatchInfo.TemplateMatchInfos.Any(mi => mi.HasBaselineMismatch()));

        /// <summary>
        /// Returns true when at least one template has mismatch in author.
        /// </summary>
        internal bool HasAuthorMismatch => _matchedTemplateGroups.Any(groupMatchInfo => groupMatchInfo.TemplateMatchInfos.Any(mi => mi.HasAuthorMismatch()));

        /// <summary>
        /// Returns true when at least one template has mismatch in tags.
        /// </summary>
        internal bool HasClassificationMismatch => _matchedTemplateGroups.Any(groupMatchInfo => groupMatchInfo.TemplateMatchInfos.Any(mi => mi.HasClassificationMismatch()));

        /// <summary>
        /// Returns true when at least one template group has template that matches filters. Template parameters matches are not considered.
        /// </summary>
        internal bool HasTemplateGroupWithTemplateInfoMatches => TemplateGroupsWithMatchingTemplateInfo.Any();

        /// <summary>
        /// Returns count of groups that would be a match but failed on constraints.
        /// </summary>
        internal int ContraintsMismatchGroupCount => _matchedTemplateGroups.Count(groupMatchInfo => groupMatchInfo.TemplateMatchInfos.Any(mi => mi.HasMismatchOnConstraints()));

        /// <summary>
        /// Returns count of groups that would be a match but failed on filters.
        /// </summary>
        internal int ListFilterMismatchGroupCount => _matchedTemplateGroups.Count(groupMatchInfo => groupMatchInfo.TemplateMatchInfos.Any(mi => mi.HasMismatchOnListFilters()));

        /// <summary>
        /// Returns true when there is at least one template group that matches group filters. Template info filters and parameters matches are not considered.
        /// </summary>
        internal bool HasTemplateGroupMatches => TemplateGroups.Any();

        internal BaseTemplateResolver Resolver => _resolver;

        internal static IReadOnlyDictionary<string, string?> GetAllMatchedParametersList(IEnumerable<ITemplateMatchInfo> templateMatchInfos)
        {
            Dictionary<string, string?> parameterList = new();
            if (!templateMatchInfos.Any())
            {
                return parameterList;
            }
            foreach (ParameterMatchInfo parameterMatchInfo in templateMatchInfos.SelectMany(template => template.MatchDisposition.OfType<ParameterMatchInfo>()))
            {
                if (!string.IsNullOrWhiteSpace(parameterMatchInfo.Value) || !parameterList.ContainsKey(parameterMatchInfo.InputFormat ?? parameterMatchInfo.Name))
                {
                    parameterList[parameterMatchInfo.InputFormat ?? parameterMatchInfo.Name] = parameterMatchInfo.Value;
                }
            }
            return parameterList;
        }

        internal IReadOnlyDictionary<string, string?> GetAllMatchedParametersList()
        {
            return GetAllMatchedParametersList(
                _matchInformation
                    .Where(group => group.IsGroupAndTemplateInfoMatch) //this is needed as if the template group is not a match on group and info filaters, the parameter matches are not evaluated
                    .SelectMany(group => group.TemplateMatchInfos));
        }

        internal bool IsParameterMismatchReason(string parameterName)
        {
            foreach (var templateGroup in _matchedTemplateGroups)
            {
                if (templateGroup.TemplateMatchInfos.All(
                    templateMatchInfo =>
                        templateMatchInfo.MatchDisposition.Any(
                            matchInfo =>
                                matchInfo.GetType() == typeof(ParameterMatchInfo)
                                 && matchInfo.Name.Equals(parameterName, StringComparison.OrdinalIgnoreCase)
                                 && matchInfo.Kind == MatchKind.Mismatch)))
                {
                    return true;
                }
            }
            return false;
        }

        private void EvaluateTemplateToInvoke()
        {
            EvaluateUnambiguousTemplateGroup();
            switch (GroupResolutionStatus)
            {
                case TemplateGroupStatus.NotEvaluated:
                    throw new ArgumentException($"{nameof(GroupResolutionStatus)} should not be {nameof(TemplateGroupStatus.NotEvaluated)} after running {nameof(EvaluateUnambiguousTemplateGroup)}");
                case TemplateGroupStatus.NoMatch:
                    _singularInvokableMatchStatus = Status.NoMatch;
                    return;
                case TemplateGroupStatus.Ambiguous:
                    _singularInvokableMatchStatus = Status.AmbiguousTemplateGroupChoice;
                    return;
                case TemplateGroupStatus.SingleMatch:
                    if (_unambiguousTemplateGroup == null)
                    {
                        throw new ArgumentException($"{nameof(_unambiguousTemplateGroup)} should not be null if running {nameof(GroupResolutionStatus)} is {nameof(TemplateGroupStatus.SingleMatch)}");
                    }
                    //valid state to proceed
                    break;
                default:
                    throw new ArgumentException($"Unexpected value of {nameof(GroupResolutionStatus)}: {GroupResolutionStatus}.");
            }

            //if no templates are invokable there is a problem with parameter name or value - cannot resolve template to instantiate
            if (!_unambiguousTemplateGroup!.TemplateMatchInfosWithMatchingParameters.Any())
            {
                _singularInvokableMatchStatus = Status.InvalidParameter;
                return;
            }

            _templateToInvoke = _unambiguousTemplateGroup.GetTemplateToInvoke();
            if (_templateToInvoke != null)
            {
                _singularInvokableMatchStatus = Status.SingleMatch;
                return;
            }

            IEnumerable<ITemplateInfo> highestPrecedenceTemplates = _unambiguousTemplateGroup.GetHighestPrecedenceTemplates();
            IEnumerable<string?> templateLanguages = highestPrecedenceTemplates.Select(t => t.GetLanguage()).Distinct(StringComparer.OrdinalIgnoreCase);
            if (templateLanguages.Count() > 1)
            {
                _singularInvokableMatchStatus = Status.AmbiguousLanguageChoice;
                return;
            }
            _singularInvokableMatchStatus = Status.AmbiguousTemplateChoice;
        }

        private void EvaluateUnambiguousTemplateGroup()
        {
            if (!_matchInformation.Any(groupMatchInfo => groupMatchInfo.IsGroupMatch && groupMatchInfo.IsGroupAndTemplateInfoMatch))
            {
                _unambigiousTemplateGroupStatus = TemplateGroupStatus.NoMatch;
                return;
            }
            if (_matchInformation.Count(groupMatchInfo => groupMatchInfo.IsGroupMatch && groupMatchInfo.IsGroupAndTemplateInfoMatch) == 1)
            {
                _unambiguousTemplateGroup = _matchInformation.Single(groupMatchInfo => groupMatchInfo.IsGroupMatch && groupMatchInfo.IsGroupAndTemplateInfoMatch);
                _unambigiousTemplateGroupStatus = TemplateGroupStatus.SingleMatch;
                return;
            }
            _unambigiousTemplateGroupStatus = TemplateGroupStatus.Ambiguous;
        }
    }
}
