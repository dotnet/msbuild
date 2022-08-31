// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

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
