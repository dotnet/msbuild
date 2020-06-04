// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Evaluation;

using Xunit;
using Shouldly;

namespace Microsoft.Build.UnitTests.Evaluation
{
    public class SemiColonTokenizer_Tests
    {
        [Theory]
        [InlineData("", new string[0])]
        [InlineData(";", new string[0])]
        [InlineData(";;", new string[0])]
        [InlineData(" ; ; ", new string[0])]
        [InlineData("First", new string[] { "First" })]
        [InlineData("First;", new string[] { "First" })]
        [InlineData("First;Second", new string[] { "First", "Second" })]
        [InlineData("First;Second;Third", new string[] { "First", "Second", "Third" })]
        [InlineData(" First ;\tSecond\t;\nThird\n", new string[] { "First", "Second", "Third" })]
        [InlineData("@(foo->'xxx;xxx');@(foo, 'xxx;xxx');@(foo->'xxx;xxx', 'xxx;xxx')", new string[] { "@(foo->'xxx;xxx')", "@(foo, 'xxx;xxx')", "@(foo->'xxx;xxx', 'xxx;xxx')" })]
        public void TokenizeExpression(string expression, string[] expectedTokens)
        {
            new SemiColonTokenizer(expression).ShouldBe(expectedTokens);
        }
    }
}
