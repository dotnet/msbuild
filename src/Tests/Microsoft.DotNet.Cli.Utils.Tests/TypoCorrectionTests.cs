// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

#nullable enable

using FluentAssertions;
using Xunit;

namespace Microsoft.DotNet.Cli.Utils.Tests
{
    public class TypoCorrectionTests
    {
        [InlineData("wbe", "web|webapp|wpf|install|uninstall", "web|wpf")]
        [InlineData("uninstal", "web|webapp|install|uninstall", "uninstall")]
        [InlineData("console", "web|webapp|install|uninstall", "")]
        [Theory]
        public void TypoCorrection_BasicTest(string token, string possibleTokens, string expectedTokens)
        {
            TypoCorrection.GetSimilarTokens(possibleTokens.Split('|'), token)
                .Should().BeEquivalentTo(expectedTokens.Split('|', System.StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
