// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Logging;
using Shouldly;
using Xunit;

#nullable enable

namespace Microsoft.Build.UnitTests
{
    public class BinaryLogger_PathTraversal_Tests
    {
        [Theory]
        [InlineData("..")]
        [InlineData("../etc/passwd")]
        [InlineData("..\\Windows\\System32\\evil.dll")]
        [InlineData("C/Users/jan/sub/../../../escape.txt")]
        [InlineData("dir/../../up")]
        [InlineData("a\\..\\b")]
        public void IsPathTraversal_WithDotDotSegment_ReturnsTrue(string entryPath)
        {
            BuildEventArgsReader.IsPathTraversal(entryPath).ShouldBeTrue();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("C/Users/jan/project.csproj")]
        [InlineData("Program Files/dotnet/sdk/Sdk.props")]
        [InlineData("a/b/c.targets")]
        [InlineData("file..with..dots.txt")]
        [InlineData("..hidden")]
        [InlineData("trailing..")]
        public void IsPathTraversal_WithLegitimateSegments_ReturnsFalse(string? entryPath)
        {
            BuildEventArgsReader.IsPathTraversal(entryPath).ShouldBeFalse();
        }
    }
}
