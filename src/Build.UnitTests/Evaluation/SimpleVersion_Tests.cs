// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using Xunit;

namespace Microsoft.Build.UnitTests.Evaluation
{
    public class SimpleVersion_Tests
    {
        [Fact]
        public void Ctor_Default()
        {
            VerifyVersion(new SimpleVersion(), 0, 0, 0, 0);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(2)]
        [InlineData(int.MaxValue)]
        public static void Ctor_Int(int major)
        {
            VerifyVersion(new SimpleVersion(major), major, 0, 0, 0);
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(2, 3)]
        [InlineData(int.MaxValue, int.MaxValue)]
        public static void Ctor_Int_Int(int major, int minor)
        {
            VerifyVersion(new SimpleVersion(major, minor), major, minor, 0, 0);
        }

        [Theory]
        [InlineData(0, 0, 0)]
        [InlineData(2, 3, 4)]
        [InlineData(int.MaxValue, int.MaxValue, int.MaxValue)]
        public static void Ctor_Int_Int_Int(int major, int minor, int build)
        {
            VerifyVersion(new SimpleVersion(major, minor, build), major, minor, build, 0);
        }

        [Theory]
        [InlineData(0, 0, 0, 0)]
        [InlineData(2, 3, 4, 7)]
        [InlineData(2, 3, 4, 32767)]
        [InlineData(2, 3, 4, 32768)]
        [InlineData(2, 3, 4, 65535)]
        [InlineData(2, 3, 4, 65536)]
        [InlineData(2, 3, 4, 2147483647)]
        [InlineData(2, 3, 4, 2147450879)]
        [InlineData(2, 3, 4, 2147418112)]
        [InlineData(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue)]
        public static void Ctor_Int_Int_Int_Int(int major, int minor, int build, int revision)
        {
            VerifyVersion(new SimpleVersion(major, minor, build, revision), major, minor, build, revision);
        }

        [Fact]
        public void Ctor_NegativeMajor_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>("major", () => new SimpleVersion(-1, 0));
            Assert.Throws<ArgumentOutOfRangeException>("major", () => new SimpleVersion(-1, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>("major", () => new SimpleVersion(-1, 0, 0, 0));
        }

        [Fact]
        public void Ctor_NegativeMinor_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>("minor", () => new SimpleVersion(0, -1));
            Assert.Throws<ArgumentOutOfRangeException>("minor", () => new SimpleVersion(0, -1, 0));
            Assert.Throws<ArgumentOutOfRangeException>("minor", () => new SimpleVersion(0, -1, 0, 0));
        }

        [Fact]
        public void Ctor_NegativeBuild_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>("build", () => new SimpleVersion(0, 0, -1));
            Assert.Throws<ArgumentOutOfRangeException>("build", () => new SimpleVersion(0, 0, -1, 0));
        }

