// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests
{
    public class RegexMatchMacroTests : EndToEndTestBase
    {
        [Theory(DisplayName = nameof(RegexMatchTest))]
        [InlineData("TestAssets.TemplateWithRegexMatchMacro --name hello", "RegexMatchMacroPositive.json")]
        [InlineData("TestAssets.TemplateWithRegexMatchMacro --name there", "RegexMatchMacroNegative.json")]
        public void RegexMatchTest(string args, params string[] scripts)
        {
            Run(args, scripts);
        }
    }
}
