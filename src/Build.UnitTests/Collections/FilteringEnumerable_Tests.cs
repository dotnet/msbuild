// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Tests the FilteringEnumerable utility class</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Microsoft.Build.Collections;
using Xunit;

namespace Microsoft.Build.UnitTests.OM.Collections
{
    /// <summary>
    /// Tests for FilteringEnumerable
    /// </summary>
    public class FilteringEnumerable_Tests
    {
        /// <summary>
        /// Verify basic case
        /// </summary>
        [Fact]
        public void FilteringEnumerableBasic()
        {
            A a1 = new A();
            B b1 = new B();
            A a2 = new A();
            B b2 = new B();

            List<A> list = new List<A>();
            list.Add(a1);
            list.Add(b1);
            list.Add(a2);
            list.Add(b2);
            var collection = new FilteringEnumerable<A, B>(list);

            List<B> result = new List<B>(collection);
            Assert.Equal(2, result.Count);
        }

        /// <summary>
        /// Null collection should be like an empty collection
        /// (Seems useful for a general purpose class)
        /// </summary>
        [Fact]
        public void FilteringEnumerableNullBacking()
        {
            IEnumerable<B> enumerable = new FilteringEnumerable<A, B>(null);

            Assert.Equal(false, enumerable.GetEnumerator().MoveNext());
        }

        /// <summary>
        /// Test class
        /// </summary>
        private class A
        {
        }

        /// <summary>
        /// Test class
        /// </summary>
        private class B : A
        {
        }
    }
}
