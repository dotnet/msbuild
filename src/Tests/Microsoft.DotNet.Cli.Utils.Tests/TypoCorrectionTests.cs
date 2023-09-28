// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable


namespace Microsoft.DotNet.Cli.Utils.Tests
{
    public class TypoCorrectionTests
    {
        [InlineData("wbe", "web|webapp|wpf|install|uninstall", "web|wpf", "Levanshtein algorithm")]
        [InlineData("uninstal", "web|webapp|install|uninstall", "uninstall|install", "StartsWith & Contains")]
        [InlineData("console", "web|webapp|install|uninstall", "", "No matches")]
        [InlineData("blazor", "razor|pazor|blazorweb|blazorservice|uninstall|pizor", "blazorweb|blazorservice|razor|pazor", "StartsWith & Levanshtein algorithm")]
        [InlineData("blazor", "razor|pazor|pazors", "razor|pazor", "Levanshtein algorithm with shortest distance filtering")]
        [InlineData("con", "lacon|test|consoleweb|precon|uninstall|ponsole|pons", "consoleweb|lacon|precon|pons", "StartsWith & Contains & Levanshtein algorithm")]
        [InlineData("c", "lacon|test|consoleweb|preconsole|uninstall|ponsole|pons|ccs", "consoleweb|ccs", "StartsWith & Levanshtein algorithm")]
        [InlineData("c", "peacon|lecture|beacon", "", "No matches due to Contains restriction on input length")]
        [InlineData(
            "eac",
            "peac|lect|beac|zeac|dect|meac|qeac|aect|oeac|xeac|necte|geacy|gueac",
            "peac|beac|zeac|meac|qeac|oeac|xeac|geacy|gueac|lect",
            "Contains due to max number of suggestions restriction")]
        [InlineData(
            "eacy",
            "eacyy|eacyl|eacys|eacyt|eacyp|eacyzz|eacyqwe|eacyasd|eacyaa|eacynbv|eacyrfd|peacy|peacp",
            "eacyy|eacyl|eacys|eacyt|eacyp|eacyzz|eacyaa|eacyqwe|eacyasd|eacynbv",
            "StartsWith due to max number of suggestions restriction")]
        [Theory]
        public void TypoCorrection_BasicTest(string token, string possibleTokens, string expectedTokens, string checkedScenario)
        {
            TypoCorrection.GetSimilarTokens(possibleTokens.Split('|'), token)
                .Should().BeEquivalentTo(expectedTokens.Split('|', StringSplitOptions.RemoveEmptyEntries), checkedScenario);
        }
    }
}
