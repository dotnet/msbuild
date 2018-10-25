// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Reflection;
using FluentAssertions;
using Microsoft.Build.Framework;
using Microsoft.Extensions.DependencyModel;
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
