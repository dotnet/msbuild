// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.UnitTests
{
    internal static class Assertion
    {
        internal static void AssertEquals<T>(T expected, T actual) => Xunit.Assert.Equal(expected, actual);
        internal static void AssertEquals<T>(string message, T expected, T actual) => Xunit.Assert.True(expected.Equals(actual), message);

        internal static void Assert(bool condition, string message) => Xunit.Assert.True(condition, message);
        internal static void Assert(string message, bool condition) => Xunit.Assert.True(condition, message);
        internal static void Assert(bool condition) => Xunit.Assert.True(condition);

        internal static void Fail(string message) => Xunit.Assert.True(false, message);

        internal static void AssertNull(object obj) => Xunit.Assert.Null(obj);

        internal static void AssertNotNull(object obj) => Xunit.Assert.NotNull(obj);
        internal static void AssertNotNull(string message, object obj) => Xunit.Assert.False(obj is null, message);

        internal static void AssertSame(object first, object second) => Xunit.Assert.Same(first, second);
    }
}
