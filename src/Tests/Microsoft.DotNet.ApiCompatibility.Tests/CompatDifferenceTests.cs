// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiCompatibility.Tests
{
    public class CompatDifferenceTests
    {
        public static IEnumerable<object[]> CompatDifferencesData =>
            new object[][]
            {
                new object[]
                {
                    MetadataInformation.DefaultLeft, MetadataInformation.DefaultRight, DiagnosticIds.TypeMustExist, "Type Foo exists on left but not on right", "T:Foo", DifferenceType.Added,
                },
                new object[]
                {
                    MetadataInformation.DefaultLeft, MetadataInformation.DefaultRight, DiagnosticIds.MemberMustExist, "Member Foo.Blah exists on right but not on left", "M:Foo.Blah", DifferenceType.Removed,
                },
                new object[]
                {
                    MetadataInformation.DefaultLeft, MetadataInformation.DefaultRight, "CP320", string.Empty, "F:Blah.Blah", DifferenceType.Changed
                }
            };

        [Theory]
        [MemberData(nameof(CompatDifferencesData))]
        public void PropertiesAreCorrect(MetadataInformation left, MetadataInformation right, string diagId, string message, string memberId, DifferenceType type)
        {
            CompatDifference difference = new(left, right, diagId, message, type, memberId);
            Assert.Equal(left, difference.Left);
            Assert.Equal(right, difference.Right);
            Assert.Equal(diagId, difference.DiagnosticId);
            Assert.Equal(message, difference.Message);
            Assert.Equal(memberId, difference.ReferenceId);
            Assert.Equal(type, difference.Type);

            Assert.Equal($"{diagId} : {message}", difference.ToString());
        }

        [Fact]
        public void IsEquatableWorksAsExpected()
        {
            CompatDifference difference = CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Removed, "T:Foo");
            CompatDifference otherEqual = CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Removed, "T:Foo");
            CompatDifference differentDiagId = CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.CannotAddMemberToInterface, string.Empty, DifferenceType.Removed, "T:Foo");
            CompatDifference differentType = CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Added, "T:Foo");
            CompatDifference differentMemberId = CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.TypeMustExist, string.Empty, DifferenceType.Removed, "T:FooBar");
            CompatDifference differentMessage = CompatDifference.CreateWithDefaultMetadata(DiagnosticIds.TypeMustExist, "Hello", DifferenceType.Removed, "T:Foo");

            Assert.False(difference.Equals(null));
            Assert.True(difference.Equals(otherEqual));
            Assert.True(difference.Equals((object)otherEqual));
            Assert.False(difference.Equals(differentDiagId));
            Assert.False(difference.Equals(differentType));
            Assert.False(difference.Equals(differentMemberId));
            Assert.True(difference.Equals(differentMessage));
        }
    }
}
