// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Evaluation;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests.OM.Definition
{
    /// <summary>
    /// Tests the <see cref="ItemDataCollectionValue{I}"/> data type.
    /// </summary>
    public class ItemDataCollectionValue_Tests
    {
        private int[] MakeArray(ItemDataCollectionValue<int> value)
        {
            List<int> result = new List<int>();
            foreach (int i in value)
            {
                result.Add(i);
            }
            return result.ToArray();
        }

        [Fact]
        public void RepresentsSingleItem()
        {
            var value = new ItemDataCollectionValue<int>(1);
            value.IsEmpty.ShouldBeFalse();
            MakeArray(value).ShouldBe(new[] { 1 });
        }

        [Fact]
        public void AddsSecondItem()
        {
            var value = new ItemDataCollectionValue<int>(1);
            value.Add(2);
            value.IsEmpty.ShouldBeFalse();
            MakeArray(value).ShouldBe(new[] { 1, 2 });
        }

        [Fact]
        public void DeletesSingleItem()
        {
            var value = new ItemDataCollectionValue<int>(1);
            value.Delete(1);
            value.IsEmpty.ShouldBeTrue();
            MakeArray(value).ShouldBe(Array.Empty<int>());
        }

        [Fact]
        public void DeletesFirstItem()
        {
            var value = new ItemDataCollectionValue<int>(1);
            value.Add(2);
            value.Delete(1);
            value.IsEmpty.ShouldBeFalse();
            MakeArray(value).ShouldBe(new[] { 2 });
        }

        [Fact]
        public void DeletesSecondItem()
        {
            var value = new ItemDataCollectionValue<int>(1);
            value.Add(2);
            value.Delete(2);
            value.IsEmpty.ShouldBeFalse();
            MakeArray(value).ShouldBe(new[] { 1 });
        }

        [Fact]
        public void DeletesNonExistentItem()
        {
            var value = new ItemDataCollectionValue<int>(1);
            value.Add(2);
            value.Delete(3);
            value.IsEmpty.ShouldBeFalse();
            MakeArray(value).ShouldBe(new[] { 1, 2 });
        }

        [Fact]
        public void ReplacesSingleItem()
        {
            var value = new ItemDataCollectionValue<int>(1);
            value.Replace(1, 11);
            value.IsEmpty.ShouldBeFalse();
            MakeArray(value).ShouldBe(new[] { 11 });
        }

        [Fact]
        public void ReplacesFirstItem()
        {
            var value = new ItemDataCollectionValue<int>(1);
            value.Add(2);
            value.Replace(1, 11);
            value.IsEmpty.ShouldBeFalse();
            MakeArray(value).ShouldBe(new[] { 11, 2 });
        }

        [Fact]
        public void ReplacesSecondItem()
        {
            var value = new ItemDataCollectionValue<int>(1);
            value.Add(2);
            value.Replace(2, 22);
            value.IsEmpty.ShouldBeFalse();
            MakeArray(value).ShouldBe(new[] { 1, 22 });
        }

        [Fact]
        public void ReplacesNonExistentItem()
        {
            var value = new ItemDataCollectionValue<int>(1);
            value.Add(2);
            value.Replace(3, 33);
            value.IsEmpty.ShouldBeFalse();
            MakeArray(value).ShouldBe(new[] { 1, 2 });
        }
    }
}
