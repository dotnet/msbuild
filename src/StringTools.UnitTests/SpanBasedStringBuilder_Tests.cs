// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET35_UNITTEST
extern alias StringToolsNet35;
#endif

using System.Collections.Generic;

using Shouldly;
#if NET35_UNITTEST
using Xunit;
#endif

#if NET35_UNITTEST
using StringToolsNet35::Microsoft.NET.StringTools;
#endif

#nullable disable

namespace Microsoft.NET.StringTools.Tests
{
    [TestClass]
    public class SpanBasedStringBuilder_Tests
    {
        private SpanBasedStringBuilder MakeSpanBasedStringBuilder(InterningTestData.TestDatum datum, bool appendSubStrings = false)
        {
            bool wrapFirstFragment = datum.Fragments.Length > 0 && datum.Fragments[0] != null;

            SpanBasedStringBuilder stringBuilder = wrapFirstFragment
                ? new SpanBasedStringBuilder(datum.Fragments[0])
                : new SpanBasedStringBuilder();

            for (int i = 1; i < datum.Fragments.Length; i++)
            {
                if (appendSubStrings)
                {
                    int index = datum.Fragments[i].Length / 2;
                    stringBuilder.Append(datum.Fragments[i], 0, index);
                    stringBuilder.Append(datum.Fragments[i], index, datum.Fragments[i].Length - index);
                }
                else
                {
                    stringBuilder.Append(datum.Fragments[i]);
                }
            }
            return stringBuilder;
        }

