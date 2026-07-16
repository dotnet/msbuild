// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Collections;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;

#nullable disable

namespace Microsoft.Build.UnitTests.OM.Collections
{
    /// <summary>
    /// Tests for MSBuildNameIgnoreCaseComparer
    /// </summary>
    [TestClass]
    public class MSBuildNameIgnoreCaseComparer_Tests
    {
        /// <summary>
        /// Verify default comparer works on the whole string
        /// </summary>
        [MSBuildTestMethod]
        public void DefaultEquals()
        {
            Assert.IsTrue(MSBuildNameIgnoreCaseComparer.Default.Equals("FOO", "foo"));
            Assert.IsFalse(MSBuildNameIgnoreCaseComparer.Default.Equals("FOO", " FOO"));
            Assert.IsFalse(MSBuildNameIgnoreCaseComparer.Default.Equals("FOOA", "FOOB"));
            Assert.IsFalse(MSBuildNameIgnoreCaseComparer.Default.Equals("AFOO", "BFOO"));
            Assert.IsFalse(MSBuildNameIgnoreCaseComparer.Default.Equals("FOO", "FOO "));
            Assert.IsFalse(MSBuildNameIgnoreCaseComparer.Default.Equals("a", "b"));
            Assert.IsTrue(MSBuildNameIgnoreCaseComparer.Default.Equals("", ""));
            Assert.IsFalse(MSBuildNameIgnoreCaseComparer.Default.Equals("x", null));
            Assert.IsFalse(MSBuildNameIgnoreCaseComparer.Default.Equals(null, "x"));
            Assert.IsTrue(MSBuildNameIgnoreCaseComparer.Default.Equals((string)null, (string)null));
        }

        /// <summary>
        /// Compare real expressions
        /// </summary>
        [MSBuildTestMethod]
        public void MatchProperty()
        {
            MSBuildNameIgnoreCaseComparer comparer = MSBuildNameIgnoreCaseComparer.Default;
            PropertyDictionary<ProjectPropertyInstance> dictionary = new PropertyDictionary<ProjectPropertyInstance>(comparer);

            ProjectPropertyInstance p = ProjectPropertyInstance.Create("foo", "bar");

            dictionary.Set(p);

            string s = "$(foo)";
            ProjectPropertyInstance value = dictionary.GetProperty(s, 2, 4);

            Assert.IsTrue(Object.ReferenceEquals(p, value)); // "Should have returned the same object as was inserted"

            Assert.AreEqual(MSBuildNameIgnoreCaseComparer.Default.GetHashCode("foo"), comparer.GetHashCode(s, 2, 3));
        }

        /// <summary>
        /// Null
        /// </summary>
        [MSBuildTestMethod]
        public void Null1()
        {
            Assert.IsFalse(MSBuildNameIgnoreCaseComparer.Default.Equals("x", null));
        }

        /// <summary>
        /// Null
        /// </summary>
        [MSBuildTestMethod]
        public void Null2()
        {
            Assert.IsFalse(MSBuildNameIgnoreCaseComparer.Default.Equals(null, "x"));
        }

        /// <summary>
        /// Invalid start
        /// </summary>
        [MSBuildTestMethod]
        public void InvalidValue2()
        {
            Assert.ThrowsExactly<InternalErrorException>(() =>
            {
                MSBuildNameIgnoreCaseComparer.Default.Equals("x", "y", -1, 0);
            });
        }
        /// <summary>
        /// Invalid small end
        /// </summary>
        [MSBuildTestMethod]
        public void InvalidValue4()
        {
            Assert.ThrowsExactly<InternalErrorException>(() =>
            {
                MSBuildNameIgnoreCaseComparer.Default.Equals("x", "y", 0, -1);
            });
        }
        /// <summary>
        /// Invalid large end
        /// </summary>
        [MSBuildTestMethod]
        public void InvalidValue5()
        {
            Assert.ThrowsExactly<InternalErrorException>(() =>
            {
                MSBuildNameIgnoreCaseComparer.Default.Equals("x", "y", 0, 2);
            });
        }
        /// <summary>
        /// End past the end of other string
        /// </summary>
        [MSBuildTestMethod]
        public void EqualsEndPastEnd1()
        {
            Assert.IsTrue(MSBuildNameIgnoreCaseComparer.Default.Equals("bbb", "abbbaaa", 1, 3));
        }

        /// <summary>
        /// Same values means one char
        /// </summary>
        [MSBuildTestMethod]
        public void EqualsSameStartEnd1()
        {
            Assert.IsTrue(MSBuildNameIgnoreCaseComparer.Default.Equals("A", "babbbb", 1, 1));
        }

        /// <summary>
        /// Same values means one char
        /// </summary>
        [MSBuildTestMethod]
        public void EqualsSameStartEnd2()
        {
            Assert.IsTrue(MSBuildNameIgnoreCaseComparer.Default.Equals("b", "aabaa", 2, 1));
        }

        /// <summary>
        /// Same values means one char
        /// </summary>
        [MSBuildTestMethod]
        public void EqualsSameStartEnd3()
        {
            Assert.IsTrue(MSBuildNameIgnoreCaseComparer.Default.Equals("a", "ab", 0, 1));
        }

        /// <summary>
        /// Start at 0
        /// </summary>
        [MSBuildTestMethod]
        public void EqualsStartZero()
        {
            Assert.IsTrue(MSBuildNameIgnoreCaseComparer.Default.Equals("aab", "aabaa", 0, 3));
        }

        /// <summary>
        /// Default get hash code
        /// </summary>
        [MSBuildTestMethod]
        public void DefaultGetHashcode()
        {
            Assert.IsTrue(0 == MSBuildNameIgnoreCaseComparer.Default.GetHashCode((string)null));

            MSBuildNameIgnoreCaseComparer.Default.GetHashCode(""); // doesn't throw
            Assert.AreEqual(MSBuildNameIgnoreCaseComparer.Default.GetHashCode("aBc"), MSBuildNameIgnoreCaseComparer.Default.GetHashCode("AbC"));
        }

        /// <summary>
        /// Indexed get hashcode
        /// </summary>
        [MSBuildTestMethod]
        public void IndexedGetHashcode1()
        {
            MSBuildNameIgnoreCaseComparer comparer = MSBuildNameIgnoreCaseComparer.Default;

            comparer.GetHashCode(""); // does not crash

            Assert.IsTrue(0 == comparer.GetHashCode((string)null));
            Assert.AreEqual(comparer.GetHashCode("aBc"), comparer.GetHashCode("AbC"));
            Assert.AreEqual(comparer.GetHashCode("xyz", 0, 1), comparer.GetHashCode("x"));
        }

        /// <summary>
        /// Indexed get hashcode
        /// </summary>
        [MSBuildTestMethod]
        public void IndexedGetHashcode2()
        {
            MSBuildNameIgnoreCaseComparer comparer = MSBuildNameIgnoreCaseComparer.Default;

            Assert.AreEqual(comparer.GetHashCode("xyz", 1, 2), comparer.GetHashCode("YZ"));
        }

        /// <summary>
        /// Indexed get hashcode
        /// </summary>
        [MSBuildTestMethod]
        public void IndexedGetHashcode3()
        {
            MSBuildNameIgnoreCaseComparer comparer = MSBuildNameIgnoreCaseComparer.Default;

            Assert.AreEqual(comparer.GetHashCode("abcd", 0, 3), comparer.GetHashCode("abc"));
        }
    }
}
