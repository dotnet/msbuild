// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace Microsoft.NET.Sdk.Razor.SourceGenerators.Tests
{
    public class RazorSourceGeneratorComparerTest
    {
        [Fact]
        public void Equals_ReturnsTrue_IfSuppressRazorSourceGeneratorOnRightIsTrue()
        {
            // Arrange
            var left = (new ComparableType(), new RazorSourceGenerationOptions());
            var right = (new ComparableType(), new RazorSourceGenerationOptions { SuppressRazorSourceGenerator = true });

            var razorComparer = new RazorSourceGeneratorComparer<ComparableType>();

            // Act
            var result = razorComparer.Equals(left, right);

            // Assert
            Assert.True(result);
            Assert.False(right.Item1.EqualsCalled);
        }

        [Fact]
        public void Equals_ComparesWithoutSuppressRazorSourceGenerator()
        {
            // Arrange
            var left = (new ComparableType(), new RazorSourceGenerationOptions { SuppressRazorSourceGenerator = true /* This is ignored */});
            var right = (new ComparableType(), new RazorSourceGenerationOptions());

            var razorComparer = new RazorSourceGeneratorComparer<ComparableType>();

            // Act
            var result = razorComparer.Equals(left, right);

            // Assert
            Assert.True(result);
            Assert.True(left.Item1.EqualsCalled);
        }

        [Fact]
        public void Equals_UsesProvidedComparer()
        {
            // Arrange
            var left = (new ComparableType(), new RazorSourceGenerationOptions { SuppressRazorSourceGenerator = true /* This is ignored */});
            var right = (new ComparableType(), new RazorSourceGenerationOptions());
            var invoked = false;

            var razorComparer = new RazorSourceGeneratorComparer<ComparableType>((a, b) =>
            {
                invoked = true;
                return true;
            });

            // Act
            var result = razorComparer.Equals(left, right);

            // Assert
            Assert.True(result);
            Assert.False(left.Item1.EqualsCalled);
            Assert.True(invoked);
        }

        [Fact]
        public void Equals_ReturnsFalse_IfItemComparersReturnFalse()
        {
            // Arrange
            var left = (new ComparableType(), new RazorSourceGenerationOptions());
            var right = (new ComparableType { Value = "Different" }, new RazorSourceGenerationOptions());

            var razorComparer = new RazorSourceGeneratorComparer<ComparableType>();

            // Act
            var result = razorComparer.Equals(left, right);

            // Assert
            Assert.False(result);
            Assert.True(left.Item1.EqualsCalled);
        }

        [Fact]
        public void GetHashCode_ReturnsValueFromItem()
        {
            // Arrange
            var item = (new ComparableType(), new RazorSourceGenerationOptions());

            var razorComparer = new RazorSourceGeneratorComparer<ComparableType>();

            // Act
            var result = razorComparer.GetHashCode(item);

            // Assert
            Assert.Equal(42, result);
        }

        private class ComparableType : IEquatable<ComparableType>
        {
            public string Value { get; set; } = "Some value";

            public bool EqualsCalled = false;

            public bool Equals(ComparableType other)
            {
                EqualsCalled = true;
                return Value.Equals(other.Value);
            }

            public override int GetHashCode() => 42;

            public override bool Equals(object obj) => obj is ComparableType other && Equals(other);
        }
    }
}
