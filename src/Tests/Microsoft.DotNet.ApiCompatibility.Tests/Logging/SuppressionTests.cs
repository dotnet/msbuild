// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiCompatibility.Logging.Tests
{
    public class SuppressionTests
    {
        public static IEnumerable<object[]> GetEqualData()
        {
            yield return new object[] { new Suppression(string.Empty), new Suppression(string.Empty) { Left = null, Right = null, Target = null} };
            yield return new object[] { new Suppression(string.Empty), new Suppression(string.Empty) { Left = string.Empty, Right = string.Empty, Target = string.Empty} };
            yield return new object[] { new Suppression("PK004"), new Suppression("pk004") };
            yield return new object[] { new Suppression("PK004"), new Suppression(" pk004 ") };
            yield return new object[] { new Suppression("PK004") { Target = "A.B" }, new Suppression(" pk004 ") { Target = "A.b " } };
            yield return new object[] { new Suppression("PK004") { Target = "A.B", Left = "ref/net6.0/myLib.dll" }, new Suppression(" pk004 ") { Target = "A.B", Left = "ref/net6.0/mylib.dll" } };
            yield return new object[] { new Suppression("PK004") { Target = "A.B", Left = "ref/net6.0/myLib.dll", Right = "lib/net6.0/myLib.dll" }, new Suppression("PK004") { Target = "A.B", Left = "ref/net6.0/myLib.dll", Right = "lib/net6.0/myLib.dll", IsBaselineSuppression = false } };
            yield return new object[] { new Suppression("PK004") { Target = "A.B", Left = "ref/net6.0/myLib.dll", Right = "lib/net6.0/myLib.dll", IsBaselineSuppression = false }, new Suppression("PK004") { Target = "A.B", Left = "ref/net6.0/myLib.dll", Right = "lib/net6.0/myLib.dll", IsBaselineSuppression = false } };
            yield return new object[] { new Suppression("PK004") { Target = "A.B", Left = "ref/net6.0/myLib.dll", Right = "lib/net6.0/myLib.dll", IsBaselineSuppression = true }, new Suppression("PK004") { Target = "A.B", Left = "ref/net6.0/myLib.dll", Right = "lib/net6.0/myLib.dll", IsBaselineSuppression = true } };
        }

        public static IEnumerable<object[]> GetDifferentData()
        {
            yield return new object[] { new Suppression(string.Empty), new Suppression("PK005") };
            yield return new object[] { new Suppression("PK004"), new Suppression("PK005") };
            yield return new object[] { new Suppression("PK004"), new Suppression("PK004") { Target = "A.B()" } };
            yield return new object[] { new Suppression("PK004") { Target = "A.B" }, new Suppression("PK004") { Target = "A.B()" } };
            yield return new object[] { new Suppression("PK004") { Target = "A.C" }, new Suppression("PK004") { Target = "A.B()" } };
            yield return new object[] { new Suppression("PK004") { Target = "A.B()", Left = "ref/net6.0/myLib.dll" }, new Suppression("PK004") { Target = "A.B()" } };
            yield return new object[] { new Suppression("PK004") { Target = "A.B()", Left = "ref/net6.0/myLib.dll" }, new Suppression("PK004") { Target = "A.B()", Left = "lib/net6.0/myLib.dll" } };
            yield return new object[] { new Suppression("PK004") { Target = "A.B()", Right = "ref/net6.0/myLib.dll" }, new Suppression("PK004") { Target = "A.B()" } };
            yield return new object[] { new Suppression("PK004") { Target = "A.B()", Right = "ref/net6.0/myLib.dll" }, new Suppression("PK004") { Target = "A.B()", Right = "lib/net6.0/myLib.dll" } };
            yield return new object[] { new Suppression("PK004") { Target = "A.B()", Left = "ref/net6.0/mylib.dll", Right = "lib/net6.0/myLib.dll" }, new Suppression("PK004") { Target = "A.B()", Left = "ref/netstandard2.0/mylib.dll", Right = "lib/net6.0/myLib.dll" } };
            yield return new object[] { new Suppression("PK004") { Target = "A.B", Left = "ref/net6.0/myLib.dll", Right = "lib/net6.0/myLib.dll", IsBaselineSuppression = true }, new Suppression("PK004") { Target = "A.B", Left = "ref/net6.0/myLib.dll", Right = "lib/net6.0/myLib.dll", IsBaselineSuppression = false } };
            yield return new object[] { new Suppression("PK004") { Target = "A.B", Left = "ref/net6.0/myLib.dll", Right = "lib/net6.0/myLib.dll" }, new Suppression("PK004") { Target = "A.B", Left = "ref/net6.0/myLib.dll", Right = "lib/net6.0/myLib.dll", IsBaselineSuppression = true } };
        }

        [Theory]
        [MemberData(nameof(GetEqualData))]
        public void CheckSuppressionsAreEqual(Suppression suppression, Suppression other)
        {
            Assert.True(suppression.Equals(other));
            Assert.True(other.Equals(suppression));
        }

        [Theory]
        [MemberData(nameof(GetDifferentData))]
        public void CheckSuppressionsAreNotEqual(Suppression suppression, Suppression other)
        {
            Assert.False(suppression.Equals(other));
            Assert.False(other.Equals(suppression));
        }

        [Fact]
        public void CheckSuppressionIsNotEqualWithNull()
        {
            Assert.False(new Suppression("PK0004").Equals(null));
        }
    }
}
