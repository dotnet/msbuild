using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.TemplateResolution;
using Microsoft.TemplateEngine.Edge.Template;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Cli.UnitTests.TemplateResolutionTests
{
    internal static class ResolutionTestHelper
    {
        public static ICacheTag CreateTestCacheTag(string choice, string choiceDescription = null, string defaultValue = null, string defaultIfOptionWithoutValue = null)
        {
            return new CacheTag(null,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { choice, choiceDescription }
                },
                defaultValue,
                defaultIfOptionWithoutValue);
        }

        public static ICacheTag CreateTestCacheTag(IReadOnlyList<string> choiceList, string tagDescription = null, string defaultValue = null, string defaultIfOptionWithoutValue = null)
        {
            Dictionary<string, string> choicesDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string choice in choiceList)
            {
                choicesDict.Add(choice, null);
            };

            return new CacheTag(tagDescription, choicesDict, defaultValue, defaultIfOptionWithoutValue);
        }

        public static string DebugOutputForResolutionResult(TemplateResolutionResult matchResult, Func<ITemplateMatchInfo, bool> filter)
        {
            if (!matchResult.TryGetCoreMatchedTemplatesWithDisposition(filter, out IReadOnlyList<ITemplateMatchInfo> matchingTemplates))
            {
                return "No templates matched the filter";
            }

            StringBuilder builder = new StringBuilder(512);
            foreach (ITemplateMatchInfo templateMatchInfo in matchingTemplates)
            {
                builder.AppendLine($"Identity: {templateMatchInfo.Info.Identity}");
                foreach (MatchInfo disposition in templateMatchInfo.MatchDisposition)
                {
                    builder.AppendLine($"\t{disposition.Location.ToString()} = {disposition.Kind.ToString()}");
                }

                builder.AppendLine();
            }

            return builder.ToString();
        }
    }
}