        public static IEnumerable<object[]> TestData => InterningTestData.TestData;
        public static IEnumerable<object[]> TestDataForTrim => InterningTestData.TestDataForTrim;

#if NET35_UNITTEST
        [Theory]
        [MemberData(nameof(TestData))]
#else
        [TestMethod]
        [DynamicData(nameof(TestData))]
#endif
        public void LengthReturnsLength(InterningTestData.TestDatum datum)
        {
            using var stringBuilder = MakeSpanBasedStringBuilder(datum);
            stringBuilder.Length.ShouldBe(datum.Length);
        }

#if NET35_UNITTEST
        [Theory]
        [MemberData(nameof(TestData))]
#else
        [TestMethod]
        [DynamicData(nameof(TestData))]
#endif
        public void EnumeratorEnumeratesCharacters(InterningTestData.TestDatum datum)
        {
            using SpanBasedStringBuilder stringBuilder = MakeSpanBasedStringBuilder(datum);
            int index = 0;
            foreach (char ch in stringBuilder)
            {
                ch.ShouldBe(datum[index]);
                index++;
            }
        }

#if NET35_UNITTEST
        [Theory]
        [MemberData(nameof(TestData))]
#else
        [TestMethod]
        [DynamicData(nameof(TestData))]
#endif
        public void EqualsReturnsExpectedValue(InterningTestData.TestDatum datum)
        {
            using var stringBuilder = MakeSpanBasedStringBuilder(datum);
            InternableString internableString = new InternableString(stringBuilder);
            internableString.Equals(string.Empty).ShouldBe(internableString.Length == 0);

            string substr = datum.Fragments[0] ?? string.Empty;
            internableString.Equals(substr).ShouldBe(substr.Length == internableString.Length);

            if (datum.Fragments.Length > 1)
            {
                substr += datum.Fragments[1];
                internableString.Equals(substr).ShouldBe(substr.Length == internableString.Length);

                internableString.Equals(datum.ToString()).ShouldBeTrue();
            }

            internableString.Equals("Things").ShouldBeFalse();
        }

#if NET35_UNITTEST
        [Fact]
#else
        [TestMethod]
#endif
        public void ReferenceEqualsReturnsExpectedValue()
        {
            string str = "Test";
            InternableString internableString = new InternableString(str);
            internableString.ReferenceEquals(str).ShouldBeTrue();
            internableString = new InternableString(new string(str.ToCharArray()));
            internableString.ReferenceEquals(str).ShouldBeFalse();
        }

#if NET35_UNITTEST
        [Theory]
        [InlineData("012345678")] // odd number of characters
        [InlineData("0123456789")] // even number of characters
#else
        [TestMethod]
        [DataRow("012345678")] // odd number of characters
        [DataRow("0123456789")] // even number of characters
#endif
        public void GetHashCodeIsStableRegardlessOfSpanLength(string testString)
        {
            int hashCode = new InternableString(testString).GetHashCode();

            // Chop the string into 2-3 parts and verify that the hash code is unchanged.
            for (int i = 0; i < testString.Length - 1; i++)
            {
                for (int j = i + 1; j < testString.Length; j++)
                {
                    using SpanBasedStringBuilder stringBuilder = new SpanBasedStringBuilder();
                    stringBuilder.Append(testString.Substring(0, i));
                    stringBuilder.Append(testString.Substring(i, j - i));
                    stringBuilder.Append(testString.Substring(j));
                    InternableString internableString = new InternableString(stringBuilder);
                    internableString.GetHashCode().ShouldBe(hashCode);
                }
            }
        }

#if NET35_UNITTEST
        [Theory]
        [MemberData(nameof(TestData))]
#else
        [TestMethod]
        [DynamicData(nameof(TestData))]
#endif
        public void AppendAppendsString(InterningTestData.TestDatum datum)
        {
            using SpanBasedStringBuilder stringBuilder = MakeSpanBasedStringBuilder(datum, false);
            new InternableString(stringBuilder).ExpensiveConvertToString().ShouldBe(datum.ToString());
        }

#if NET35_UNITTEST
        [Theory]
        [MemberData(nameof(TestData))]
#else
        [TestMethod]
        [DynamicData(nameof(TestData))]
#endif
        public void AppendAppendsSubstring(InterningTestData.TestDatum datum)
        {
            using SpanBasedStringBuilder stringBuilder = MakeSpanBasedStringBuilder(datum, true);
            new InternableString(stringBuilder).ExpensiveConvertToString().ShouldBe(datum.ToString());
        }

#if !NET35_UNITTEST
#if NET35_UNITTEST
        [Theory]
        [MemberData(nameof(TestDataForTrim))]
#else
        [TestMethod]
        [DynamicData(nameof(TestDataForTrim))]
#endif
        public void TrimStartRemovesLeadingWhiteSpace(InterningTestData.TestDatum datum)
        {
            using SpanBasedStringBuilder stringBuilder = MakeSpanBasedStringBuilder(datum);
            stringBuilder.TrimStart();
            new InternableString(stringBuilder).ExpensiveConvertToString().ShouldBe(datum.ToString().TrimStart());
        }

#if NET35_UNITTEST
        [Theory]
        [MemberData(nameof(TestDataForTrim))]
#else
        [TestMethod]
        [DynamicData(nameof(TestDataForTrim))]
#endif
        public void TrimEndRemovesTrailingWhiteSpace(InterningTestData.TestDatum datum)
        {
            using SpanBasedStringBuilder stringBuilder = MakeSpanBasedStringBuilder(datum);
            stringBuilder.TrimEnd();
            new InternableString(stringBuilder).ExpensiveConvertToString().ShouldBe(datum.ToString().TrimEnd());
        }

#if NET35_UNITTEST
        [Theory]
        [MemberData(nameof(TestDataForTrim))]
#else
        [TestMethod]
        [DynamicData(nameof(TestDataForTrim))]
#endif
        public void TrimRemovesLeadingAndTrailingWhiteSpace(InterningTestData.TestDatum datum)
        {
            using SpanBasedStringBuilder stringBuilder = MakeSpanBasedStringBuilder(datum);
            stringBuilder.Trim();
            new InternableString(stringBuilder).ExpensiveConvertToString().ShouldBe(datum.ToString().Trim());
        }
#endif

#if NET35_UNITTEST
        [Theory]
        [MemberData(nameof(TestData))]
#else
        [TestMethod]
        [DynamicData(nameof(TestData))]
#endif
        public void ClearRemovesAllCharacters(InterningTestData.TestDatum datum)
        {
            using SpanBasedStringBuilder stringBuilder = MakeSpanBasedStringBuilder(datum);
            stringBuilder.Clear();
            stringBuilder.Length.ShouldBe(0);
            stringBuilder.GetEnumerator().MoveNext().ShouldBeFalse();
        }
    }
}
