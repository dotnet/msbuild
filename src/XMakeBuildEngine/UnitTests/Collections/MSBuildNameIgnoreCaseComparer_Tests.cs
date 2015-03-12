// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Tests the MSBuildNameIgnoreCaseComparer</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Build.Collections;
using Microsoft.Build.Shared;
using Microsoft.Build.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
        [TestMethod]
        public void DefaultEquals()
        {
            Assert.AreEqual(true, MSBuildNameIgnoreCaseComparer.Default.Equals("FOO", "foo"));
            Assert.AreEqual(false, MSBuildNameIgnoreCaseComparer.Default.Equals("FOO", " FOO"));
            Assert.AreEqual(false, MSBuildNameIgnoreCaseComparer.Default.Equals("FOOA", "FOOB"));
            Assert.AreEqual(false, MSBuildNameIgnoreCaseComparer.Default.Equals("AFOO", "BFOO"));
            Assert.AreEqual(false, MSBuildNameIgnoreCaseComparer.Default.Equals("FOO", "FOO "));
            Assert.AreEqual(false, MSBuildNameIgnoreCaseComparer.Default.Equals("a", "b"));
            Assert.AreEqual(true, MSBuildNameIgnoreCaseComparer.Default.Equals("", ""));
            Assert.AreEqual(false, MSBuildNameIgnoreCaseComparer.Default.Equals("x", null));
            Assert.AreEqual(false, MSBuildNameIgnoreCaseComparer.Default.Equals(null, "x"));
            Assert.AreEqual(true, MSBuildNameIgnoreCaseComparer.Default.Equals(null, null));
        }

        /// <summary>
        /// Compare real expressions
        /// </summary>
        [TestMethod]
        public void MatchProperty()
        {
            PropertyDictionary<ProjectPropertyInstance> dictionary = new PropertyDictionary<ProjectPropertyInstance>();

            ProjectPropertyInstance p = ProjectPropertyInstance.Create("foo", "bar");

            dictionary.Set(p);

            string s = "$(foo)";
            ProjectPropertyInstance value = MSBuildNameIgnoreCaseComparer.Mutable.GetValueWithConstraints<ProjectPropertyInstance>(dictionary, s, 2, 4);

            Assert.IsTrue(Object.ReferenceEquals(p, value), "Should have returned the same object as was inserted");

            try
            {
                MSBuildNameIgnoreCaseComparer.Mutable.SetConstraintsForUnitTestingOnly(s, 2, 4);
                Assert.AreEqual(MSBuildNameIgnoreCaseComparer.Default.GetHashCode("foo"), MSBuildNameIgnoreCaseComparer.Mutable.GetHashCode(s));
            }
            finally
            {
                MSBuildNameIgnoreCaseComparer.Mutable.RemoveConstraintsForUnitTestingOnly();
            }
        }

        /// <summary>
        /// Default comparer is immutable
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InternalErrorException))]
        public void Immutable()
        {
            Dictionary<string, Object> dictionary = new Dictionary<string, Object>(MSBuildNameIgnoreCaseComparer.Default);

            MSBuildNameIgnoreCaseComparer.Default.GetValueWithConstraints(dictionary, "x", 0, 1);
        }

        /// <summary>
        /// Objects work
        /// </summary>
        [TestMethod]
        public void NonString()
        {
            Object o = new Object();
            Assert.AreEqual(true, ((IEqualityComparer)MSBuildNameIgnoreCaseComparer.Default).Equals(o, o));
        }

        /// <summary>
        /// Null 
        /// </summary>
        [TestMethod]
        public void Null1()
        {
            Assert.AreEqual(false, MSBuildNameIgnoreCaseComparer.Default.Equals("x", null));
        }

        /// <summary>
        /// Null 
        /// </summary>
        [TestMethod]
        public void Null2()
        {
            Assert.AreEqual(false, MSBuildNameIgnoreCaseComparer.Default.Equals(null, "x"));
        }

        /// <summary>
        /// Make sure we can handle the case where the dictionary is null.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InternalErrorException))]
        public void NullDictionary()
        {
            MSBuildNameIgnoreCaseComparer.Mutable.GetValueWithConstraints<Object>(null, "s", 0, 1);
        }

        /// <summary>
        /// Invalid start 
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InternalErrorException))]
        public void InvalidValue2()
        {
            PropertyDictionary<ProjectPropertyInstance> dictionary = new PropertyDictionary<ProjectPropertyInstance>();
            MSBuildNameIgnoreCaseComparer.Mutable.GetValueWithConstraints<ProjectPropertyInstance>(dictionary, "x", -1, 0);
        }

        /// <summary>
        /// Invalid small end 
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InternalErrorException))]
        public void InvalidValue4()
        {
            PropertyDictionary<ProjectPropertyInstance> dictionary = new PropertyDictionary<ProjectPropertyInstance>();
            MSBuildNameIgnoreCaseComparer.Mutable.GetValueWithConstraints<ProjectPropertyInstance>(dictionary, "x", 0, -1);
        }

        /// <summary>
        /// Invalid large end 
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InternalErrorException))]
        public void InvalidValue5()
        {
            PropertyDictionary<ProjectPropertyInstance> dictionary = new PropertyDictionary<ProjectPropertyInstance>();
            MSBuildNameIgnoreCaseComparer.Mutable.GetValueWithConstraints<ProjectPropertyInstance>(dictionary, "x", 0, 2);
        }

        /// <summary>
        /// End past the end of other string
        /// </summary>
        [TestMethod]
        public void EqualsEndPastEnd1()
        {
            PropertyDictionary<ProjectPropertyInstance> dictionary = new PropertyDictionary<ProjectPropertyInstance>();

            ProjectPropertyInstance p = ProjectPropertyInstance.Create("bbb", "value");
            dictionary.Set(p);

            ProjectPropertyInstance value = MSBuildNameIgnoreCaseComparer.Mutable.GetValueWithConstraints<ProjectPropertyInstance>(dictionary, "abbbaaa", 1, 3);

            Assert.IsTrue(Object.ReferenceEquals(p, value), "Should have returned the same object as was inserted");
        }

        /// <summary>
        /// Same values means one char
        /// </summary>
        [TestMethod]
        public void EqualsSameStartEnd1()
        {
            PropertyDictionary<ProjectPropertyInstance> dictionary = new PropertyDictionary<ProjectPropertyInstance>();

            ProjectPropertyInstance p1 = ProjectPropertyInstance.Create("A", "value1");
            ProjectPropertyInstance p2 = ProjectPropertyInstance.Create("B", "value2");

            dictionary.Set(p1);
            dictionary.Set(p2);

            ProjectPropertyInstance value = MSBuildNameIgnoreCaseComparer.Mutable.GetValueWithConstraints<ProjectPropertyInstance>(dictionary, "babbbb", 1, 1);

            Assert.IsTrue(Object.ReferenceEquals(p1, value), "Should have returned the 'A' value");
        }

        /// <summary>
        /// Same values means one char
        /// </summary>
        [TestMethod]
        public void EqualsSameStartEnd2()
        {
            PropertyDictionary<ProjectPropertyInstance> dictionary = new PropertyDictionary<ProjectPropertyInstance>();

            ProjectPropertyInstance p1 = ProjectPropertyInstance.Create("a", "value1");
            ProjectPropertyInstance p2 = ProjectPropertyInstance.Create("b", "value2");

            dictionary.Set(p1);
            dictionary.Set(p2);

            ProjectPropertyInstance value = MSBuildNameIgnoreCaseComparer.Mutable.GetValueWithConstraints<ProjectPropertyInstance>(dictionary, "aabaa", 2, 2);

            Assert.IsTrue(Object.ReferenceEquals(p2, value), "Should have returned the 'b' value");
        }

        /// <summary>
        /// Same values means one char
        /// </summary>
        [TestMethod]
        public void EqualsSameStartEnd3()
        {
            PropertyDictionary<ProjectPropertyInstance> dictionary = new PropertyDictionary<ProjectPropertyInstance>();

            ProjectPropertyInstance p1 = ProjectPropertyInstance.Create("a", "value1");
            ProjectPropertyInstance p2 = ProjectPropertyInstance.Create("b", "value2");

            dictionary.Set(p1);
            dictionary.Set(p2);

            ProjectPropertyInstance value = MSBuildNameIgnoreCaseComparer.Mutable.GetValueWithConstraints<ProjectPropertyInstance>(dictionary, "ab", 0, 0);

            Assert.IsTrue(Object.ReferenceEquals(p1, value), "Should have returned the 'a' value");
        }

        /// <summary>
        /// Start at 0
        /// </summary>
        [TestMethod]
        public void EqualsStartZero()
        {
            PropertyDictionary<ProjectPropertyInstance> dictionary = new PropertyDictionary<ProjectPropertyInstance>();

            ProjectPropertyInstance p1 = ProjectPropertyInstance.Create("aab", "value1");
            ProjectPropertyInstance p2 = ProjectPropertyInstance.Create("aba", "value2");

            dictionary.Set(p1);
            dictionary.Set(p2);

            ProjectPropertyInstance value = MSBuildNameIgnoreCaseComparer.Mutable.GetValueWithConstraints<ProjectPropertyInstance>(dictionary, "aabaa", 0, 2);

            Assert.IsTrue(Object.ReferenceEquals(p1, value), "Should have returned the 'aab' value");
        }

        /// <summary>
        /// End at end
        /// </summary>
        [TestMethod]
        public void EqualsEndEnd()
        {
            PropertyDictionary<ProjectPropertyInstance> dictionary = new PropertyDictionary<ProjectPropertyInstance>();

            ProjectPropertyInstance p1 = ProjectPropertyInstance.Create("aabaaaa", "value1");
            ProjectPropertyInstance p2 = ProjectPropertyInstance.Create("baaaa", "value2");
            dictionary.Set(p1);
            dictionary.Set(p2);

            string constraint = "aabaaa";

            ProjectPropertyInstance p3 = ProjectPropertyInstance.Create("abaaa", "value3");
            dictionary.Set(p3);

            // Should match o3
            ProjectPropertyInstance value1 = MSBuildNameIgnoreCaseComparer.Mutable.GetValueWithConstraints<ProjectPropertyInstance>(dictionary, constraint, 1, 5);

            Assert.IsTrue(Object.ReferenceEquals(p3, value1), "Should have returned the 'abaaa' value");

            dictionary.Remove("abaaa"); // get rid of o3

            ProjectPropertyInstance value2 = MSBuildNameIgnoreCaseComparer.Mutable.GetValueWithConstraints<ProjectPropertyInstance>(dictionary, constraint, 1, 5);

            Assert.IsNull(value2, "Should not have been a match in the dictionary");

            // Even if the string is exactly the same, if only a substring is being compared, then although it 
            // will be judged equal, the hash codes will NOT be the same, and for that reason, a lookup in the 
            // dictionary will fail.  
            int originalHashCode = MSBuildNameIgnoreCaseComparer.Mutable.GetHashCode("aabaaa");
            try
            {
                MSBuildNameIgnoreCaseComparer.Mutable.SetConstraintsForUnitTestingOnly(constraint, 1, 5);

                Assert.IsTrue(MSBuildNameIgnoreCaseComparer.Mutable.Equals("aabaaa", constraint)); // same on both sides
                Assert.AreNotEqual(originalHashCode, MSBuildNameIgnoreCaseComparer.Mutable.GetHashCode(constraint));
            }
            finally
            {
                MSBuildNameIgnoreCaseComparer.Mutable.RemoveConstraintsForUnitTestingOnly();
            }
        }

        /// <summary>
        /// Default get hash code
        /// </summary>
        [TestMethod]
        public void DefaultGetHashcode()
        {
            Assert.AreEqual(true, 0 == MSBuildNameIgnoreCaseComparer.Default.GetHashCode(null));

            MSBuildNameIgnoreCaseComparer.Default.GetHashCode(""); // doesn't throw            
            Assert.AreEqual(MSBuildNameIgnoreCaseComparer.Default.GetHashCode("aBc"), MSBuildNameIgnoreCaseComparer.Default.GetHashCode("AbC"));
        }

        /// <summary>
        /// Indexed get hashcode
        /// </summary>
        [TestMethod]
        public void IndexedGetHashcode1()
        {
            MSBuildNameIgnoreCaseComparer comparer = MSBuildNameIgnoreCaseComparer.Mutable;
            string s = "xyz";

            try
            {
                comparer.SetConstraintsForUnitTestingOnly(s, 0, 0);

                comparer.GetHashCode(""); // does not crash

                Assert.AreEqual(true, 0 == comparer.GetHashCode(null));
                Assert.AreEqual(comparer.GetHashCode("aBc"), comparer.GetHashCode("AbC"));
                Assert.AreEqual(comparer.GetHashCode(s), comparer.GetHashCode("x"));
            }
            finally
            {
                comparer.RemoveConstraintsForUnitTestingOnly();
            }
        }

        /// <summary>
        /// Indexed get hashcode
        /// </summary>
        [TestMethod]
        public void IndexedGetHashcode2()
        {
            MSBuildNameIgnoreCaseComparer comparer = MSBuildNameIgnoreCaseComparer.Mutable;
            string s = "xyz";

            try
            {
                comparer.SetConstraintsForUnitTestingOnly(s, 1, 2);

                Assert.AreEqual(comparer.GetHashCode(s), comparer.GetHashCode("YZ"));
            }
            finally
            {
                comparer.RemoveConstraintsForUnitTestingOnly();
            }
        }

        /// <summary>
        /// Indexed get hashcode
        /// </summary>
        [TestMethod]
        public void IndexedGetHashcode3()
        {
            MSBuildNameIgnoreCaseComparer comparer = MSBuildNameIgnoreCaseComparer.Mutable;
            string s = "abcd";

            try
            {
                comparer.SetConstraintsForUnitTestingOnly(s, 0, 2);

                Assert.AreEqual(comparer.GetHashCode(s), comparer.GetHashCode("abc"));
            }
            finally
            {
                comparer.RemoveConstraintsForUnitTestingOnly();
            }
        }
    }
}