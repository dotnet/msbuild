// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Tests for <see cref="TypeInfo{T}"/>.
    /// </summary>
    public class TypeInfo_Tests
    {
        [Fact]
        public void PrimitiveTypes_AreNotReferences()
        {
            TypeInfo<int>.IsReferenceOrContainsReferences().ShouldBeFalse();
            TypeInfo<long>.IsReferenceOrContainsReferences().ShouldBeFalse();
            TypeInfo<byte>.IsReferenceOrContainsReferences().ShouldBeFalse();
            TypeInfo<char>.IsReferenceOrContainsReferences().ShouldBeFalse();
            TypeInfo<bool>.IsReferenceOrContainsReferences().ShouldBeFalse();
            TypeInfo<double>.IsReferenceOrContainsReferences().ShouldBeFalse();
        }

        [Fact]
        public void DateTime_IsNotReference()
        {
            TypeInfo<DateTime>.IsReferenceOrContainsReferences().ShouldBeFalse();
        }

        [Fact]
        public void EnumTypes_AreNotReferences()
        {
            TypeInfo<DayOfWeek>.IsReferenceOrContainsReferences().ShouldBeFalse();
            TypeInfo<StringComparison>.IsReferenceOrContainsReferences().ShouldBeFalse();
        }

        [Fact]
        public void StringType_IsReference()
        {
            TypeInfo<string>.IsReferenceOrContainsReferences().ShouldBeTrue();
        }

        [Fact]
        public void ObjectType_IsReference()
        {
            TypeInfo<object>.IsReferenceOrContainsReferences().ShouldBeTrue();
        }

        [Fact]
        public void ReferenceType_IsReference()
        {
            TypeInfo<List<int>>.IsReferenceOrContainsReferences().ShouldBeTrue();
        }

        [Fact]
        public void ContainingTestType_IsReference()
        {
            TypeInfo<TypeInfo_Tests>.IsReferenceOrContainsReferences().ShouldBeTrue();
        }

        [Fact]
        public void StructContainingReference_ContainsReferences()
        {
            TypeInfo<StructWithReference>.IsReferenceOrContainsReferences().ShouldBeTrue();
        }

        [Fact]
        public void StructContainingString_ContainsReferences()
        {
            TypeInfo<StructWithString>.IsReferenceOrContainsReferences().ShouldBeTrue();
        }

        [Fact]
        public void SimpleStruct_DoesNotContainReferences()
        {
            TypeInfo<SimpleStruct>.IsReferenceOrContainsReferences().ShouldBeFalse();
        }

        [Fact]
        public void PureValueStruct_DoesNotContainReferences()
        {
            TypeInfo<PureValueStruct>.IsReferenceOrContainsReferences().ShouldBeFalse();
        }

        [Fact]
        public void Result_IsCached_ReturnsConsistentValue()
        {
            // Call multiple times to exercise the cached code path.
            bool first = TypeInfo<string>.IsReferenceOrContainsReferences();
            bool second = TypeInfo<string>.IsReferenceOrContainsReferences();
            bool third = TypeInfo<string>.IsReferenceOrContainsReferences();

            first.ShouldBe(second);
            second.ShouldBe(third);
            first.ShouldBeTrue();
        }

        [Fact]
        public void Result_IsCached_ForValueType_ReturnsConsistentValue()
        {
            bool first = TypeInfo<int>.IsReferenceOrContainsReferences();
            bool second = TypeInfo<int>.IsReferenceOrContainsReferences();

            second.ShouldBe(first);
            first.ShouldBeFalse();
        }

        private struct StructWithReference
        {
#pragma warning disable CS0649 // Field is never assigned — only the layout matters for the test
            public object Reference;
            public int Value;
#pragma warning restore CS0649
        }

        private struct StructWithString
        {
#pragma warning disable CS0649 // Field is never assigned — only the layout matters for the test
            public string Text;
            public double Number;
#pragma warning restore CS0649
        }

        private struct SimpleStruct
        {
#pragma warning disable CS0649 // Field is never assigned — only the layout matters for the test
            public int X;
            public int Y;
#pragma warning restore CS0649
        }

        private struct PureValueStruct
        {
#pragma warning disable CS0649 // Field is never assigned — only the layout matters for the test
            public int A;
            public long B;
            public double C;
#pragma warning restore CS0649
        }
    }
}
