// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.CommandParsing;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Template;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Cli.TemplateResolution
{
    internal static class TemplateResolver
    {
        private static readonly IReadOnlyCollection<MatchLocation> NameFields = new HashSet<MatchLocation>
        {
            MatchLocation.Name,
            MatchLocation.ShortName
        };

        internal static void ParseTemplateArgs(ITemplateInfo templateInfo, IHostSpecificDataLoader hostDataLoader, INewCommandInput commandInput)
        {
            HostSpecificTemplateData hostData = hostDataLoader.ReadHostSpecificTemplateData(templateInfo);
            commandInput.ReparseForTemplate(templateInfo, hostData);
        }

        internal static bool AreAllTemplatesSameGroupIdentity(IEnumerable<ITemplateMatchInfo> templateList)
        {
            if (!templateList.Any())
            {
                return false;
            }

            return templateList.AllAreTheSame((x) => x.Info.GroupIdentity, StringComparer.OrdinalIgnoreCase);
        }

        private static bool IsTemplateHiddenByHostFile(ITemplateInfo templateInfo, IHostSpecificDataLoader hostDataLoader)
        {
            HostSpecificTemplateData hostData = hostDataLoader.ReadHostSpecificTemplateData(templateInfo);
            return hostData.IsHidden;
        }

        // This version relies on the commandInput being in the context desired - so the most recent parse would have to have been
        // for what wants to be validated, either:
        //  - not in the context of any template
        //  - in the context of a specific template.
        internal static bool ValidateRemainingParameters(INewCommandInput commandInput, out IReadOnlyList<string> invalidParams)
        {
            List<string> badParams = new List<string>();

            if (commandInput.RemainingParameters.Any())
            {
                foreach (string flag in commandInput.RemainingParameters.Keys)
                {
                    badParams.Add(flag);
                }
            }

            invalidParams = badParams;
            return !invalidParams.Any();
        }

        // This version is preferred, its clear which template the results are in the context of.
        internal static bool ValidateRemainingParameters(ITemplateMatchInfo template, out IReadOnlyList<string> invalidParams)
        {
            invalidParams = template.GetInvalidParameterNames();

            return !invalidParams.Any();
        }

        // Lists all the templates, unfiltered - except the ones hidden by their host file.
        internal static IReadOnlyCollection<ITemplateMatchInfo> PerformAllTemplatesQuery(IReadOnlyList<ITemplateInfo> templateInfo, IHostSpecificDataLoader hostDataLoader)
        {
            IReadOnlyList<ITemplateInfo> filterableTemplateInfo = SetupTemplateInfoWithGroupShortNames(templateInfo);

            IReadOnlyCollection<ITemplateMatchInfo> templates = TemplateListFilter.GetTemplateMatchInfo(
                filterableTemplateInfo,
                TemplateListFilter.PartialMatchFilter,
                CliNameFilter(string.Empty)
            )
            .Where(x => !IsTemplateHiddenByHostFile(x.Info, hostDataLoader)).ToList();

            return templates;
        }

        internal static TemplateResolutionResult GetTemplateResolutionResult(IReadOnlyList<ITemplateInfo> templateInfo, IHostSpecificDataLoader hostDataLoader, INewCommandInput commandInput, string? defaultLanguage)
        {
            IReadOnlyCollection<ITemplateMatchInfo> coreMatchedTemplates = PerformCoreTemplateQuery(templateInfo, hostDataLoader, commandInput, defaultLanguage);
            return new TemplateResolutionResult(commandInput.Language, coreMatchedTemplates);
        }

        internal static TemplateListResolutionResult GetTemplateResolutionResultForListOrHelp(IReadOnlyList<ITemplateInfo> templateInfo, IHostSpecificDataLoader hostDataLoader, INewCommandInput commandInput, string? defaultLanguage)
        {
            IReadOnlyCollection<ITemplateMatchInfo> coreMatchedTemplates;

            //we need different set of templates for help and list
            //for list we need to show all exact and partial names by name
            //for help if there is an exact match by shortname or name we need to show help for that exact template and also apply default language mapping in case language is not specified
            if (commandInput.IsListFlagSpecified)
            {
                coreMatchedTemplates = PerformCoreTemplateQueryForList(templateInfo, hostDataLoader, commandInput);
            }
            else
            {
                coreMatchedTemplates = PerformCoreTemplateQueryForHelp(templateInfo, hostDataLoader, commandInput, defaultLanguage);
            }
            return new TemplateListResolutionResult(coreMatchedTemplates);
        }

        internal static IReadOnlyCollection<ITemplateMatchInfo> PerformCoreTemplateQueryForList(IReadOnlyList<ITemplateInfo> templateInfo, IHostSpecificDataLoader hostDataLoader, INewCommandInput commandInput)
        {
            IReadOnlyList<ITemplateInfo> filterableTemplateInfo = SetupTemplateInfoWithGroupShortNames(templateInfo);

            // for list we also try to get match on template name in classification (tags). These matches only will be used if short name and name has a mismatch.
            // filter below only sets the exact or partial match if name matches the tag. If name doesn't match the tag, no match disposition is added to collection.

            var listFilters = new List<Func<ITemplateInfo, MatchInfo?>>()
            {
                CliNameFilter(commandInput.TemplateName)
            };
            listFilters.AddRange(SupportedFilterOptions.SupportedListFilters
                                    .OfType<TemplateFilterOption>()
                                    .Select(filter => filter.TemplateMatchFilter(commandInput)));

            IReadOnlyList<ITemplateMatchInfo> coreMatchedTemplates = TemplateListFilter.GetTemplateMatchInfo(
                filterableTemplateInfo,
                TemplateListFilter.PartialMatchFilter,
                listFilters.ToArray()
            )
            .Where(x => !IsTemplateHiddenByHostFile(x.Info, hostDataLoader)).ToList();

            AddParameterMatchingToTemplates(coreMatchedTemplates, hostDataLoader, commandInput);
            return coreMatchedTemplates;
        }

        internal static IReadOnlyCollection<ITemplateMatchInfo> PerformCoreTemplateQueryForHelp(IReadOnlyList<ITemplateInfo> templateInfo, IHostSpecificDataLoader hostDataLoader, INewCommandInput commandInput, string? defaultLanguage)
        {
            IReadOnlyList<ITemplateInfo> filterableTemplateInfo = SetupTemplateInfoWithGroupShortNames(templateInfo);
            IReadOnlyList<ITemplateMatchInfo> coreMatchedTemplates = TemplateListFilter.GetTemplateMatchInfo(
                filterableTemplateInfo,
                TemplateListFilter.PartialMatchFilter,
                CliNameFilter(commandInput.TemplateName),
                WellKnownSearchFilters.LanguageFilter(commandInput.Language),
                WellKnownSearchFilters.ContextFilter(commandInput.TypeFilter),
                WellKnownSearchFilters.BaselineFilter(commandInput.BaselineName)
            )
            .Where(x => !IsTemplateHiddenByHostFile(x.Info, hostDataLoader)).ToList();

            //for help if template name from CLI exactly matches the template name we should consider only that template
            IReadOnlyList<ITemplateMatchInfo> matchesWithExactDispositionsInNameFields = coreMatchedTemplates.Where(x => x.MatchDisposition.Any(y => NameFields.Contains(y.Location) && y.Kind == MatchKind.Exact)).ToList();
            if (matchesWithExactDispositionsInNameFields.Count > 0)
            {
                coreMatchedTemplates = matchesWithExactDispositionsInNameFields;
            }

            //for help we also need to match on default language if language was not specified as parameter
            if (string.IsNullOrEmpty(commandInput.Language) && !string.IsNullOrEmpty(defaultLanguage))
            {
                // default language matching only makes sense if the user didn't specify a language.
                AddDefaultLanguageMatchingToTemplates(coreMatchedTemplates, defaultLanguage!);
            }
            AddParameterMatchingToTemplates(coreMatchedTemplates, hostDataLoader, commandInput);
            return coreMatchedTemplates;
        }

        /// <summary>
        /// Performs filtering of provided template list for --search option. Filters applied: template name filter, --search option filters, template parameters filter.
        /// Only templates that exactly match the filters are returned.
        /// </summary>
        /// <param name="templateInfo">the list of templates to be filtered.</param>
        /// <param name="hostDataLoader">data of the host.</param>
        /// <param name="commandInput">new command data used in CLI.</param>
        /// <returns>filtered list of templates.</returns>
        internal static IReadOnlyCollection<ITemplateMatchInfo> PerformCoreTemplateQueryForSearch(IEnumerable<ITemplateInfo> templateInfo, IHostSpecificDataLoader hostDataLoader, INewCommandInput commandInput)
        {
            IReadOnlyList<ITemplateInfo> filterableTemplateInfo = SetupTemplateInfoWithGroupShortNames(templateInfo.ToList());
            List<Func<ITemplateInfo, MatchInfo?>> searchFilters = new List<Func<ITemplateInfo, MatchInfo?>>()
            {
                CliNameFilter(commandInput.TemplateName),
            };
            searchFilters.AddRange(SupportedFilterOptions.SupportedSearchFilters
                                    .OfType<TemplateFilterOption>()
                                    .Select(filter => filter.TemplateMatchFilter(commandInput)));

            IReadOnlyCollection<ITemplateMatchInfo> matchedTemplates = TemplateListFilter.GetTemplateMatchInfo(filterableTemplateInfo, TemplateListFilter.ExactMatchFilter, searchFilters.ToArray());

            AddParameterMatchingToTemplates(matchedTemplates, hostDataLoader, commandInput);
            return matchedTemplates.Where(t => t.IsInvokableMatch()).ToList();
        }

        /// <summary>
        /// Performs the filtering of installed templates for template instantiated.
        /// Filters applied: template name filter; language, type, classification and baseline filters. Only templates that match the filters are returned, no partial matches allowed.
        /// In case any templates in match above are matching name or short name exactly, only they are returned.
        /// The matches for default language and template specific parameters are added to the result.
        /// </summary>
        /// <param name="templateInfo">the list of templates to be filtered.</param>
        /// <param name="hostDataLoader">data of the host.</param>
        /// <param name="commandInput">new command data used in CLI.</param>
        /// <param name="defaultLanguage"></param>
        /// <returns>the collection of the templates with their match dispositions (<seealso cref="ITemplateMatchInfo"/>). The templates that do not match are not added to the collection.</returns>
        internal static IReadOnlyCollection<ITemplateMatchInfo> PerformCoreTemplateQuery(IReadOnlyList<ITemplateInfo> templateInfo, IHostSpecificDataLoader hostDataLoader, INewCommandInput commandInput, string? defaultLanguage)
        {
            IReadOnlyList<ITemplateInfo> filterableTemplateInfo = SetupTemplateInfoWithGroupShortNames(templateInfo);

            IReadOnlyCollection<ITemplateMatchInfo> templates = TemplateListFilter.GetTemplateMatchInfo(
                filterableTemplateInfo,
                TemplateListFilter.ExactMatchFilter,
                CliNameFilter(commandInput.TemplateName),
                WellKnownSearchFilters.LanguageFilter(commandInput.Language),
                WellKnownSearchFilters.ContextFilter(commandInput.TypeFilter),
                WellKnownSearchFilters.BaselineFilter(commandInput.BaselineName)
            )
            .Where(x => !IsTemplateHiddenByHostFile(x.Info, hostDataLoader)).ToList();

            //select only the templates which do not have mismatches
            //if any template has exact match for name - use those; otherwise partial name matches are also considered when resolving templates
            IReadOnlyList<ITemplateMatchInfo> matchesWithExactDispositionsInNameFields = templates.Where(x => x.MatchDisposition.Any(y => NameFields.Contains(y.Location) && y.Kind == MatchKind.Exact)).ToList();
            if (matchesWithExactDispositionsInNameFields.Count > 0)
            {
                templates = matchesWithExactDispositionsInNameFields;
            }

            if (!string.IsNullOrEmpty(defaultLanguage) && string.IsNullOrEmpty(commandInput.Language))
            {
                // add default language matches to the list
                // default language matching only makes sense if the user didn't specify a language.
                AddDefaultLanguageMatchingToTemplates(templates, defaultLanguage!);
            }

            //add specific template parameters matches to the list
            AddParameterMatchingToTemplates(templates, hostDataLoader, commandInput);

            return templates;
        }

        /// <summary>
        /// Adds match dispositions to the templates based on matches between the default language and the language defined in template.
        /// </summary>
        /// <param name="listToFilter">the templates to match.</param>
        /// <param name="language">default language.</param>
        private static void AddDefaultLanguageMatchingToTemplates(IReadOnlyCollection<ITemplateMatchInfo> listToFilter, string language)
        {
            if (string.IsNullOrEmpty(language))
            {
                return;
            }

            foreach (ITemplateMatchInfo template in listToFilter)
            {
                MatchKind matchKind;

                string templateLanguage = template.Info.GetLanguage();
                // only add default language disposition when there is a language specified for the template.
                if (string.IsNullOrWhiteSpace(templateLanguage))
                {
                    continue;
                }

                if (templateLanguage.Equals(language, StringComparison.OrdinalIgnoreCase))
                {
                    matchKind = MatchKind.Exact;
                }
                else
                {
                    matchKind = MatchKind.Mismatch;
                }

                template.AddDisposition(new MatchInfo
                {
                    Location = MatchLocation.DefaultLanguage,
                    Kind = matchKind
                });
            }
        }

        /// <summary>
        /// Adds match dispositions to the templates based on matches between the input parameters and the template specific parameters.
        /// </summary>
        /// <param name="templatesToFilter">the templates to match.</param>
        /// <param name="hostDataLoader"></param>
        /// <param name="commandInput">the command input used in CLI.</param>
        private static void AddParameterMatchingToTemplates(IReadOnlyCollection<ITemplateMatchInfo> templatesToFilter, IHostSpecificDataLoader hostDataLoader, INewCommandInput commandInput)
        {
            foreach (ITemplateMatchInfo template in templatesToFilter)
            {
                try
                {
                    ParseTemplateArgs(template.Info, hostDataLoader, commandInput);

                    // params are already parsed. But choice values aren't checked
                    foreach (KeyValuePair<string, string> matchedParamInfo in commandInput.InputTemplateParams)
                    {
                        string paramName = matchedParamInfo.Key;
                        string paramValue = matchedParamInfo.Value;

                        if (template.Info.Tags.TryGetValue(paramName, out ICacheTag paramDetails))
                        {
                            if (string.IsNullOrEmpty(paramValue)
                                && paramDetails is IAllowDefaultIfOptionWithoutValue paramDetailsWithNoValueDefault
                                && !string.IsNullOrEmpty(paramDetailsWithNoValueDefault.DefaultIfOptionWithoutValue))
                            {
                                // The user provided the param switch on the command line, without a value.
                                // In this case, the DefaultIfOptionWithoutValue is the effective value.
                                paramValue = paramDetailsWithNoValueDefault.DefaultIfOptionWithoutValue;
                            }

                            // key is the value user should provide, value is description
                            if (string.IsNullOrEmpty(paramValue))
                            {
                                template.AddDisposition(new MatchInfo
                                {
                                    Location = MatchLocation.OtherParameter,
                                    Kind = MatchKind.InvalidParameterValue,
                                    InputParameterName = paramName,
                                    ParameterValue = paramValue,
                                    InputParameterFormat = commandInput.TemplateParamInputFormat(paramName)
                                });
                            }
                            else if (paramDetails.Choices.ContainsKey(paramValue))
                            {
                                template.AddDisposition(new MatchInfo
                                {
                                    Location = MatchLocation.OtherParameter,
                                    Kind = MatchKind.Exact,
                                    InputParameterName = paramName,
                                    ParameterValue = paramValue,
                                    InputParameterFormat = commandInput.TemplateParamInputFormat(paramName)
                                });
                            }
                            else
                            {
                                int startsWithCount = paramDetails.Choices.Count(x => x.Key.StartsWith(paramValue, StringComparison.OrdinalIgnoreCase));
                                if (startsWithCount == 1)
                                {
                                    template.AddDisposition(new MatchInfo
                                    {
                                        Location = MatchLocation.OtherParameter,
                                        Kind = MatchKind.SingleStartsWith,
                                        InputParameterName = paramName,
                                        ParameterValue = paramValue,
                                        InputParameterFormat = commandInput.TemplateParamInputFormat(paramName)
                                    });
                                }
                                else if (startsWithCount > 1)
                                {
                                    template.AddDisposition(new MatchInfo
                                    {
                                        Location = MatchLocation.OtherParameter,
                                        Kind = MatchKind.AmbiguousParameterValue,
                                        InputParameterName = paramName,
                                        ParameterValue = paramValue,
                                        InputParameterFormat = commandInput.TemplateParamInputFormat(paramName)
                                    });
                                }
                                else
                                {
                                    template.AddDisposition(new MatchInfo
                                    {
                                        Location = MatchLocation.OtherParameter,
                                        Kind = MatchKind.InvalidParameterValue,
                                        InputParameterName = paramName,
                                        ParameterValue = paramValue,
                                        InputParameterFormat = commandInput.TemplateParamInputFormat(paramName)
                                    });
                                }
                            }
                        }
                        else if (template.Info.CacheParameters.ContainsKey(paramName))
                        {
                            template.AddDisposition(new MatchInfo
                            {
                                Location = MatchLocation.OtherParameter,
                                Kind = MatchKind.Exact,
                                InputParameterName = paramName,
                                ParameterValue = paramValue,
                                InputParameterFormat = commandInput.TemplateParamInputFormat(paramName)
                            });
                        }
                        else
                        {
                            template.AddDisposition(new MatchInfo
                            {
                                Location = MatchLocation.OtherParameter,
                                Kind = MatchKind.InvalidParameterName,
                                InputParameterName = paramName,
                                ParameterValue = paramValue,
                                InputParameterFormat = commandInput.TemplateParamInputFormat(paramName)
                            });
                        }
                    }

                    foreach (string unmatchedParamName in commandInput.RemainingParameters.Keys.Where(x => !x.Contains(':'))) // filter debugging params
                    {
                        if (commandInput.TryGetCanonicalNameForVariant(unmatchedParamName, out string canonical))
                        {
                            // the name is a known template param, it must have not parsed due to an invalid value
                            // Note (scp 2017-02-27): This probably can't happen, the param parsing doesn't check the choice values.
                            template.AddDisposition(new MatchInfo
                            {
                                Location = MatchLocation.OtherParameter,
                                Kind = MatchKind.InvalidParameterName,
                                InputParameterName = canonical,
                                InputParameterFormat = commandInput.TemplateParamInputFormat(unmatchedParamName)
                            });
                        }
                        else
                        {
                            // the name is not known
                            // TODO: reconsider storing the canonical in this situation. It's not really a canonical since the param is unknown.
                            template.AddDisposition(new MatchInfo
                            {
                                Location = MatchLocation.OtherParameter,
                                Kind = MatchKind.InvalidParameterName,
                                InputParameterName = unmatchedParamName,
                                InputParameterFormat = unmatchedParamName
                            });
                        }
                    }
                }
                catch (CommandParserException ex)
                {
                    // if we do actually throw, add a non-match
                    template.AddDisposition(new MatchInfo
                    {
                        Location = MatchLocation.Unspecified,
                        Kind = MatchKind.Unspecified,
                        AdditionalInformation = ex.Message
                    });
                }
                catch (Exception ex)
                {
                    template.AddDisposition(new MatchInfo
                    {
                        Location = MatchLocation.Unspecified,
                        Kind = MatchKind.Unspecified,
                        AdditionalInformation = $"Unexpected error: {ex.Message}"
                    });
                }
            }
        }

        /// <summary>
        /// The method makes all short names in template group available for matching for given template (using <see cref="TemplateInfoWithGroupShortNames"/> <see cref="ITemplateInfo"/> implementation).
        /// TODO: double-check if that is needed. If needed that should be replaced by TemplateGroup logic as:
        /// - group templates by template groups
        /// - match templates through filters by template group including this logic of sharing short names
        /// - create resolution result afterwards.
        /// </summary>
        /// <param name="templateList">the list of <see cref="ITemplateInfo"/> to process.</param>
        /// <returns></returns>
        private static IReadOnlyList<ITemplateInfo> SetupTemplateInfoWithGroupShortNames(IReadOnlyList<ITemplateInfo> templateList)
        {
            Dictionary<string, HashSet<string>> shortNamesByGroup = new Dictionary<string, HashSet<string>>();

            // get the short names lists for the groups
            foreach (ITemplateInfo template in templateList)
            {
                string effectiveGroupIdentity = !string.IsNullOrEmpty(template.GroupIdentity)
                    ? template.GroupIdentity
                    : template.Identity;

                if (!shortNamesByGroup.TryGetValue(effectiveGroupIdentity, out HashSet<string> shortNames))
                {
                    shortNames = new HashSet<string>();
                    shortNamesByGroup[effectiveGroupIdentity] = shortNames;
                }

                if (template is IShortNameList templateWithShortNameList)
                {
                    shortNames.UnionWith(templateWithShortNameList.ShortNameList);
                }
                else
                {
                    shortNames.Add(template.ShortName);
                }
            }

            // create the TemplateInfoWithGroupShortNames with the group short names
            List<TemplateInfoWithGroupShortNames> templateListWithGroupNames = new List<TemplateInfoWithGroupShortNames>();

            foreach (ITemplateInfo template in templateList)
            {
                string effectiveGroupIdentity = !string.IsNullOrEmpty(template.GroupIdentity)
                    ? template.GroupIdentity
                    : template.Identity;

                templateListWithGroupNames.Add(new TemplateInfoWithGroupShortNames(template, shortNamesByGroup[effectiveGroupIdentity]));
            }

            return templateListWithGroupNames;
        }

        /// <summary>
        /// Filter for <see cref="TemplateListFilter.GetTemplateMatchInfo"></see>.
        /// Filters <see cref="ITemplateInfo"/> by name. Requires <see cref="TemplateInfoWithGroupShortNames"/> implementation.
        /// The fields to be compared are <see cref="ITemplateInfo.Name"/> and <see cref="TemplateInfoWithGroupShortNames.GroupShortNameList"/>.
        /// Unlike <see cref="WellKnownSearchFilters.NameFilter(string)"/> the filter also matches other template group short names the template belongs to.
        /// </summary>
        /// <param name="name">the name to match with template name or short name.</param>
        /// <returns></returns>
        private static Func<ITemplateInfo, MatchInfo?> CliNameFilter(string name)
        {
            return (template) =>
            {
                var groupAwareTemplate = template as TemplateInfoWithGroupShortNames ?? throw new ArgumentException("CliNameFilter filter supports only TemplateInfoWithGroupShortNames templates");

                if (string.IsNullOrEmpty(name))
                {
                    return new MatchInfo { Location = MatchLocation.Name, Kind = MatchKind.Partial };
                }

                int nameIndex = groupAwareTemplate.Name.IndexOf(name, StringComparison.OrdinalIgnoreCase);

                if (nameIndex == 0 && groupAwareTemplate.Name.Length == name.Length)
                {
                    return new MatchInfo { Location = MatchLocation.Name, Kind = MatchKind.Exact };
                }

                bool hasShortNamePartialMatch = false;

                foreach (string shortName in groupAwareTemplate.GroupShortNameList)
                {
                    int shortNameIndex = shortName.IndexOf(name, StringComparison.OrdinalIgnoreCase);

                    if (shortNameIndex == 0 && shortName.Length == name.Length)
                    {
                        return new MatchInfo { Location = MatchLocation.ShortName, Kind = MatchKind.Exact };
                    }

                    hasShortNamePartialMatch |= shortNameIndex > -1;
                }

                if (nameIndex > -1)
                {
                    return new MatchInfo { Location = MatchLocation.Name, Kind = MatchKind.Partial };
                }

                if (hasShortNamePartialMatch)
                {
                    return new MatchInfo { Location = MatchLocation.ShortName, Kind = MatchKind.Partial };
                }

                return new MatchInfo { Location = MatchLocation.Name, Kind = MatchKind.Mismatch };
            };
        }

        /// <summary>
        /// In addition to <see cref="ITemplateInfo"/> the class contains <see cref="TemplateInfoWithGroupShortNames.GroupShortNameList"/> property which contains the short names of other templates in the template group.
        /// The class is used for template filtering using specific <see cref="CliNameFilter(string)"/> filter which takes into account the short names of template group when matching names.
        /// </summary>
        private class TemplateInfoWithGroupShortNames : ITemplateInfo, IShortNameList
        {
            private ITemplateInfo _parent;
            internal TemplateInfoWithGroupShortNames (ITemplateInfo source, IEnumerable<string> groupShortNameList)
            {
                _parent = source;
                GroupShortNameList = groupShortNameList.ToList();
            }

            public string Author => _parent.Author;

            public string Description => _parent.Description;

            public IReadOnlyList<string> Classifications => _parent.Classifications;

            public string DefaultName => _parent.DefaultName;

            public string Identity => _parent.Identity;

            public Guid GeneratorId => _parent.GeneratorId;

            public string GroupIdentity => _parent.GroupIdentity;

            public int Precedence => _parent.Precedence;

            public string Name => _parent.Name;

            public string ShortName => _parent.ShortName;

            public IReadOnlyList<string> ShortNameList
            {
                get
                {
                    if (_parent is IShortNameList sourceWithShortNameList)
                    {
                        return sourceWithShortNameList.ShortNameList;
                    }
                    return new List<string>();
                }
            }

            public IReadOnlyList<string> GroupShortNameList { get; } = new List<string>();

            public IReadOnlyDictionary<string, ICacheTag> Tags => _parent.Tags;

            public IReadOnlyDictionary<string, ICacheParameter> CacheParameters => _parent.CacheParameters;

            public IReadOnlyList<ITemplateParameter> Parameters => _parent.Parameters;

            public string MountPointUri => _parent.MountPointUri;

            public string ConfigPlace => _parent.ConfigPlace;

            public string LocaleConfigPlace => _parent.LocaleConfigPlace;

            public string HostConfigPlace => _parent.HostConfigPlace;

            public string ThirdPartyNotices => _parent.ThirdPartyNotices;

            public IReadOnlyDictionary<string, IBaselineInfo> BaselineInfo => _parent.BaselineInfo;

            public bool HasScriptRunningPostActions
            {
                get
                {
                    return _parent.HasScriptRunningPostActions;
                }
                set
                {
                    _parent.HasScriptRunningPostActions = value;
                }

            }
        }
    }
}
