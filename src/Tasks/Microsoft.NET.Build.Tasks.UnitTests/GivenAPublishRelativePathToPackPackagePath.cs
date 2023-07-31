// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAResolveToolPackagePaths
    {
        [Theory]
        [InlineData("tools/myfile.exe", "tools")]
        [InlineData(@"tools\myfile.exe", "tools")]
        [InlineData(@"tools\/myfile.exe", "tools")]
        [InlineData(@"tools/\myfile.exe", "tools")]
        [InlineData(@"myfile.exe", "")]
        [InlineData(@"myfile", "")]
        [InlineData("tools/myfile", "tools")]
        [InlineData("/myfile", "")]
        [InlineData("\\myfile", "")]
        [InlineData("tools/sub/myfile.exe", "tools/sub")]
        [InlineData("tools\\sub\\myfile.exe", "tools/sub")]
        public void ItConvertsFromPublishRelativePathToPackPackagePath(string publishRelativePath, string packPackagePath)
        {
            ResolveToolPackagePaths.GetDirectoryPathInRelativePath(publishRelativePath).Should().Be(packPackagePath);
        }
    }
}
