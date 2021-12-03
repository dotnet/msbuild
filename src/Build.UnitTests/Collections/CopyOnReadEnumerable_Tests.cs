// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Build.Collections;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.OM.Collections
{
    /// <summary>
    /// Tests for CopyOnReadEnumerable
    /// </summary>
    public class CopyOnReadEnumerable_Tests
    {
        [Fact]
        public void EnumeratesBackingCollection()
        {
            List<int> values = new List<int>(new int[] { 1, 2, 3 });

            CopyOnReadEnumerable<int> enumerable = new CopyOnReadEnumerable<int>(values, values);

            using (IEnumerator<int> enumerator = values.GetEnumerator())
            {
                foreach (int i in enumerable)
                {
                    enumerator.MoveNext();
                    enumerator.Current.ShouldBe(i);
                }
                enumerator.MoveNext().ShouldBeFalse();
            }
        }

        [Fact]
        public void CopiesBackingCollection()
        {
            List<string> values = new List<string>(new string[] { "a", "b", "c" });

            CopyOnReadEnumerable<string> enumerable = new CopyOnReadEnumerable<string>(values, values);

            int expectedCount = values.Count;
            var enumerator = enumerable.GetEnumerator();

            // The list has been copied and adding to it has no effect on the enumerable.
            values.Add("d");

            int actualCount = 0;
            while (enumerator.MoveNext())
            {
                actualCount++;
            }
            actualCount.ShouldBe(expectedCount);
        }
    }
}
