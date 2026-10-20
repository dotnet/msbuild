// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Evaluation;

public sealed class NormalizedItemPath_Tests
{
    [Theory]
    [InlineData("a.cs", "a.cs", true)]
    [InlineData("a.cs", "A.CS", true)]
    [InlineData("a.cs", "b.cs", false)]
    [InlineData("", "", true)]
    public void EqualityUsesOrdinalIgnoreCasePaths(string leftValue, string rightValue, bool equal)
    {
        NormalizedItemPath left = new(leftValue);
        NormalizedItemPath right = new(rightValue);

        left.IsDefault.ShouldBeFalse();
        left.GetHashCode().ShouldBe(StringComparer.OrdinalIgnoreCase.GetHashCode(leftValue));
        left.Equals(right).ShouldBe(equal);
        if (equal)
        {
            left.GetHashCode().ShouldBe(right.GetHashCode());
        }
    }

    [Fact]
    public void DefaultKeysRejectPathAccess()
    {
        NormalizedItemPath path = default;
        path.IsDefault.ShouldBeTrue();
        Should.Throw<InvalidOperationException>(() => { _ = path.Value; });
    }

    [Fact]
    public void EquivalentNormalizedPathsDeduplicateInSets()
    {
        string directory = Path.GetTempPath();
        NormalizedItemPath first = new(FileUtilities.NormalizePathForComparisonNoThrow("a.cs", directory));
        NormalizedItemPath equivalent = new(FileUtilities.NormalizePathForComparisonNoThrow("./A.CS", directory));
        NormalizedItemPath different = new(FileUtilities.NormalizePathForComparisonNoThrow("b.cs", directory));
        HashSet<NormalizedItemPath> paths = [first, equivalent, different];

        paths.Count.ShouldBe(2);
        paths.Contains(first).ShouldBeTrue();
        paths.Contains(equivalent).ShouldBeTrue();
        paths.Remove(equivalent).ShouldBeTrue();
        paths.ShouldBe([different]);
    }
}
