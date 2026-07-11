// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
#if NET35_UNITTEST
using Xunit.Sdk;
#endif

#nullable disable

namespace Microsoft.NET.StringTools.Tests
{
    public static class InterningTestData
    {
        /// <summary>
        /// Represents an array of string fragments to initialize an InternableString with.
        /// </summary>
        public class TestDatum
#if NET35_UNITTEST
            : IXunitSerializable
#endif
        {
            private string _string;
            public string[] Fragments { get; private set; }

            public int Length => _string.Length;

            // Required for deserialization
            public TestDatum()
            {
            }

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

#if NET35_UNITTEST
            public void Deserialize(IXunitSerializationInfo info)
            {
                Fragments = info.GetValue<string[]>(nameof(Fragments));
                _string = string.Join(string.Empty, Fragments);
            }

            public void Serialize(IXunitSerializationInfo info)
            {
                info.AddValue(nameof(Fragments), Fragments);
            }
#endif
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
