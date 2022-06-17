// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using Xunit;

namespace Microsoft.DotNet.ApiCompatibility.Tests
{
    public class CompatDifferenceTests
    {
        public static IEnumerable<object[]> CompatDifferencesData =>
            new object[][]
            {
                new object[]
                {
                    DiagnosticIds.TypeMustExist, "Type Foo exists on left but not on right", "T:Foo", DifferenceType.Added,
                },
                new object[]
                {
                    DiagnosticIds.MemberMustExist, "Member Foo.Blah exists on right but not on left", "M:Foo.Blah", DifferenceType.Removed,
                },
                new object[]
                {
                    "CP320", string.Empty, "F:Blah.Blah", DifferenceType.Changed
                }
            };

        [Theory]
        [MemberData(nameof(CompatDifferencesData))]
        public void PropertiesAreCorrect(string diagId, string message, string memberId, DifferenceType type)
        {
            CompatDifference difference = new(diagId, message, type, memberId);
            Assert.Equal(diagId, difference.DiagnosticId);
            Assert.Equal(message, difference.Message);
            Assert.Equal(memberId, difference.ReferenceId);
            Assert.Equal(type, difference.Type);

            Assert.Equal($"{diagId} : {message}", difference.ToString());
        }

        [Fact]
        public void IsEquatableWorksAsExpected()
        {
            CompatDifference difference = new(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Removed, "T:Foo");
            CompatDifference otherEqual = new(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Removed, "T:Foo");
            CompatDifference differentDiagId = new(DiagnosticIds.CannotAddMemberToInterface, string.Empty, DifferenceType.Removed, "T:Foo");
            CompatDifference differentType = new(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Added, "T:Foo");
            CompatDifference differentMemberId = new(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Removed, "T:FooBar");
            CompatDifference differentMessage = new(DiagnosticIds.TypeMustExist, "Hello", DifferenceType.Removed, "T:Foo");

            Assert.False(difference.Equals(null));
            Assert.True(difference.Equals(otherEqual));
            Assert.True(difference.Equals((object)otherEqual));
            Assert.False(difference.Equals(differentDiagId));
            Assert.False(difference.Equals(differentType));
            Assert.False(difference.Equals(differentMemberId));
            Assert.True(difference.Equals(differentMessage));
        }

        [Fact]
        public void ConstructorThrowsExpected()
        {
            Assert.Throws<ArgumentNullException>("diagnosticId", () => new CompatDifference(null, string.Empty, DifferenceType.Added, string.Empty));
            Assert.Throws<ArgumentNullException>("message", () => new CompatDifference(string.Empty, null, DifferenceType.Added, string.Empty));
            Assert.Throws<ArgumentNullException>("memberId", () => new CompatDifference(string.Empty, string.Empty, DifferenceType.Added, (ISymbol)null));
        }
    }
}
