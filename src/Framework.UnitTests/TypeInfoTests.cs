// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Framework.UnitTests;

public class TypeInfoTests
{
    [Fact]
    public void IsReferenceOrContainsReferences_ReferenceTypes_ReturnsTrue()
    {
        // Reference types should return true
        TypeInfo<string>.IsReferenceOrContainsReferences().ShouldBeTrue();
        TypeInfo<object>.IsReferenceOrContainsReferences().ShouldBeTrue();
        TypeInfo<TypeInfoTests>.IsReferenceOrContainsReferences().ShouldBeTrue();
    }

    [Fact]
    public void IsReferenceOrContainsReferences_ValueTypesWithoutReferences_ReturnsFalse()
    {
        // Value types without references should return false
        TypeInfo<int>.IsReferenceOrContainsReferences().ShouldBeFalse();
        TypeInfo<bool>.IsReferenceOrContainsReferences().ShouldBeFalse();
        TypeInfo<DateTime>.IsReferenceOrContainsReferences().ShouldBeFalse();
        TypeInfo<TestEnum>.IsReferenceOrContainsReferences().ShouldBeFalse();
        TypeInfo<SimpleStruct>.IsReferenceOrContainsReferences().ShouldBeFalse();
    }

    [Fact]
    public void IsReferenceOrContainsReferences_ValueTypesWithReferences_ReturnsTrue()
    {
        // Value types containing references should return true
        TypeInfo<StructWithReference>.IsReferenceOrContainsReferences().ShouldBeTrue();
        TypeInfo<StructWithString>.IsReferenceOrContainsReferences().ShouldBeTrue();
    }

    [Fact]
    public void IsReferenceOrContainsReferences_ResultIsCached()
    {
        // First call should compute the result
        bool firstResult = TypeInfo<int>.IsReferenceOrContainsReferences();

        // Second call should use cached result
        bool secondResult = TypeInfo<int>.IsReferenceOrContainsReferences();

        // Both calls should return the same result
        secondResult.ShouldBe(firstResult);
    }

#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
    private enum TestEnum
    {
        Value1,
        Value2
    }

    private struct SimpleStruct
    {
        public int X;
        public int Y;
    }

    private struct StructWithReference
    {
        public object Reference;
        public int Value;
    }

    private struct StructWithString
    {
        public string Text;
        public double Number;
    }
#pragma warning restore CS0649 // Field is never assigned to, and will always have its default value
}
