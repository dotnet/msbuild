// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;

namespace Microsoft.DotNet.Tools.Test.Utilities
{
    public static class StringAssertionsExtensions
    {
        private static string NormalizeLineEndings(string s)
        {
            return s.Replace("\r\n", "\n");
        }

        public static AndConstraint<StringAssertions> BeVisuallyEquivalentTo(this StringAssertions assertions, string expected, string because = "", params object[] becauseArgs)
        {
            Execute.Assertion
                .ForCondition(NormalizeLineEndings(assertions.Subject) == NormalizeLineEndings(expected))
                .BecauseOf(because, becauseArgs)
                .FailWith($"String \"{assertions.Subject}\" is not visually equivalent to expected string \"{expected}\".");

            return new AndConstraint<StringAssertions>(assertions);
        }

        public static AndConstraint<StringAssertions> ContainVisuallySameFragment(this StringAssertions assertions, string expected, string because = "", params object[] becauseArgs)
        {
            Execute.Assertion
                .ForCondition(NormalizeLineEndings(assertions.Subject).Contains(NormalizeLineEndings(expected)))
                .BecauseOf(because, becauseArgs)
                .FailWith($"String \"{assertions.Subject}\" does not contain visually same fragment string \"{expected}\".");

            return new AndConstraint<StringAssertions>(assertions);
        }
    }
}
