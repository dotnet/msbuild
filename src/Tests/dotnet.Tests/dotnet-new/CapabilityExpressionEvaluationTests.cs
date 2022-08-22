// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.MSBuildEvaluation;
using Xunit;

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
        public void EvaluateCapaibilityExpression(string expression, string availableCapabilities, bool expectedResult)
        {
            IReadOnlyList<string> projectCapabilites = availableCapabilities.Split("|");
            Assert.Equal(expectedResult, CapabilityExpressionEvaluator.Evaluate(expression, projectCapabilites));

        }

        [Theory]
        [InlineData("Cap1 |", "Cap3")]
        [InlineData("(Cap1 | Cap2", "Cap1")]
        [InlineData("(Cap1 | Cap2) + ((Cap3 | Cap4)", "Cap3")]
        public void EvaluateCapaibilityExpression_ThrowsOnInvalidExpression(string expression, string availableCapabilities)
        {
            IReadOnlyList<string> projectCapabilites = availableCapabilities.Split("|");
            Assert.Throws<ArgumentException>(() => CapabilityExpressionEvaluator.Evaluate(expression, projectCapabilites));

        }

    }
}
