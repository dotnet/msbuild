// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplateFiltering;
using Microsoft.TemplateEngine.Cli.CommandParsing;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Cli.TemplateResolution
{
    internal static class CliFilters
    {
        /// <summary>
        /// Filters <see cref="TemplateGroup"/> by short name.
        /// The fields to be compared are <see cref="TemplateGroup.ShortNames"/> and they should exactly match user input.
        /// </summary>
        /// <param name="name">the name to match with group short names.</param>
        /// <returns></returns>
        internal static Func<TemplateGroup, MatchInfo?> ExactShortNameTemplateGroupFilter(string name)
        {
            return (templateGroup) =>
            {
                if (string.IsNullOrEmpty(name))
                {
                    return new MatchInfo(MatchInfo.BuiltIn.ShortName, name, MatchKind.Mismatch);
                }
                foreach (string shortName in templateGroup.ShortNames)
                {
                    if (shortName.Equals(name, StringComparison.OrdinalIgnoreCase))
                    {
                        return new MatchInfo(MatchInfo.BuiltIn.ShortName, name, MatchKind.Exact);
                    }
                }
                return new MatchInfo(MatchInfo.BuiltIn.ShortName, name, MatchKind.Mismatch);
            };
        }

        /// <summary>
        /// Filters <see cref="TemplateGroup"/> by name.
        /// The fields to be compared are <see cref="TemplateGroup.Name"/> and <see cref="TemplateGroup.ShortNames"/>.
        /// </summary>
        /// <param name="name">the name to match with template group name or short name.</param>
        /// <returns></returns>
        internal static Func<TemplateGroup, MatchInfo?> NameTemplateGroupFilter(string? name)
        {
            return (templateGroup) =>
            {
                if (string.IsNullOrEmpty(name))
                {
                    return new MatchInfo(MatchInfo.BuiltIn.Name, name, MatchKind.Partial);
                }

                int nameIndex = templateGroup.Name.IndexOf(name, StringComparison.CurrentCultureIgnoreCase);

                if (nameIndex == 0 && templateGroup.Name.Length == name.Length)
                {
                    return new MatchInfo(MatchInfo.BuiltIn.Name, name, MatchKind.Exact);
                }

                bool hasShortNamePartialMatch = false;

                foreach (string shortName in templateGroup.ShortNames)
                {
                    int shortNameIndex = shortName.IndexOf(name, StringComparison.OrdinalIgnoreCase);

                    if (shortNameIndex == 0 && shortName.Length == name.Length)
                    {
                        return new MatchInfo(MatchInfo.BuiltIn.ShortName, name, MatchKind.Exact);
                    }

                    hasShortNamePartialMatch |= shortNameIndex > -1;
                }

                if (nameIndex > -1)
                {
                    return new MatchInfo(MatchInfo.BuiltIn.Name, name, MatchKind.Partial);
                }

                if (hasShortNamePartialMatch)
                {
                    return new MatchInfo(MatchInfo.BuiltIn.ShortName, name, MatchKind.Partial);
                }

                return new MatchInfo(MatchInfo.BuiltIn.Name, name, MatchKind.Mismatch);
            };
        }

        /// <summary>
        /// Filters <see cref="TemplateGroup"/> by language.
        /// </summary>
        /// <param name="language">the language from command input.</param>
        /// <param name="defaultLanguage">the default language.</param>
        /// <returns></returns>
        internal static Func<TemplateGroup, MatchInfo?> LanguageGroupFilter(string? language, string? defaultLanguage)
        {
            return (templateGroup) =>
            {
                if (string.IsNullOrWhiteSpace(language) && string.IsNullOrWhiteSpace(defaultLanguage))
                {
                    return null;
                }
                IEnumerable<string?> templateLanguages = templateGroup.Languages;

                if (!string.IsNullOrWhiteSpace(language))
                {
                    // only add default language disposition when there is a language specified for the template.
                    if (templateLanguages.Any(lang => string.IsNullOrWhiteSpace(lang)))
                    {
                        return null;
                    }
                    if (templateLanguages.Contains(language, StringComparer.OrdinalIgnoreCase))
                    {
                        return new MatchInfo(MatchInfo.BuiltIn.Language, language, MatchKind.Exact);
                    }
                    else
                    {
                        return new MatchInfo(MatchInfo.BuiltIn.Language, language, MatchKind.Mismatch);
                    }
                }

                if (!string.IsNullOrWhiteSpace(defaultLanguage))
                {
                    if (templateLanguages.Contains(defaultLanguage, StringComparer.OrdinalIgnoreCase))
                    {
                        return new MatchInfo(MatchInfo.BuiltIn.Language, defaultLanguage, MatchKind.Exact);
                    }
                }
                if (templateLanguages.Count() == 1)
                {
                    //if only one language is defined, this is the language to be taken
                    return new MatchInfo(MatchInfo.BuiltIn.Language, language, MatchKind.Exact);
                }
                return null;
            };
        }

        /// <summary>
        /// Filters <see cref="ITemplateInfo"/> by template parameters read from <paramref name="commandInput"/>.
        /// </summary>
        internal static Func<ITemplateInfo, IEnumerable<MatchInfo>> TemplateParameterFilter(IHostSpecificDataLoader hostDataLoader, INewCommandInput commandInput)
        {
            return (template) =>
            {
                try
                {
                    TemplateCommandInput reparsedCommand = TemplateCommandInput.ParseForTemplate(template, commandInput, hostDataLoader.ReadHostSpecificTemplateData(template));
                    List<MatchInfo> matchInfos = new List<MatchInfo>();

                    // parameters are already parsed. But choice values aren't checked
                    foreach (KeyValuePair<string, string?> matchedParamInfo in reparsedCommand.InputTemplateParams)
                    {
                        string paramName = matchedParamInfo.Key;
                        string? paramValue = matchedParamInfo.Value;
                        MatchKind matchKind;
                        ParameterMatchInfo.MismatchKind mismatchKind = ParameterMatchInfo.MismatchKind.NoMismatch;

                        ITemplateParameter? paramDetails = template.Parameters.FirstOrDefault(param => param.Name.Equals(paramName));
                        if (paramDetails != null)
                        {
                            if (paramDetails.IsChoice() && paramDetails.Choices != null)
                            {
                                if (string.IsNullOrEmpty(paramValue)
                                    && !string.IsNullOrEmpty(paramDetails.DefaultIfOptionWithoutValue))
                                {
                                    // The user provided the parameter switch on the command line, without a value.
                                    // In this case, the DefaultIfOptionWithoutValue is the effective value.
                                    paramValue = paramDetails.DefaultIfOptionWithoutValue;
                                }

                                // key is the value user should provide, value is description
                                if (string.IsNullOrEmpty(paramValue))
                                {
                                    matchKind = MatchKind.Mismatch;
                                    mismatchKind = ParameterMatchInfo.MismatchKind.InvalidValue;
                                }
                                else if (paramDetails.Choices.ContainsKey(paramValue))
                                {
                                    matchKind = MatchKind.Exact;
                                }
                                else
                                {
                                    matchKind = MatchKind.Mismatch;
                                    mismatchKind = ParameterMatchInfo.MismatchKind.InvalidValue;
                                }
                            }
                            else // other parameter
                            {
                                matchKind = MatchKind.Exact;
                            }
                        }
                        else
                        {
                            matchKind = MatchKind.Mismatch;
                            mismatchKind = ParameterMatchInfo.MismatchKind.InvalidName;
                        }
                        matchInfos.Add(new ParameterMatchInfo(paramName, paramValue, matchKind, mismatchKind, reparsedCommand.TemplateParamInputFormat(paramName)));
                    }

                    foreach (string unmatchedParamName in reparsedCommand.RemainingParameters.Where(x => !x.Contains(':') && x.StartsWith('-'))) // filter debugging params and parameters that do not start with '-'
                    {
                        if (reparsedCommand.TryGetCanonicalNameForVariant(unmatchedParamName, out string? canonical) && !string.IsNullOrWhiteSpace(canonical))
                        {
                            // the name is a known template param, it must have not parsed due to an invalid value
                            // Note (scp 2017-02-27): This probably can't happen, the param parsing doesn't check the choice values.
                            matchInfos.Add(new ParameterMatchInfo(
                                canonical,
                                value: null,
                                MatchKind.Mismatch,
                                ParameterMatchInfo.MismatchKind.InvalidName,
                                reparsedCommand.TemplateParamInputFormat(unmatchedParamName)));
                        }
                        else
                        {
                            // the name is not known
                            // TODO: reconsider storing the canonical in this situation. It's not really a canonical since the param is unknown.
                            matchInfos.Add(new ParameterMatchInfo(
                                unmatchedParamName,
                                value: null,
                                MatchKind.Mismatch,
                                ParameterMatchInfo.MismatchKind.InvalidName,
                                unmatchedParamName));
                        }
                    }
                    return matchInfos;
                }
                catch (CommandParserException ex)
                {
                    string shortname = template.ShortNameList.Any() ? template.ShortNameList[0] : $"'{template.Name}'";
                    Reporter.Error.WriteLine(
                        string.Format(
                            LocalizableStrings.TemplateResolver_Warning_FailedToReparseTemplate,
                            $"{template.Identity} ({shortname})"));
                    Reporter.Verbose.WriteLine(string.Format(LocalizableStrings.Generic_Details, ex.ToString()));
                }
                return Array.Empty<MatchInfo>();
            };
        }

        /// <summary>
        /// Filters <see cref="ITemplateInfo"/> by template parameters read from <paramref name="commandInput"/>.
        /// Unlike <see cref="TemplateParameterFilter(IHostSpecificDataLoader, INewCommandInput)"/>, consider template parameter
        /// a match when the template parameter exists in template definition, but given without value.
        /// This is needed for --list and --search that filters template by the supported parameters.
        /// </summary>
        internal static Func<ITemplateInfo, IEnumerable<MatchInfo>> ListTemplateParameterFilter(IHostSpecificDataLoader hostDataLoader, INewCommandInput commandInput)
        {
            return (template) =>
            {
                try
                {
                    //do not reparse if base command input doesn't have template parameters
                    if (!commandInput.RemainingParameters.Any())
                    {
                        return Array.Empty<MatchInfo>();
                    }
                    TemplateCommandInput reparsedCommand = TemplateCommandInput.ParseForTemplate(template, commandInput, hostDataLoader.ReadHostSpecificTemplateData(template));

                    List<MatchInfo> matchInfos = new List<MatchInfo>();
                    foreach (KeyValuePair<string, string?> matchedParamInfo in reparsedCommand.InputTemplateParams)
                    {
                        string paramName = matchedParamInfo.Key;
                        string? paramValue = matchedParamInfo.Value;
                        MatchKind matchKind;
                        ParameterMatchInfo.MismatchKind mismatchKind = ParameterMatchInfo.MismatchKind.NoMismatch;
                        ITemplateParameter? paramDetails = template.Parameters.FirstOrDefault(param => param.Name.Equals(paramName));
                        if (paramDetails != null)
                        {
                            if (paramDetails.IsChoice() && paramDetails.Choices != null)
                            {
                                // key is the value user should provide, value is description
                                if (string.IsNullOrWhiteSpace(paramValue))
                                {
                                    matchKind = MatchKind.Exact;
                                    paramValue = null;
                                }
                                else if (paramDetails.Choices.ContainsKey(paramValue))
                                {
                                    matchKind = MatchKind.Exact;
                                }
                                else
                                {
                                    matchKind = MatchKind.Mismatch;
                                    mismatchKind = ParameterMatchInfo.MismatchKind.InvalidValue;
                                }
                            }
                            else // other parameter
                            {
                                matchKind = MatchKind.Exact;
                                paramValue = null;
                            }
                        }
                        else
                        {
                            matchKind = MatchKind.Mismatch;
                            mismatchKind = ParameterMatchInfo.MismatchKind.InvalidName;
                        }
                        matchInfos.Add(new ParameterMatchInfo(paramName, paramValue, matchKind, mismatchKind, reparsedCommand.TemplateParamInputFormat(paramName)));
                    }

                    foreach (string unmatchedParamName in reparsedCommand.RemainingParameters.Where(x => !x.Contains(':') && x.StartsWith('-'))) // filter debugging params and parameters that do not start with '-'
                    {
                        if (reparsedCommand.TryGetCanonicalNameForVariant(unmatchedParamName, out string? canonical) && !string.IsNullOrWhiteSpace(canonical))
                        {
                            matchInfos.Add(new ParameterMatchInfo(
                                canonical,
                                value: null,
                                MatchKind.Mismatch,
                                ParameterMatchInfo.MismatchKind.InvalidName,
                                reparsedCommand.TemplateParamInputFormat(unmatchedParamName)));
                        }
                        else
                        {
                            // the name is not known
                            matchInfos.Add(new ParameterMatchInfo(
                                unmatchedParamName,
                                value: null,
                                MatchKind.Mismatch,
                                ParameterMatchInfo.MismatchKind.InvalidName,
                                unmatchedParamName));
                        }
                    }
                    return matchInfos;
                }
                catch (CommandParserException ex)
                {
                    string shortname = template.ShortNameList.Any() ? template.ShortNameList[0] : $"'{template.Name}'";
                    Reporter.Error.WriteLine(
                        string.Format(
                            LocalizableStrings.TemplateResolver_Warning_FailedToReparseTemplate,
                            $"{template.Identity} ({shortname})"));
                    Reporter.Verbose.WriteLine(string.Format(LocalizableStrings.Generic_Details, ex.ToString()));
                }
                return Array.Empty<MatchInfo>();
            };
        }
    }
}
