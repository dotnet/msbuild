// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Threading;
using Microsoft.Build.Collections;
using Shouldly;
using Xunit;

#nullable disable

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

            CopyOnReadEnumerable<int, string> enumerable = new CopyOnReadEnumerable<int, string>(values, values, i => i.ToString());

            using (IEnumerator<int> enumerator = values.GetEnumerator())
            {
                foreach (string s in enumerable)
                {
                    enumerator.MoveNext();
                    enumerator.Current.ToString().ShouldBe(s);
                }
                enumerator.MoveNext().ShouldBeFalse();
            }
        }

        [Fact]
        public void CopiesBackingCollection()
        {
            List<string> values = new List<string>(new string[] { "a", "b", "c" });

            CopyOnReadEnumerable<string, string> enumerable = new CopyOnReadEnumerable<string, string>(values, values, s => s);

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

        [Fact]
        public void FiltersBeforeCopyingAndPreservesSnapshotSemantics()
        {
            List<string> values = ["a", "b", "c"];
            List<string> copied = [];
            var enumerable = new CopyOnReadEnumerable<string, string>(values, values, value =>
            {
                Monitor.IsEntered(values).ShouldBeTrue();
                copied.Add(value);
                return value.ToUpperInvariant();
            });
            var filtered = enumerable.Filter(value =>
            {
                Monitor.IsEntered(values).ShouldBeTrue();
                return value != "b";
            });
            copied.ShouldBeEmpty();

            using var enumerator = filtered.GetEnumerator();
            copied.ShouldBe(["a", "c"]);
            values.Add("d");

            enumerator.MoveNext().ShouldBeTrue();
            enumerator.Current.ShouldBe("A");
            enumerator.MoveNext().ShouldBeTrue();
            enumerator.Current.ShouldBe("C");
            enumerator.MoveNext().ShouldBeFalse();

            filtered.ShouldBe(["A", "C", "D"]);
            copied.ShouldBe(["a", "c", "a", "c", "d"]);
            enumerable.ShouldBe(["A", "B", "C", "D"]);
        }

        [Fact]
        public void FiltersWithNoMatchesDoNotCopyItems()
        {
            List<int> values = [1, 2, 3];
            int copies = 0;
            var enumerable = new CopyOnReadEnumerable<int, int>(values, values, value =>
            {
                copies++;
                return value;
            });

            enumerable.Filter(static _ => false).ShouldBeEmpty();
            copies.ShouldBe(0);
        }
    }
}
