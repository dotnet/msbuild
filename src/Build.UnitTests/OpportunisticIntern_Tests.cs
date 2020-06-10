// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Text;
using System.IO;
using Microsoft.Build;
using Microsoft.Build.Shared;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.UnitTests
{
    public abstract class OpportunisticInternTestBase : IDisposable
    {
        protected TestEnvironment _env;

        public void Dispose()
        {
            _env.Dispose();
        }

        protected OpportunisticInternTestBase(ITestOutputHelper testOutput)
        {
            _env = TestEnvironment.Create(testOutput);
        }

        private static bool IsInternable(IInternable internable)
        {
            string i1 = OpportunisticIntern.InternableToString(internable);
            string i2 = OpportunisticIntern.InternableToString(internable);
            Assert.Equal(i1, i2); // No matter what, the same string value should return.
            return Object.ReferenceEquals(i1, i2);
        }

        private static void AssertInternable(IInternable internable)
        {
            Assert.True(IsInternable(internable));
        }

        private static void AssertInternable(StringBuilder sb)
        {
            AssertInternable(new StringBuilderInternTarget(sb));
        }

        private static string AssertInternable(char[] ch, int startIndex, int count)
        {
            var target = new CharArrayInternTarget(ch, startIndex, count);
            AssertInternable(target);
            Assert.Equal(target.Length, count);

            return target.ExpensiveConvertToString();
        }

        private static void AssertInternable(string value)
        {
            AssertInternable(new StringBuilder(value));
            AssertInternable(value.ToCharArray(), 0, value.ToCharArray().Length);
        }

        private static void AssertNotInternable(IInternable internable)
        {
            Assert.False(IsInternable(internable));
        }

        private static void AssertNotInternable(StringBuilder sb)
        {
            AssertNotInternable(new StringBuilderInternTarget(sb));
        }

        private static void AssertNotInternable(char[] ch)
        {
            AssertNotInternable(new CharArrayInternTarget(ch, ch.Length));
        }

        protected static void AssertNotInternable(string value)
        {
            AssertNotInternable(new StringBuilder(value));
            AssertNotInternable(value.ToCharArray());
        }

        /// <summary>
        /// Test interning segment of char array
        /// </summary>
        [Fact]
        public void SubArray()
        {
            var result = AssertInternable(new char[] { 'a', 't', 'r', 'u', 'e' }, 1, 4);

            Assert.Equal("true", result);
        }

        /// <summary>
        /// Test interning segment of char array
        /// </summary>
        [Fact]
        public void SubArray2()
        {
            var result = AssertInternable(new char[] { 'a', 't', 'r', 'u', 'e', 'x' }, 1, 4);

            Assert.Equal("true", result);
        }

        /// <summary>
        /// Unique strings should not be interned
        /// </summary>
        [Fact]
        public void NonInternableDummyGlobalVariable()
        {
            AssertNotInternable($"{MSBuildConstants.MSBuildDummyGlobalPropertyHeader}{new string('1', 100)}");
        }

        /// <summary>
        /// This is the list of hard-coded interns. They should report interned even though they are too small for normal interning.
        /// </summary>
        [Fact]
        public void KnownInternableTinyStrings()
        {
            AssertInternable("C#");
            AssertInternable("F#");
            AssertInternable("VB");
            AssertInternable("True");
            AssertInternable("TRUE");
            AssertInternable("Copy");
            AssertInternable("v4.0");
            AssertInternable("true");
            AssertInternable("FALSE");
            AssertInternable("false");
            AssertInternable("Debug");
            AssertInternable("Build");
            AssertInternable("''!=''");
            AssertInternable("AnyCPU");
            AssertInternable("Library");
            AssertInternable("MSBuild");
            AssertInternable("Release");
            AssertInternable("ResolveAssemblyReference");
        }

        /// <summary>
        /// Test a set of strings that are similar to each other
        /// </summary>
        [Fact]
        public void InternableDifferingOnlyByNthCharacter()
        {
            string test = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ01234567890!@#$%^&*()_+ABCDEFGHIJKLMNOPQRSTUVabcdefghijklmnopqrstuvwxyz0150";
            for (int i = 0; i < test.Length; ++i)
            {
                string mutated = test.Substring(0, i) + " " + test.Substring(i + 1);
                AssertInternable(mutated);
            }
        }

        /// <summary>
        /// Test The empty string
        /// </summary>
        [Fact]
        public void StringDotEmpty()
        {
            AssertInternable(String.Empty);
        }

        /// <summary>
        /// Test an empty string.
        /// </summary>
        [Fact]
        public void DoubleDoubleQuotes()
        {
            AssertInternable("");
        }
    }

    /// <summary>
    /// Tests the new (default) implementation of OpportunisticIntern.
    /// </summary>
    public class OpportunisticIntern_Tests : OpportunisticInternTestBase
    {
        public OpportunisticIntern_Tests(ITestOutputHelper testOutput)
            : base(testOutput)
        {
            OpportunisticIntern.ResetForTests();
        }
    }

    /// <summary>
    /// Tests the legacy implementation of OpportunisticIntern.
    /// </summary>
    public class OpportunisticInternLegacy_Tests : OpportunisticInternTestBase
    {
        public OpportunisticInternLegacy_Tests(ITestOutputHelper testOutput)
            : base(testOutput)
        {
            _env.SetEnvironmentVariable("MSBuildUseLegacyStringInterner", "1");
            OpportunisticIntern.ResetForTests();
        }

        /// <summary>
        /// The legacy implementation does not intern tiny strings unless they are on the hard-coded list.
        /// </summary>
        [Fact]
        public void NonInternableTinyString()
        {
            AssertNotInternable("1234");
        }
    }

    /// <summary>
    /// Tests the legacy implementation of OpportunisticIntern with simple concurrency enabled.
    /// </summary>
    public class OpportunisticInternLegacySimpleConcurrecy_Tests : OpportunisticInternTestBase
    {
        public OpportunisticInternLegacySimpleConcurrecy_Tests(ITestOutputHelper testOutput)
            : base(testOutput)
        {
            _env.SetEnvironmentVariable("MSBuildUseLegacyStringInterner", "1");
            _env.SetEnvironmentVariable("MSBuildUseSimpleInternConcurrency", "1");
            OpportunisticIntern.ResetForTests();
        }
    }
}
