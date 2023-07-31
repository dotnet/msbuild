// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.TemplateEngine.MSBuildEvaluation;

namespace Microsoft.DotNet.Cli.New.Tests
{
    public class CapabilityExpressionEvaluationTests 
    {
        [Theory]
        [InlineData("Capability", "", false)]
        [InlineData("Cap1", "cap1", true)]
        [InlineData("Cap1 | Cap2", "Cap3", false)]
        [InlineData("Cap1 | Cap2", "Cap1", true)]
        [InlineData("Cap1 & Cap2", "Cap3", false)]
        [InlineData("Cap1 & Cap2", "Cap1", false)]
        [InlineData("Cap1 & Cap2", "Cap1|Cap2", true)]
        [InlineData("Cap1 + Cap2", "Cap3", false)]
        [InlineData("Cap1 + Cap2", "Cap1", false)]
        [InlineData("Cap1 + Cap2", "Cap1|Cap2", true)]
        [InlineData("!Cap3", "Cap3", false)]
        [InlineData("!Cap3", "Cap1", true)]
        [InlineData("(Cap1 | Cap2) + (Cap3 | Cap4)", "Cap3", false)]
        [InlineData("(Cap1 | Cap2) + (Cap3 | Cap4)", "Cap3|Cap1", true)]
        [InlineData("(Cap1 | Cap2) | (Cap3 | Cap4)", "Cap3", true)]
        [InlineData("Cap1 | Cap2 + Cap3 | Cap4", "Cap3", false)]
        [InlineData("Cap1 | Cap2 + Cap3 | Cap4", "Cap3|Cap1", true)]
        [InlineData("Cap1 | Cap2 & Cap3 | Cap4", "Cap3", false)]
        [InlineData("Cap1 | Cap2 & Cap3 | Cap4", "Cap3|Cap1", true)]
        public void EvaluateCapabilityExpression(string expression, string availableCapabilities, bool expectedResult)
        {
            IReadOnlyList<string> projectCapabilites = availableCapabilities.Split("|");
            Assert.Equal(expectedResult, CapabilityExpressionEvaluator.Evaluate(expression, projectCapabilites));
        }

        [Theory]
        [InlineData("Cap1 |", "Cap3")]
        [InlineData("(Cap1 | Cap2", "Cap1")]
        [InlineData("(Cap1 | Cap2) + ((Cap3 | Cap4)", "Cap3")]
        public void EvaluateCapabilityExpression_ThrowsOnInvalidExpression(string expression, string availableCapabilities)
        {
            IReadOnlyList<string> projectCapabilites = availableCapabilities.Split("|");
            Assert.Throws<ArgumentException>(() => CapabilityExpressionEvaluator.Evaluate(expression, projectCapabilites));
        }
    }
}
