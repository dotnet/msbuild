// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Build.UnitTests
{
    internal static class Assert
    {
        internal static void IsTrue(bool condition) => Xunit.Assert.True(condition);
        internal static void IsTrue(bool condition, string message) => Xunit.Assert.True(condition, message);

        internal static void IsNull(object obj) => Xunit.Assert.Null(obj);
        internal static void IsNull(object obj, string message) => Xunit.Assert.True(obj is null, message);

        internal static void IsFalse(bool condition) => Xunit.Assert.False(condition);
        internal static void IsFalse(bool condition, string message) => Xunit.Assert.False(condition, message);

        internal static void IsNotNull(object obj) => Xunit.Assert.NotNull(obj);
        internal static void IsNotNull(object obj, string message) => Xunit.Assert.False(obj is null, message);

        internal static void AreEqual<T>(T expected, T actual) => Xunit.Assert.Equal(expected, actual);
        internal static void AreEqual<T>(T expected, T actual, string message) => Xunit.Assert.True(expected.Equals(actual), message);

        internal static void AreNotEqual<T>(T expected, T actual) => Xunit.Assert.NotEqual(expected, actual);
        internal static void AreNotEqual<T>(T expected, T actual, string message) => Xunit.Assert.False(expected.Equals(actual), message);

        internal static void Ignore(string message) => Xunit.Assert.True(true, message);
    }
}
