// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.NET.StringTools.Tests
{
    public static class InterningTestData
    {
        /// <summary>
        /// Represents an array of string fragments to initialize an InternableString with.
        /// </summary>
        public class TestDatum
        {
            private string _string;
            public string[] Fragments { get; }

            public int Length => _string.Length;

            public TestDatum(params string[] fragments)
            {
                Fragments = fragments;
                _string = string.Join(string.Empty, Fragments);
            }

            public char this[int index] => _string[index];

            public override string ToString()
            {
                return _string;
            }
        }

        public static IEnumerable<object[]> TestData
        {
            get
            {
                yield return new object[] { new TestDatum((string)null) };
                yield return new object[] { new TestDatum("") };
                yield return new object[] { new TestDatum("Test") };
                yield return new object[] { new TestDatum(null, "All") };
                yield return new object[] { new TestDatum("", "All") };
                yield return new object[] { new TestDatum("", "All", "") };
                yield return new object[] { new TestDatum("Test", "All", "The", "Things") };
            }
        }

        public static IEnumerable<object[]> TestDataForTrim
        {
            get
            {
                yield return new object[] { new TestDatum((string)null) };
                yield return new object[] { new TestDatum("") };
                yield return new object[] { new TestDatum(" ") };
                yield return new object[] { new TestDatum("  ") };
                yield return new object[] { new TestDatum(null, "") };
                yield return new object[] { new TestDatum(null, " ") };
                yield return new object[] { new TestDatum(" T ") };
                yield return new object[] { new TestDatum("  Test  ") };
                yield return new object[] { new TestDatum(null, " Test ") };
                yield return new object[] { new TestDatum(null, "  Test All  ") };
                yield return new object[] { new TestDatum(" ", "  Test", "", "All  ", " ") };
                yield return new object[] { new TestDatum("Test", "  ", "", "  ", " ") };
                yield return new object[] { new TestDatum("Test", " All ", " The ", "Things") };
            }
        }
    }
}
