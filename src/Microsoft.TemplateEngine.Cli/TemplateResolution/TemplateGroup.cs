// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplateFiltering;
using Microsoft.TemplateEngine.Cli.HelpAndUsage;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Cli.TemplateResolution
{
    /// <summary>
    /// The class represents template group. Templates in single group:<br/>
    /// - should same group identity
    /// - should have different template identity <br/>
    /// - same short name (however different short names are also supported) <br/>
    /// - the templates may have different languages and types <br/>
    /// - the templates should have different precedence value in case same language is used <br/>
    /// - the templates in the group may have different parameters and different choices for parameter symbols defined<br/>
    /// In case the template does not have group identity defined it represents separate template group with single template.
    /// </summary>
    internal sealed class TemplateGroup
    {
        /// <param name="templates">the templates of the template group.</param>
        /// <exception cref="ArgumentNullException">when <paramref name="templates"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">when <paramref name="templates"/> is empty or don't have same <see cref="ITemplateInfo.GroupIdentity"/> defined.</exception>
        internal TemplateGroup(IEnumerable<ITemplateMatchInfo> templates)
        {
            _ = templates ?? throw new ArgumentNullException(paramName: nameof(templates));
            if (!templates.Any())
            {
                throw new ArgumentException(paramName: nameof(templates), message: "The templates collection cannot be empty");
            }

            try
            {
                //all templates in the group should have same group identity
                GroupIdentity = templates.Select(t => string.IsNullOrWhiteSpace(t.Info.GroupIdentity) ? null : t.Info.GroupIdentity)
                                            .Distinct(StringComparer.OrdinalIgnoreCase)
                                            .Single();
            }
            catch (InvalidOperationException)
            {
                throw new ArgumentException(paramName: nameof(templates), message: "The templates should have same group identity");
            }
            Templates = templates.ToList();
        }

        /// <summary>
        /// Group identity of template group. The value can be null if the template does not have group identity set.
        /// </summary>
        internal string? GroupIdentity { get; private set; }

        /// <summary>
        /// Returns the list of short names defined for templates in the group.
        /// In theory, template group templates can have different short names but they are treated equally.
        /// </summary>
        internal IReadOnlyList<string> ShortNames
        {
            get
            {
                HashSet<string> shortNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (ITemplateMatchInfo template in Templates)
                {
                    if (template.Info is TemplateInfoWithGroupShortNames groupAwareTemplate)
                    {
                        shortNames.UnionWith(groupAwareTemplate.GroupShortNameList);
                    }
                    else
                    {
                        shortNames.UnionWith(template.Info.ShortNameList);
                    }
                }
                return shortNames.ToList();
            }
        }

        /// <summary>
        /// Returns true when <see cref="GroupIdentity"/> is not <c>null</c> or empty.
        /// </summary>
        internal bool HasGroupIdentity => !string.IsNullOrWhiteSpace(GroupIdentity);

        /// <summary>
        /// Returns true when the template group has single template.
        /// </summary>
        internal bool HasSingleTemplate => Templates.Count == 1;

        /// <summary>
        /// Returns the enumerator to invokable templates in the group.
        /// </summary>
        internal IEnumerable<ITemplateMatchInfo> InvokableTemplates => Templates.Where(templates => templates.IsInvokableMatch());

        /// <summary>
        /// Returns the collection of templates in the group.
        /// </summary>
        internal IReadOnlyCollection<ITemplateMatchInfo> Templates { get; private set; }

        /// <summary>
        /// Returns the ambiguous <see cref="MatchKind.SingleStartsWith"/> parameters in invokable templates in the template group.
        /// </summary>
        /// <returns>the enumerator for ambiguous <see cref="MatchKind.SingleStartsWith"/> parameters in invokable templates in the template group.</returns>
        /// <remarks>The template group is not valid when there are at least one ambiguous <see cref="MatchKind.SingleStartsWith"/> parameters in invokable templates.</remarks>
        internal IEnumerable<InvalidParameterInfo> GetAmbiguousSingleStartsWithParameters()
        {
            var invalidParameterList = new List<InvalidParameterInfo>();
            HashSet<string> singleStartsWithParamNames = new HashSet<string>();
            foreach (ITemplateMatchInfo checkTemplate in InvokableTemplates)
            {
                //https://github.com/dotnet/templating/issues/2494
                //after tab completion is implemented we no longer will be using this match kind - only exact matches will be allowed
                IEnumerable<ParameterMatchInfo> singleStartParams = checkTemplate.MatchDisposition
                    .OfType<ParameterMatchInfo>()
#pragma warning disable CS0618 // Type or member is obsolete
                    .Where(x => x.Kind == MatchKind.SingleStartsWith);
#pragma warning restore CS0618 // Type or member is obsolete

                foreach (var singleStartParam in singleStartParams)
                {
                    if (!singleStartsWithParamNames.Add(singleStartParam.Name))
                    {
                        invalidParameterList.Add(new InvalidParameterInfo(
                                                InvalidParameterInfo.Kind.AmbiguousParameterValue,
                                                singleStartParam.InputFormat,
                                                singleStartParam.Value,
                                                singleStartParam.Name));
                    }
                }
            }
            return invalidParameterList.Distinct();
        }

        /// <summary>
        /// Returns the invalid template specific parameters for the template group.
        /// Invalid parameters can have: invalid name, invalid value (determined only for choice parameter symbols), ambiguous value (determined only for choice parameter symbols).
        /// </summary>
        /// <returns>The enumerator for invalid parameters in templates in the template group.</returns>
        internal IEnumerable<InvalidParameterInfo> GetInvalidParameterList()
        {
            List<InvalidParameterInfo> invalidParameterList = new List<InvalidParameterInfo>();

            //collect the parameters which have ambiguous value match in all templates in the template group
            IEnumerable<ParameterMatchInfo> ambiguousParametersForTemplates = Templates.SelectMany(template => template.MatchDisposition
                .OfType<ParameterMatchInfo>()
                //https://github.com/dotnet/templating/issues/2494
                //after tab completion is implemented we no longer will be using this match kind - only exact matches will be allowed
#pragma warning disable CS0618 // Type or member is obsolete
                .Where(x => x.Kind == MatchKind.AmbiguousValue))
#pragma warning restore CS0618 // Type or member is obsolete
                .Distinct(new OrdinalIgnoreCaseMatchInfoComparer());
            foreach (ParameterMatchInfo parameter in ambiguousParametersForTemplates)
            {
                invalidParameterList.Add(new InvalidParameterInfo(
                                                InvalidParameterInfo.Kind.AmbiguousParameterValue,
                                                parameter.InputFormat,
                                                parameter.Value,
                                                parameter.Name));
            }

            if (InvokableTemplates.Any())
            {
                //add the parameters that have single starts with match in several invokable templates in template group
                return invalidParameterList.Union(GetAmbiguousSingleStartsWithParameters()).ToList();
            }

            //collect the parameters with invalid names for all templates in the template group
            IEnumerable<ParameterMatchInfo> parametersWithInvalidNames = Templates.SelectMany(
                template => template.MatchDisposition
                    .OfType<ParameterMatchInfo>()
                    .Where(x => x.Kind == MatchKind.InvalidName)).Distinct(new OrdinalIgnoreCaseMatchInfoComparer());

            foreach (ParameterMatchInfo parameter in parametersWithInvalidNames)
            {
                if (Templates.All(
                    template => template.MatchDisposition
                        .OfType<ParameterMatchInfo>()
                        .Any(x => x.Kind == MatchKind.InvalidName
                                  && x.Name.Equals(parameter.Name, StringComparison.OrdinalIgnoreCase))))
                {
                    invalidParameterList.Add(new InvalidParameterInfo(
                                                InvalidParameterInfo.Kind.InvalidParameterName,
                                                parameter.InputFormat,
                                                parameter.Value,
                                                parameter.Name));
                }
            }

            //if there are templates which have a match for all template specific parameters, only they to be analyzed
            var filteredTemplates = Templates.Where(template => !template.HasInvalidParameterName());
            if (!filteredTemplates.Any())
            {
                filteredTemplates = Templates;
            }

            //collect the choice parameters with invalid values
            IEnumerable<ParameterMatchInfo> invalidParameterValuesForTemplates = filteredTemplates.SelectMany(
                template => template.MatchDisposition
                    .OfType<ParameterMatchInfo>()
                    .Where(x => x.Kind == MatchKind.InvalidValue))
                .Distinct(new OrdinalIgnoreCaseMatchInfoComparer());
            foreach (ParameterMatchInfo parameter in invalidParameterValuesForTemplates)
            {
                if (filteredTemplates.All(
                   template => template.MatchDisposition
                        .OfType<ParameterMatchInfo>()
                        .Any(x => x.Kind == MatchKind.InvalidValue
                                  && x.Name.Equals(parameter.Name, StringComparison.OrdinalIgnoreCase))))
                {
                    invalidParameterList.Add(new InvalidParameterInfo(
                                                InvalidParameterInfo.Kind.InvalidParameterValue,
                                                parameter.InputFormat,
                                                parameter.Value,
                                                parameter.Name));
                }
            }

            return invalidParameterList;
        }

        /// <summary>
        /// The method returns the single invokable template with highest precedence.
        /// </summary>
        /// <param name="highestPrecedenceTemplate">Contains the invokable template with highest precedence.</param>
        /// <param name="useDefaultLanguage">Defines if default language template should be preferred in case of ambiguity.</param>
        /// <returns>
        /// <c>true</c> when single invokable template with highest precedence can be defined.
        /// <c>false</c> otherwise.
        /// </returns>
        internal bool TryGetHighestPrecedenceInvokableTemplate(out ITemplateMatchInfo? highestPrecedenceTemplate, bool useDefaultLanguage = false)
        {
            highestPrecedenceTemplate = null;
            if (!InvokableTemplates.Any())
            {
                return false;
            }
            IEnumerable<ITemplateMatchInfo> highestPrecendenceTemplates = GetHighestPrecedenceInvokableTemplates(useDefaultLanguage);
            if (highestPrecendenceTemplates.Count() == 1)
            {
                highestPrecedenceTemplate = highestPrecendenceTemplates.First();
                return true;
            }
            return false;
        }

        /// <summary>
        /// The method returns the invokable templates with highest precedence.
        /// </summary>
        /// <param name="useDefaultLanguage">Defines if default language template should be preferred in case of ambiguity.</param>
        /// <returns>
        /// the enumerator of invokable templates with highest precedence.
        /// </returns>
        internal IEnumerable<ITemplateMatchInfo> GetHighestPrecedenceInvokableTemplates(bool useDefaultLanguage = false)
        {
            IEnumerable<ITemplateMatchInfo> highestPrecedenceTemplates;
            if (!InvokableTemplates.Any())
            {
                return new List<ITemplateMatchInfo>();
            }

            int highestPrecedence = InvokableTemplates.Max(t => t.Info.Precedence);
            highestPrecedenceTemplates = InvokableTemplates.Where(t => t.Info.Precedence == highestPrecedence);

            if (useDefaultLanguage && highestPrecedenceTemplates.Count() > 1)
            {
                IEnumerable<ITemplateMatchInfo> highestPrecedenceTemplatesForDefaultLanguage = highestPrecedenceTemplates.Where(t => t.HasDefaultLanguageMatch());
                if (highestPrecedenceTemplatesForDefaultLanguage.Any())
                {
                    return highestPrecedenceTemplatesForDefaultLanguage;
                }
            }
            return highestPrecedenceTemplates;
        }

        /// <summary>
        /// Gets the list of valid choices for <paramref name="parameter"/>.
        /// </summary>
        /// <param name="parameter">parameter canonical name.</param>
        /// <returns>the dictionary of valid choices and descriptions.</returns>
        internal IDictionary<string, ParameterChoice> GetValidValuesForChoiceParameter(string parameter)
        {
            Dictionary<string, ParameterChoice> validChoices = new Dictionary<string, ParameterChoice>();
            foreach (ITemplateMatchInfo template in Templates)
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
        /// Gets the list of ambiguous choices for <paramref name="parameter"/> for value <paramref name="value"/>.
        /// </summary>
        /// <param name="parameter">parameter canonical name.</param>
        /// <param name="value">ambiguous value for the parameter to return possible choices for.</param>
        /// <returns>the dictionary of possible choices and descriptions that are matching ambiguous input.</returns>
        internal Dictionary<string, ParameterChoice> GetAmbiguousValuesForChoiceParameter(string parameter, string value)
        {
            Dictionary<string, ParameterChoice> validChoices = new Dictionary<string, ParameterChoice>();
            foreach (ITemplateMatchInfo template in Templates)
            {
                ITemplateParameter? choiceParameter = template.Info.GetChoiceParameter(parameter);
                if (choiceParameter != null && choiceParameter.Choices != null)
                {
                    foreach (var choice in choiceParameter.Choices)
                    {
                        if (choice.Key.StartsWith(value, StringComparison.OrdinalIgnoreCase))
                        {
                            validChoices[choice.Key] = choice.Value;
                        }
                    }
                }
            }
            return validChoices;
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