        [Fact]
        public void Ctor_NegativeRevision_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>("revision", () => new SimpleVersion(0, 0, 0, -1));
        }

        public static IEnumerable<object[]> Comparison_TestData()
        {
            foreach (var input in new (SimpleVersion v1, SimpleVersion v2, int expectedSign)[]
            {
                (new SimpleVersion(1, 2), new SimpleVersion(1, 2), 0),
                (new SimpleVersion(1, 2), new SimpleVersion(1, 3), -1),
                (new SimpleVersion(1, 2), new SimpleVersion(1, 1), 1),
                (new SimpleVersion(1, 2), new SimpleVersion(2, 0), -1),
                (new SimpleVersion(1, 2), new SimpleVersion(1, 2, 1), -1),
                (new SimpleVersion(1, 2), new SimpleVersion(1, 2, 0, 1), -1),
                (new SimpleVersion(1, 2), new SimpleVersion(1, 0), 1),
                (new SimpleVersion(1, 2), new SimpleVersion(1, 0, 1), 1),
                (new SimpleVersion(1, 2), new SimpleVersion(1, 0, 0, 1), 1),

                (new SimpleVersion(3, 2, 1), new SimpleVersion(2, 2, 1), 1),
                (new SimpleVersion(3, 2, 1), new SimpleVersion(3, 1, 1), 1),
                (new SimpleVersion(3, 2, 1), new SimpleVersion(3, 2, 0), 1),

                (new SimpleVersion(1, 2, 3, 4), new SimpleVersion(1, 2, 3, 4), 0),
                (new SimpleVersion(1, 2, 3, 4), new SimpleVersion(1, 2, 3, 5), -1),
                (new SimpleVersion(1, 2, 3, 4), new SimpleVersion(1, 2, 3, 3), 1)
            })
            {
                yield return new object[] { input.v1, input.v2, input.expectedSign };
                yield return new object[] { input.v2, input.v1, input.expectedSign * -1 };
            }
        }

        [Theory]
        [MemberData(nameof(Comparison_TestData))]
        public void CompareTo_ReturnsExpected(object version1Object, object version2Object, int expectedSign)
        {
            var version1 = (SimpleVersion)version1Object;
            var version2 = (SimpleVersion)version2Object;

            Assert.Equal(expectedSign, Comparer<SimpleVersion>.Default.Compare(version1, version2));
            Assert.Equal(expectedSign, Math.Sign(version1.CompareTo(version2)));
        }

        [Theory]
        [MemberData(nameof(Comparison_TestData))]
        public void ComparisonOperators_ReturnExpected(object version1Object, object version2Object, int expectedSign)
        {
            var version1 = (SimpleVersion)version1Object;
            var version2 = (SimpleVersion)version2Object;

            if (expectedSign < 0)
            {
                Assert.True(version1 < version2);
                Assert.True(version1 <= version2);
                Assert.False(version1 == version2);
                Assert.False(version1 >= version2);
                Assert.False(version1 > version2);
                Assert.True(version1 != version2);
            }
            else if (expectedSign == 0)
            {
                Assert.False(version1 < version2);
                Assert.True(version1 <= version2);
                Assert.True(version1 == version2);
                Assert.True(version1 >= version2);
                Assert.False(version1 > version2);
                Assert.False(version1 != version2);
            }
            else
            {
                Assert.False(version1 < version2);
                Assert.False(version1 <= version2);
                Assert.False(version1 == version2);
                Assert.True(version1 >= version2);
                Assert.True(version1 > version2);
                Assert.True(version1 != version2);
            }
        }

        public static IEnumerable<object[]> Equals_TestData()
        {
            yield return new object[] { new SimpleVersion(2, 3), new SimpleVersion(2, 3), true };
            yield return new object[] { new SimpleVersion(2, 3), new SimpleVersion(2, 4), false };
            yield return new object[] { new SimpleVersion(2, 3), new SimpleVersion(3, 3), false };

            yield return new object[] { new SimpleVersion(2, 3, 4), new SimpleVersion(2, 3, 4), true };
            yield return new object[] { new SimpleVersion(2, 3, 4), new SimpleVersion(2, 3, 5), false };
            yield return new object[] { new SimpleVersion(2, 3, 4), new SimpleVersion(2, 3), false };

            yield return new object[] { new SimpleVersion(2, 3, 4, 5), new SimpleVersion(2, 3, 4, 5), true };
            yield return new object[] { new SimpleVersion(2, 3, 4, 5), new SimpleVersion(2, 3, 4, 6), false };
            yield return new object[] { new SimpleVersion(2, 3, 4, 5), new SimpleVersion(2, 3), false };
            yield return new object[] { new SimpleVersion(2, 3, 4, 5), new SimpleVersion(2, 3, 4), false };

            yield return new object[] { new SimpleVersion(2, 3, 0), new SimpleVersion(2, 3), true };
            yield return new object[] { new SimpleVersion(2, 3, 4, 0), new SimpleVersion(2, 3, 4), true };

            yield return new object[] { new SimpleVersion(2, 3, 4, 5), new TimeSpan(), false };
            yield return new object[] { new SimpleVersion(2, 3, 4, 5), null, false };
        }

        [Theory]
        [MemberData(nameof(Equals_TestData))]
        public static void Equals_Other_ReturnsExpected(object version1Object, object version2Object, bool expected)
        {
            var version1 = (SimpleVersion)version1Object;

            if (version2Object is SimpleVersion version2)
            {
                Assert.Equal(expected, version1.Equals(version2));
                Assert.Equal(expected, version1 == version2);
                Assert.Equal(!expected, version1 != version2);
            }

            Assert.Equal(expected, version1.Equals(version2Object));

            if (version2Object != null)
            {
                Assert.Equal(expected, version1Object.GetHashCode() == version2Object.GetHashCode());
            }
        }

        public static IEnumerable<object[]> Parse_Valid_TestData()
        {
            foreach (var prefix in new[] { "", "v",  "V"})
            {
                foreach (var suffix in new[] { "", "-pre", "-pre+metadata", "+metadata"})
                {
                    yield return new object[] { $"{prefix}1{suffix}", new SimpleVersion(1) };
                    yield return new object[] { $"{prefix}1.2{suffix}", new SimpleVersion(1, 2) };
                    yield return new object[] { $"{prefix}1.2.3{suffix}", new SimpleVersion(1, 2, 3) };
                    yield return new object[] { $"{prefix}1.2.3.4{suffix}", new SimpleVersion(1, 2, 3, 4) };
                    yield return new object[] { $"{prefix}2147483647.2147483647.2147483647.2147483647{suffix}", new SimpleVersion(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue) };
                }
            }
        }

        [Theory]
        [MemberData(nameof(Parse_Valid_TestData))]
        public static void Parse_ValidInput_ReturnsExpected(string input, object expected)
        {
            Assert.Equal(expected, SimpleVersion.Parse(input));
        }

        public static IEnumerable<object[]> Parse_Invalid_TestData()
        {
            yield return new object[] { null, typeof(ArgumentNullException) }; // Input is null

            yield return new object[] { "", typeof(FormatException) }; // Input is empty
            yield return new object[] { "1,2,3,4", typeof(FormatException) }; // Input contains invalid separator
            yield return new object[] { "1.2.3.4.5", typeof(FormatException) }; // Input has more than 4 version components

            yield return new object[] { "1." , typeof(FormatException) }; // Input contains empty component
            yield return new object[] { "1.2,", typeof(FormatException) }; // Input contains empty component
            yield return new object[] { "1.2.3.", typeof(FormatException) }; // Input contains empty component
            yield return new object[] { "1.2.3.4.", typeof(FormatException) }; // Input contains empty component

            yield return new object[] { "NotAVersion", typeof(FormatException) }; // Input contains non-numeric value
            yield return new object[] { "b.2.3.4", typeof(FormatException) }; // Input contains non-numeric value
            yield return new object[] { "1.b.3.4", typeof(FormatException) }; // Input contains non-numeric value
            yield return new object[] { "1.2.b.4", typeof(FormatException) }; // Input contains non-numeric value
            yield return new object[] { "1.2.3.b", typeof(FormatException) }; // Input contains non-numeric value

            yield return new object[] { "2147483648.2.3.4", typeof(FormatException) }; // Input contains a value > int.MaxValue
            yield return new object[] { "1.2147483648.3.4", typeof(FormatException) }; // Input contains a value > int.MaxValue
            yield return new object[] { "1.2.2147483648.4", typeof(FormatException) }; // Input contains a value > int.MaxValue
            yield return new object[] { "1.2.3.2147483648", typeof(FormatException) }; // Input contains a value > int.MaxValue

            // System.Version allows whitespace around components, but we don't
            yield return new object[] { "2  .3.    4.  \t\r\n15  ", typeof(FormatException) };
            yield return new object[] { "   2  .3.    4.  \t\r\n15  ", typeof(FormatException) };

            // System.Version rejects these because they have negative values, but SimpleVersion strips interprest as semver prerelease to strip
            // They are still invalid because the stripping leaves a empty components behind which are also not allowed as above.
            yield return new object[] { "-1.2.3.4", typeof(FormatException) };
            yield return new object[] { "1.-2.3.4", typeof(FormatException) };
            yield return new object[] { "1.2.-3.4", typeof(FormatException) };
            yield return new object[] { "1.2.3.-4", typeof(FormatException) };

            // System.Version treats this as 1.2.3.4, but SimpleVersion interprets as semver metadata to ignore yielding invalid empty string
            // System.Version parses allowing leading sign, but we treat both sign indicators as beginning of semver part to strip.
            yield return new object[] { "+1.+2.+3.+4", typeof(FormatException) };

            // Only one 'v' allowed as prefix
            yield return new object[] { "vv1.2.3.4", typeof(FormatException) };
        }

        [Theory]
        [MemberData(nameof(Parse_Invalid_TestData))]
        public static void Parse_InvalidInput_ThrowsException(string input, Type exceptionType)
        {
            Assert.Throws(exceptionType, () => SimpleVersion.Parse(input));
        }

        public static IEnumerable<object[]> ToString_TestData()
        {
            yield return new object[] { new SimpleVersion(1), "1.0.0.0"};
            yield return new object[] { new SimpleVersion(1, 2), "1.2.0.0" };
            yield return new object[] { new SimpleVersion(1, 2, 3), "1.2.3.0" };
            yield return new object[] { new SimpleVersion(1, 2, 3, 4), "1.2.3.4" };
        }

        [Theory]
        [MemberData(nameof(ToString_TestData))]
        public static void ToString_Invoke_ReturnsExpected(object versionObject, string expected)
        {
            var version = (SimpleVersion)versionObject;

            Assert.Equal(expected, version.ToString());
        }

        private static void VerifyVersion(SimpleVersion version, int major, int minor, int build, int revision)
        {
            Assert.Equal(major, version.Major);
            Assert.Equal(minor, version.Minor);
            Assert.Equal(build, version.Build);
            Assert.Equal(revision, version.Revision);
        }
    }
}
