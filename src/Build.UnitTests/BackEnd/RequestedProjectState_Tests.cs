// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using FluentAssertions;
using Microsoft.Build.Execution;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.UnitTests.BackEnd
{
    public class RequestedProjectState_Tests
    {
        [Fact]
        public void DeepCloneEmpty()
        {
            RequestedProjectState state = new();
            RequestedProjectState clone = state.DeepClone();

            clone.PropertyFilters.Should().BeNull();
            clone.ItemFilters.Should().BeNull();
        }

        [Fact]
        public void DeepCloneProperties()
        {
            List<string> properties = ["prop1", "prop2"];
            RequestedProjectState state = new()
            {
                PropertyFilters = properties,
            };
            RequestedProjectState clone = state.DeepClone();

            clone.PropertyFilters.Should().BeEquivalentTo(properties);
            clone.ItemFilters.Should().BeNull();

            // Mutating the original instance is not reflected in the clone.
            properties.Add("prop3");
            clone.PropertyFilters.Count.Should().NotBe(properties.Count);
        }

        [Fact]
        public void DeepCloneItemsNoMetadata()
        {
            Dictionary<string, List<string>> items = new()
            {
                { "item1", null! },
                { "item2", null! },
            };
            RequestedProjectState state = new()
            {
                ItemFilters = items,
            };
            RequestedProjectState clone = state.DeepClone();

            clone.PropertyFilters.Should().BeNull();
            clone.ItemFilters.Should().BeEquivalentTo(items);

            // Mutating the original instance is not reflected in the clone.
            items.Add("item3", null!);
            clone.ItemFilters.Count.Should().NotBe(items.Count);
        }

        [Fact]
        public void DeepCloneItemsWithMetadata()
        {
            Dictionary<string, List<string>> items = new()
            {
                { "item1", ["metadatum1", "metadatum2"] },
                { "item2", ["metadatum3"] },
            };
            RequestedProjectState state = new()
            {
                ItemFilters = items,
            };
            RequestedProjectState clone = state.DeepClone();

            clone.PropertyFilters.Should().BeNull();
            clone.ItemFilters.Should().BeEquivalentTo(items);

            // Mutating the original instance is not reflected in the clone.
            items["item2"].Add("metadatum4");
            clone.ItemFilters["item2"].Count.Should().NotBe(items["item2"].Count);
        }

        [Fact]
        public void IsSubsetOfEmpty()
        {
            RequestedProjectState state1 = new();
            RequestedProjectState state2 = new();

            // Empty instances are subsets of each other.
            state1.IsSubsetOf(state2).Should().BeTrue();
            state2.IsSubsetOf(state1).Should().BeTrue();

            state1.PropertyFilters = ["prop1"];
            state1.ItemFilters = new Dictionary<string, List<string>>()
            {
                { "item1", null! },
            };

            // Non-empty instance is a subset of empty instance but not the other way round.
            state1.IsSubsetOf(state2).Should().BeTrue();
            state2.IsSubsetOf(state1).Should().BeFalse();
        }

        [Fact]
        public void IsSubsetOfProperties()
        {
            RequestedProjectState state1 = new()
            {
                PropertyFilters = ["prop1"],
            };
            RequestedProjectState state2 = new()
            {
                PropertyFilters = ["prop1", "prop2"],
            };

            // "prop1" is a subset of "prop1", "prop2".
            state1.IsSubsetOf(state2).Should().BeTrue();
            state2.IsSubsetOf(state1).Should().BeFalse();

            state1.PropertyFilters.Add("prop3");

            // Disjoint sets are not subsets of each other.
            state1.IsSubsetOf(state2).Should().BeFalse();
            state2.IsSubsetOf(state1).Should().BeFalse();

            state1.PropertyFilters.Clear();

            // Empty props is a subset of anything.
            state1.IsSubsetOf(state2).Should().BeTrue();
            state2.IsSubsetOf(state1).Should().BeFalse();
        }

        [Fact]
        public void IsSubsetOfItemsNoMetadata()
        {
            RequestedProjectState state1 = new()
            {
                ItemFilters = new Dictionary<string, List<string>>()
                {
                    { "item1", null! },
                },
            };
            RequestedProjectState state2 = new()
            {
                ItemFilters = new Dictionary<string, List<string>>()
                {
                    { "item1", null! },
                    { "item2", null! },
                },
            };

            // "item1" is a subset of "item1", "item2".
            state1.IsSubsetOf(state2).Should().BeTrue();
            state2.IsSubsetOf(state1).Should().BeFalse();

            state1.ItemFilters.Add("item3", null!);

            // Disjoint sets are not subsets of each other.
            state1.IsSubsetOf(state2).Should().BeFalse();
            state2.IsSubsetOf(state1).Should().BeFalse();

            state1.ItemFilters.Clear();

            // Empty items is a subset of anything.
            state1.IsSubsetOf(state2).Should().BeTrue();
            state2.IsSubsetOf(state1).Should().BeFalse();
        }

        [Fact]
        public void IsSubsetOfItemsWithMetadata()
        {
            RequestedProjectState state1 = new()
            {
                ItemFilters = new Dictionary<string, List<string>>()
                {
                    { "item1", ["metadatum1"] },
                },
            };
            RequestedProjectState state2 = new()
            {
                ItemFilters = new Dictionary<string, List<string>>()
                {
                    { "item1", null! },
                },
            };

            // "item1" with "metadatum1" is a subset of "item1" with no metadata filter.
            state1.IsSubsetOf(state2).Should().BeTrue();
            state2.IsSubsetOf(state1).Should().BeFalse();

            state2.ItemFilters["item1"] = ["metadatum2"];

            // Disjoint metadata filters are not subsets of each other.
            state1.IsSubsetOf(state2).Should().BeFalse();
            state2.IsSubsetOf(state1).Should().BeFalse();

            state1.ItemFilters["item1"] = [];

            // Empty metadata filter is a subset of any other metadata filter.
            state1.IsSubsetOf(state2).Should().BeTrue();
            state2.IsSubsetOf(state1).Should().BeFalse();
        }
    }
}
