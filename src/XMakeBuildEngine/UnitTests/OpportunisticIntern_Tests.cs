// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Microsoft.Build;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Build.UnitTests
{
    [TestClass]
    public class OpportunisticIntern_Tests
    {
        private static bool IsInternable(OpportunisticIntern.IInternable internable)
        {
            string i1 = OpportunisticIntern.InternableToString(internable);
            string i2 = OpportunisticIntern.InternableToString(internable);
            Assert.AreEqual(i1, i2); // No matter what, the same string value should return.
            return Object.ReferenceEquals(i1, i2);
        }

        private static void AssertInternable(OpportunisticIntern.IInternable internable)
        {
            Assert.IsTrue(IsInternable(internable));
        }

        private static void AssertInternable(StringBuilder sb)
        {
            AssertInternable(new OpportunisticIntern.StringBuilderInternTarget(sb));
        }

        private static string AssertInternable(char[] ch, int startIndex, int count)
        {
            var target = new OpportunisticIntern.CharArrayInternTarget(ch, startIndex, count);
            AssertInternable(target);
            Assert.IsTrue(target.Length == count);

            return target.ExpensiveConvertToString();
        }

        private static void AssertInternable(string value)
        {
            AssertInternable(new StringBuilder(value));
            AssertInternable(value.ToCharArray(), 0, value.ToCharArray().Length);
        }

        private static void AssertNotInternable(OpportunisticIntern.IInternable internable)
        {
            Assert.IsFalse(IsInternable(internable));
        }

        private static void AssertNotInternable(StringBuilder sb)
        {
            AssertNotInternable(new OpportunisticIntern.StringBuilderInternTarget(sb));
        }

        private static void AssertNotInternable(char[] ch)
        {
            AssertNotInternable(new OpportunisticIntern.CharArrayInternTarget(ch, ch.Length));
        }

        private static void AssertNotInternable(string value)
        {
            AssertNotInternable(new StringBuilder(value));
            AssertNotInternable(value.ToCharArray());
        }

        /// <summary>
        /// Test interning segment of char array
        /// </summary>
        [TestMethod]
        public void SubArray()
        {
            var result = AssertInternable(new char[] { 'a', 't', 'r', 'u', 'e' }, 1, 4);

            Assert.AreEqual(result, "true");
        }

        /// <summary>
        /// Test interning segment of char array
        /// </summary>
        [TestMethod]
        public void SubArray2()
        {
            var result = AssertInternable(new char[] { 'a', 't', 'r', 'u', 'e', 'x' }, 1, 4);

            Assert.AreEqual(result, "true");
        }

        /// <summary>
        /// Test a single know-to-intern tiny string to verify the mechanism.
        /// </summary>
        [TestMethod]
        public void InternableTinyString()
        {
            AssertInternable("true");
        }

        /// <summary>
        /// Test a single known-to-not-intern tiny string to verify the mechanism.
        /// </summary>
        [TestMethod]
        public void NonInternableTinyString()
        {
            AssertNotInternable("1234");
        }

        /// <summary>
        /// This is the list of hard-coded interns. They should report interned even though they are too small for normal interning.
        /// </summary>
        [TestMethod]
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
        /// Test a set of strings that are similar to eachother
        /// </summary>
        [TestMethod]
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
        [TestMethod]
        public void StringDotEmpty()
        {
            AssertInternable(String.Empty);
        }

        /// <summary>
        /// Test an empty string.
        /// </summary>
        [TestMethod]
        public void DoubleDoubleQuotes()
        {
            AssertInternable("");
        }
    }
}
