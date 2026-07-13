// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Evaluation;
using Shouldly;

#nullable disable

namespace Microsoft.Build.UnitTests.Evaluation
{
    [TestClass]
    public class SemiColonTokenizer_Tests
    {
        [MSBuildTestMethod]
        [DataRow("", new string[0])]
        [DataRow(";", new string[0])]
        [DataRow(";;", new string[0])]
        [DataRow(" ; ; ", new string[0])]
        [DataRow("First", new string[] { "First" })]
        [DataRow("First;", new string[] { "First" })]
        [DataRow("First;Second", new string[] { "First", "Second" })]
        [DataRow("First;Second;Third", new string[] { "First", "Second", "Third" })]
        [DataRow(" First ;\tSecond\t;\nThird\n", new string[] { "First", "Second", "Third" })]
        [DataRow("@(foo->'xxx;xxx');@(foo, 'xxx;xxx');@(foo->'xxx;xxx', 'xxx;xxx')", new string[] { "@(foo->'xxx;xxx')", "@(foo, 'xxx;xxx')", "@(foo->'xxx;xxx', 'xxx;xxx')" })]
        public void TokenizeExpression(string expression, string[] expectedTokens)
        {
            new SemiColonTokenizer(expression).ShouldBe(expectedTokens);
        }
    }
}
