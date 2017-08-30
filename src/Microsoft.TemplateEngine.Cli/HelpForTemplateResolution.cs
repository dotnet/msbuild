// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Edge.Template;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli
{
    internal static class HelpForTemplateResolution
    {
        public static void DisplayInvalidParameters(IReadOnlyList<string> invalidParams)
        {
            if (invalidParams.Count > 0)
            {
                Reporter.Error.WriteLine(LocalizableStrings.InvalidInputSwitch.Bold().Red());
                foreach (string flag in invalidParams)
                {
                    Reporter.Error.WriteLine($"  {flag}".Bold().Red());
                }
            }
        }

        public static void DisplayParametersInvalidForSomeTemplates(IReadOnlyList<string> invalidParams)
        {
            if (invalidParams.Count > 0)
            {
                Reporter.Error.WriteLine(LocalizableStrings.PartialTemplateMatchSwitchesNotValidForAllMatches.Bold().Red());
                foreach (string flag in invalidParams)
                {
                    Reporter.Error.WriteLine($"  {flag}".Bold().Red());
                }
            }
        }

        // get a better name for this, it's unclear what it's doing.
        public static bool ShowTemplateNameMismatchHelp(string templateName, string context, TemplateListResolutionResult templateResolutionResult)
        {
            GetContextBasedAndOtherPartialMatches(templateResolutionResult, out IReadOnlyList<IReadOnlyList<IFilteredTemplateInfo>> contextProblemMatchGroups, out IReadOnlyList<IReadOnlyList<IFilteredTemplateInfo>> remainingPartialMatchGroups);
            return DisplayPartialNameMatchAndContextProblems(templateName, context, contextProblemMatchGroups, remainingPartialMatchGroups);
        }

        private static void GetContextBasedAndOtherPartialMatches(TemplateListResolutionResult templateResolutionResult, out IReadOnlyList<IReadOnlyList<IFilteredTemplateInfo>> contextProblemMatchGroups, out IReadOnlyList<IReadOnlyList<IFilteredTemplateInfo>> remainingPartialMatchGroups)
        {
            Dictionary<string, List<IFilteredTemplateInfo>> contextProblemMatches = new Dictionary<string, List<IFilteredTemplateInfo>>();
            Dictionary<string, List<IFilteredTemplateInfo>> remainingPartialMatches = new Dictionary<string, List<IFilteredTemplateInfo>>();

            // this filtering / grouping ignores language differences.
            foreach (IFilteredTemplateInfo template in templateResolutionResult.CoreMatchedTemplates)
            {
                if (template.MatchDisposition.Any(x => x.Location == MatchLocation.Context && x.Kind != MatchKind.Exact))
                {
                    if (!contextProblemMatches.TryGetValue(template.Info.GroupIdentity, out List<IFilteredTemplateInfo> templateGroup))
                    {
                        templateGroup = new List<IFilteredTemplateInfo>();
                        contextProblemMatches.Add(template.Info.GroupIdentity, templateGroup);
                    }

                    templateGroup.Add(template);
                }
                else if (template.MatchDisposition.Any(t => t.Location != MatchLocation.Context && t.Kind != MatchKind.Mismatch && t.Kind != MatchKind.Unspecified))
                {
                    if (!remainingPartialMatches.TryGetValue(template.Info.GroupIdentity, out List<IFilteredTemplateInfo> templateGroup))
                    {
                        templateGroup = new List<IFilteredTemplateInfo>();
                        remainingPartialMatches.Add(template.Info.GroupIdentity, templateGroup);
                    }

                    templateGroup.Add(template);
                }
            }

            contextProblemMatchGroups = contextProblemMatches.Values.ToList();
            remainingPartialMatchGroups = remainingPartialMatches.Values.ToList();
        }

        private static bool DisplayPartialNameMatchAndContextProblems(string templateName, string context, IReadOnlyList<IReadOnlyList<IFilteredTemplateInfo>> contextProblemMatchGroups, IReadOnlyList<IReadOnlyList<IFilteredTemplateInfo>> remainingPartialMatchGroups)
        {
            bool anythingReported = false;
            if (contextProblemMatchGroups.Count + remainingPartialMatchGroups.Count > 1)
            {
                Reporter.Error.WriteLine(string.Format(LocalizableStrings.AmbiguousInputTemplateName, templateName));
                anythingReported = true;
            }
            else if (contextProblemMatchGroups.Count + remainingPartialMatchGroups.Count == 0)
            {
                Reporter.Error.WriteLine(string.Format(LocalizableStrings.NoTemplatesMatchName, templateName));
                Reporter.Error.WriteLine();
                return true;
            }

            // scp (2017-08-28): not certain anything in this loop can happen anymore, due to the way filtering occurs.
            foreach (IReadOnlyList<IFilteredTemplateInfo> templateGroup in contextProblemMatchGroups)
            {
                // all templates in a group should have the same context & name
                if (templateGroup[0].Info.Tags != null && templateGroup[0].Info.Tags.TryGetValue("type", out ICacheTag typeTag))
                {
                    MatchInfo? matchInfo = WellKnownSearchFilters.ContextFilter(context)(templateGroup[0].Info);
                    if ((matchInfo?.Kind ?? MatchKind.Mismatch) == MatchKind.Mismatch)
                    {
                        Reporter.Error.WriteLine(string.Format(LocalizableStrings.TemplateNotValidGivenTheSpecifiedFilter, templateGroup[0].Info.Name).Bold().Red());
                        anythingReported = true;
                    }
                }
                else
                {   // this really shouldn't ever happen. But better to have a generic error than quietly ignore the partial match.
                    Reporter.Error.WriteLine(string.Format(LocalizableStrings.GenericPlaceholderTemplateContextError, templateGroup[0].Info.Name).Bold().Red());
                    anythingReported = true;
                }
            }

            if (remainingPartialMatchGroups.Count > 0)
            {
                Reporter.Error.WriteLine(LocalizableStrings.TemplateMultiplePartialNameMatches.Bold().Red());
                anythingReported = true;
            }

            Reporter.Error.WriteLine();
            return anythingReported;
        }

        // Returns a list of the parameter names that are invalid for every template in the input group.
        public static void GetParametersInvalidForTemplatesInList(IReadOnlyList<IFilteredTemplateInfo> templateList, out IReadOnlyList<string> invalidForAllTemplates, out IReadOnlyList<string> invalidForSomeTemplates)
        {
            IDictionary<string, int> invalidCounts = new Dictionary<string, int>();

            foreach (IFilteredTemplateInfo template in templateList)
            {
                foreach (string paramName in template.InvalidParameterNames)
                {
                    if (!invalidCounts.ContainsKey(paramName))
                    {
                        invalidCounts[paramName] = 1;
                    }
                    else
                    {
                        invalidCounts[paramName]++;
                    }
                }
            }

            invalidForAllTemplates = invalidCounts.Where(x => x.Value == templateList.Count).Select(x => x.Key).ToList();
            invalidForSomeTemplates = invalidCounts.Where(x => x.Value != templateList.Count).Select(x => x.Key).ToList();
        }
    }
}
