// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using NuGet.ProjectModel;
using Xunit;

namespace Microsoft.DotNet.ToolPackage.Tests
{
    public class LockFileMatcherTests
    {
        [Theory]
        [InlineData("tools/netcoreapp1.1/any/tool.dll", "tool.dll", true)]
        [InlineData(@"tools\netcoreapp1.1\any\subDirectory\tool.dll", "subDirectory/tool.dll", true)]
        [InlineData("tools/netcoreapp1.1/win-x64/tool.dll", "tool.dll", true)]
        [InlineData("tools/netcoreapp1.1/any/subDirectory/tool.dll", "subDirectory/tool.dll", true)]
        [InlineData("libs/netcoreapp1.1/any/tool.dll", "tool.dll", false)]
        [InlineData("tools/netcoreapp1.1/any/subDirectory/tool.dll", "tool.dll", false)]
        [InlineData("tools/netcoreapp1.1/any/subDirectory/tool.dll", "subDirectory/subDirectory/subDirectory/subDirectory/subDirectory/tool.dll", false)]
        public void MatchesEntryPointTests(string pathInLockFileItem, string targetRelativeFilePath, bool shouldMatch)
        {
            LockFileMatcher.MatchesFile(new LockFileItem(pathInLockFileItem), targetRelativeFilePath)
                .Should().Be(shouldMatch);
        }


        [Theory]
        [InlineData("tools/netcoreapp1.1/any/tool.dll", "", true)]
        [InlineData(@"tools\netcoreapp1.1\any\subDirectory\tool.dll", "subDirectory", true)]
        [InlineData(@"tools\netcoreapp1.1\any\subDirectory\tool.dll", "sub", false)]
        [InlineData("tools/netcoreapp1.1/any/subDirectory/tool.dll", "any/subDirectory", false)]
        public void MatchesDirectoryPathTests(
            string pathInLockFileItem,
            string targetRelativeFilePath,
            bool shouldMatch)
        {
            LockFileMatcher.MatchesDirectoryPath(new LockFileItem(pathInLockFileItem), targetRelativeFilePath)
                .Should().Be(shouldMatch);
        }
    }
}
