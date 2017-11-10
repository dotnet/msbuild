using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Cli.UnitTests.TemplateResolutionTests
{
    internal static class ResolutionTestHelper
    {
        public static ICacheTag CreateTestCacheTag(string choice, string description = null, string defaultValue = null)
        {
            return new CacheTag(null,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { choice, description }
                },
                defaultValue);
        }

        public static ICacheTag CreateTestCacheTag(IReadOnlyList<string> choiceList, string description = null, string defaultValue = null)
        {
            Dictionary<string, string> choicesDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string choice in choiceList)
            {
                choicesDict.Add(choice, null);
            };

            return new CacheTag(null, choicesDict, defaultValue);
        }
    }
}
