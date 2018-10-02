// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Text;
using NUnit.Framework;
using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class CopyOnWriteHashtable_Tests
    {
        [Test]
        public void Basic()
        {
            CopyOnWriteHashtable c = new CopyOnWriteHashtable(StringComparer.OrdinalIgnoreCase);
            CopyOnWriteHashtable b = (CopyOnWriteHashtable)c.Clone();
            CopyOnWriteHashtable a = (CopyOnWriteHashtable)b.Clone();

            c["Foo"] = "Bar";

            // Just wrote to 'c' so it should contain data.
            Assertion.Assert(c.ContainsKey("Foo"));
            
            // Writing to a depended upon hashtable should not be visible to the dependents.
            Assertion.Assert(!a.ContainsKey("Foo"));
            Assertion.Assert(!b.ContainsKey("Foo"));
        }

        [Test]
        public void Regress_SettingWhenValueNull()
        {
            CopyOnWriteHashtable c = new CopyOnWriteHashtable(StringComparer.OrdinalIgnoreCase);
            CopyOnWriteHashtable refc = new CopyOnWriteHashtable(c, StringComparer.OrdinalIgnoreCase);
            Hashtable h = new Hashtable();

            refc["key"] = null;
            h["key"] = null;

            Assertion.AssertEquals(h.ContainsKey("key"), refc.ContainsKey("key"));
            Assertion.Assert(!c.ContainsKey("key"));
        }


        [Test]
        [ExpectedException(typeof(InternalErrorException))]
        public void Regress450669_CaseSensitiveBatch_WeDontAllowChangingCaseOnCopiedHashTable()
        {
            CopyOnWriteHashtable c = new CopyOnWriteHashtable(StringComparer.OrdinalIgnoreCase);
            CopyOnWriteHashtable refc = new CopyOnWriteHashtable(c, StringComparer.Ordinal); // Different case.
        }

        [Test]
        public void Regress450669_CaseSensitiveBatch_HashtableCopyRespectsComparer()
        {
            Hashtable c = new Hashtable(StringComparer.OrdinalIgnoreCase);
            c["key"] = null;
            CopyOnWriteHashtable refc = new CopyOnWriteHashtable(c, StringComparer.OrdinalIgnoreCase); 

            Assertion.Assert(c.ContainsKey("kEy"));
            Assertion.Assert(refc.ContainsKey("kEy"));
        }

        /// <summary>
        /// Missing unittest found by mutation testing.
        /// REASON TEST WASN'T ORIGINALLY PRESENT: (Note 1)
        /// 
        /// In this test, verify that a CopyOnWriteHashtable passed through the constructor that
        /// accepts an IDictionary results in a shallow copy not a deep copy.
        /// </summary>
        [Test]
        public void Regress_Mutation_ConstructThroughDictionaryIsShallowCopy()
        {
            CopyOnWriteHashtable c = new CopyOnWriteHashtable(StringComparer.OrdinalIgnoreCase);
            CopyOnWriteHashtable refc = new CopyOnWriteHashtable((IDictionary)c, StringComparer.OrdinalIgnoreCase);

            Assertion.Assert(refc.IsShallowCopy);
        }

        /// <summary>
        /// Missing unittest found by mutation testing.
        /// REASON TEST WASN'T ORIGINALLY PRESENT: (Note 1)
        /// 
        /// In this test, verify that writing a value that exists already in a shallow copy
        /// doesn't cause a deep copy of the hash table.
        /// </summary>
        [Test]
        public void Regress_Mutation_WritingSameValueShouldNotCauseDeepCopy()
        {
            CopyOnWriteHashtable c = new CopyOnWriteHashtable(StringComparer.OrdinalIgnoreCase);
            c["answer"] = "yes";
            CopyOnWriteHashtable refc = new CopyOnWriteHashtable(c, StringComparer.OrdinalIgnoreCase);
            
            Assertion.Assert(refc.IsShallowCopy);
            refc["answer"] = "yes";
            Assertion.Assert(refc.IsShallowCopy);  // Setting the same value should not cause a deep copy.
        }


        /// <summary>
        /// Missing unittest found by mutation testing.
        /// REASON TEST WASN'T ORIGINALLY PRESENT: This is a design change, we require a string comparer 
        /// in all cases because we can't construct a deep copy without always knowing what string
        /// comparer to use.
        /// 
        /// In this test, try to construct a CopyOnWriteHashtable with no string comparer.
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Regress_Mutation_MustHaveNonNullStringComparer()
        {
            CopyOnWriteHashtable c = new CopyOnWriteHashtable(null);
        }

        /// <summary>
        /// Missing unittest found by mutation testing.
        /// REASON TEST WASN'T ORIGINALLY PRESENT: This is a design change, we require a string comparer 
        /// in all cases because we can't construct a deep copy without always knowing what string
        /// comparer to use.
        /// 
        /// In this test, try to construct a CopyOnWriteHashtable with no string comparer.
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Regress_Mutation_MustHaveNonNullStringComparer2()
        {
            CopyOnWriteHashtable c = new CopyOnWriteHashtable(null, null);
        }

        /// <summary>
        /// Missing unittest found by mutation testing.
        /// REASON TEST WASN'T ORIGINALLY PRESENT: Missed test.
        /// 
        /// In this test, make sure Clear works on shallow-copy hashtable.
        /// </summary>
        [Test]
        public void Regress_Mutation_ClearReadOnlyData()
        {
            CopyOnWriteHashtable c = new CopyOnWriteHashtable(StringComparer.OrdinalIgnoreCase);
            c["key"] = "value";
            CopyOnWriteHashtable refc = new CopyOnWriteHashtable(c, StringComparer.OrdinalIgnoreCase);

            Assertion.Assert(refc.ContainsKey("key"));
            Assertion.Assert(refc.IsShallowCopy);
            c.Clear();
            Assertion.Assert(refc.ContainsKey("key"));
            Assertion.Assert(!c.ContainsKey("key"));
        }

        /*
         * Root cause analysis: reasons for missing tests:
         * 
         * (Note 1) It was intended that the user of CopyOnWriteHashtable should not be able to detect
         *          whether a shallow copy or deep copy was made. So there was no way to unittest this.
         *          This test required adding 'IsShallowCopy' to detect this case.
         * 
         */
    }
}
