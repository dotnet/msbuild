// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using Xunit;

namespace Microsoft.DotNet.ApiCompatibility.Tests
{
    internal static class AssertExtensions
    {
        internal static void MultiRightResult(MetadataInformation expectedLeft, CompatDifference[][] expectedDifferences, IEnumerable<(MetadataInformation, MetadataInformation, IEnumerable<CompatDifference>)> actual)
        {
            var i = 0;
            Assert.Equal(expectedDifferences.Length, actual.Count());
            foreach ((MetadataInformation left, MetadataInformation right, IEnumerable<CompatDifference> differences) a in actual)
            {
                Assert.Equal(expectedLeft, a.left);

                MetadataInformation expectedRight = new(string.Empty, string.Empty, $"runtime-{i}");
                Assert.Equal(expectedRight, a.right);

                CompatDifference[] expectedDiff = expectedDifferences[i++];
                Assert.Equal(expectedDiff, a.differences, CompatDifferenceComparer.Default);
            }
        }

        internal static void MultiRightEmptyDifferences(MetadataInformation expectedLeft, int rightCount, IEnumerable<(MetadataInformation, MetadataInformation, IEnumerable<CompatDifference>)> differences)
        {
            int i = 0;
            foreach ((MetadataInformation left, MetadataInformation right, IEnumerable<CompatDifference> differences) diff in differences)
            {
                Assert.Equal(expectedLeft, diff.left);
                MetadataInformation expectedRightMetadata = new(string.Empty, string.Empty, $"runtime-{i++}");
                Assert.Equal(expectedRightMetadata, diff.right);
                Assert.Empty(diff.differences);
            }

            Assert.Equal(rightCount, i);
        }
    }
}
