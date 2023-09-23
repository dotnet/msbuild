// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;

namespace Microsoft.NET.TestFramework.Assertions
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
                .ForCondition(string.Compare(NormalizeLineEndings(assertions.Subject), NormalizeLineEndings(expected), CultureInfo.CurrentCulture, CompareOptions.IgnoreSymbols) == 0)
                .BecauseOf(because, becauseArgs)
                .FailWith($"String \"{assertions.Subject}\" is not visually equivalent to expected string \"{expected}\".");

            return new AndConstraint<StringAssertions>(assertions);
        }

        public static AndConstraint<StringAssertions> BeVisuallyEquivalentToIfNotLocalized(this StringAssertions assertions, string expected, string because = "", params object[] becauseArgs)
        {
            if (!TestContext.IsLocalized())
            {
                return BeVisuallyEquivalentTo(assertions, expected, because, becauseArgs);
            }

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

        public static AndConstraint<StringAssertions> ContainVisuallySameFragmentIfNotLocalized(this StringAssertions assertions, string expected, string because = "", params object[] becauseArgs)
        {
            if (!TestContext.IsLocalized())
            {
                return ContainVisuallySameFragment(assertions, expected, because, becauseArgs);
            }

            return new AndConstraint<StringAssertions>(assertions);
        }
    }
}
