// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET35_UNITTEST
extern alias StringToolsNet35;
#endif

using System.Collections.Generic;

using Shouldly;
using Xunit;

#if NET35_UNITTEST
using StringToolsNet35::Microsoft.NET.StringTools;
#endif

#nullable disable

namespace Microsoft.NET.StringTools.Tests
{
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

        [Theory]
        [MemberData(nameof(TestData))]
        public void LengthReturnsLength(InterningTestData.TestDatum datum)
        {
            MakeSpanBasedStringBuilder(datum).Length.ShouldBe(datum.Length);
        }

        [Theory]
        [MemberData(nameof(TestData))]
        public void EnumeratorEnumeratesCharacters(InterningTestData.TestDatum datum)
        {
            SpanBasedStringBuilder stringBuilder = MakeSpanBasedStringBuilder(datum);
            int index = 0;
            foreach (char ch in stringBuilder)
            {
                ch.ShouldBe(datum[index]);
                index++;
            }
        }

        [Theory]
        [MemberData(nameof(TestData))]
        public void EqualsReturnsExpectedValue(InterningTestData.TestDatum datum)
        {
            InternableString internableString = new InternableString(MakeSpanBasedStringBuilder(datum));
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

        [Fact]
        public void ReferenceEqualsReturnsExpectedValue()
        {
            string str = "Test";
            InternableString internableString = new InternableString(str);
            internableString.ReferenceEquals(str).ShouldBeTrue();
            internableString = new InternableString(new string(str.ToCharArray()));
            internableString.ReferenceEquals(str).ShouldBeFalse();
        }

        [Theory]
        [InlineData("012345678")] // odd number of characters
        [InlineData("0123456789")] // even number of characters
        public void GetHashCodeIsStableRegardlessOfSpanLength(string testString)
        {
            int hashCode = new InternableString(testString).GetHashCode();

            // Chop the string into 2-3 parts and verify that the hash code is unchanged.
            for (int i = 0; i < testString.Length - 1; i++)
            {
                for (int j = i + 1; j < testString.Length; j++)
                {
                    SpanBasedStringBuilder stringBuilder = new SpanBasedStringBuilder();
                    stringBuilder.Append(testString.Substring(0, i));
                    stringBuilder.Append(testString.Substring(i, j - i));
                    stringBuilder.Append(testString.Substring(j));
                    InternableString internableString = new InternableString(stringBuilder);
                    internableString.GetHashCode().ShouldBe(hashCode);
                }
            }
        }

        [Theory]
        [MemberData(nameof(TestData))]
        public void AppendAppendsString(InterningTestData.TestDatum datum)
        {
            SpanBasedStringBuilder stringBuilder = MakeSpanBasedStringBuilder(datum, false);
            new InternableString(stringBuilder).ExpensiveConvertToString().ShouldBe(datum.ToString());
        }

        [Theory]
        [MemberData(nameof(TestData))]
        public void AppendAppendsSubstring(InterningTestData.TestDatum datum)
        {
            SpanBasedStringBuilder stringBuilder = MakeSpanBasedStringBuilder(datum, true);
            new InternableString(stringBuilder).ExpensiveConvertToString().ShouldBe(datum.ToString());
        }

#if !NET35_UNITTEST
        [Theory]
        [MemberData(nameof(TestDataForTrim))]
        public void TrimStartRemovesLeadingWhiteSpace(InterningTestData.TestDatum datum)
        {
            SpanBasedStringBuilder stringBuilder = MakeSpanBasedStringBuilder(datum);
            stringBuilder.TrimStart();
            new InternableString(stringBuilder).ExpensiveConvertToString().ShouldBe(datum.ToString().TrimStart());
        }

        [Theory]
        [MemberData(nameof(TestDataForTrim))]
        public void TrimEndRemovesTrailingWhiteSpace(InterningTestData.TestDatum datum)
        {
            SpanBasedStringBuilder stringBuilder = MakeSpanBasedStringBuilder(datum);
            stringBuilder.TrimEnd();
            new InternableString(stringBuilder).ExpensiveConvertToString().ShouldBe(datum.ToString().TrimEnd());
        }

        [Theory]
        [MemberData(nameof(TestDataForTrim))]
        public void TrimRemovesLeadingAndTrailingWhiteSpace(InterningTestData.TestDatum datum)
        {
            SpanBasedStringBuilder stringBuilder = MakeSpanBasedStringBuilder(datum);
            stringBuilder.Trim();
            new InternableString(stringBuilder).ExpensiveConvertToString().ShouldBe(datum.ToString().Trim());
        }
#endif

        [Theory]
        [MemberData(nameof(TestData))]
        public void ClearRemovesAllCharacters(InterningTestData.TestDatum datum)
        {
            SpanBasedStringBuilder stringBuilder = MakeSpanBasedStringBuilder(datum);
            stringBuilder.Clear();
            stringBuilder.Length.ShouldBe(0);
            stringBuilder.GetEnumerator().MoveNext().ShouldBeFalse();
        }
    }
}
