// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.NET.TestFramework;
using NuGet.ProjectModel;
using Xunit;

namespace Microsoft.DotNet.PackageInstall.Tests
{
    public class LockFileMatcherTests
    {
        [Theory]
        [InlineData($"tools/{ToolsetInfo.CurrentTargetFramework}/any/tool.dll", "tool.dll", true)]
        [InlineData($@"tools\{ToolsetInfo.CurrentTargetFramework}\any\subDirectory\tool.dll", "subDirectory/tool.dll", true)]
        [InlineData($"tools/{ToolsetInfo.CurrentTargetFramework}/win-x64/tool.dll", "tool.dll", true)]
        [InlineData($"tools/{ToolsetInfo.CurrentTargetFramework}/any/subDirectory/tool.dll", "subDirectory/tool.dll", true)]
        [InlineData($"libs/{ToolsetInfo.CurrentTargetFramework}/any/tool.dll", "tool.dll", false)]
        [InlineData($"tools/{ToolsetInfo.CurrentTargetFramework}1/any/subDirectory/tool.dll", "tool.dll", false)]
        [InlineData($"tools/{ToolsetInfo.CurrentTargetFramework}/any/subDirectory/tool.dll", "subDirectory/subDirectory/subDirectory/subDirectory/subDirectory/tool.dll", false)]
        public void MatchesEntryPointTests(string pathInLockFileItem, string targetRelativeFilePath, bool shouldMatch)
        {
            LockFileMatcher.MatchesFile(new LockFileItem(pathInLockFileItem), targetRelativeFilePath)
                .Should().Be(shouldMatch);
        }


        [Theory]
        [InlineData($"tools/{ToolsetInfo.CurrentTargetFramework}/any/tool.dll", "", true)]
        [InlineData($@"tools\{ToolsetInfo.CurrentTargetFramework}\any\subDirectory\tool.dll", "subDirectory", true)]
        [InlineData($@"tools\{ToolsetInfo.CurrentTargetFramework}\any\subDirectory\tool.dll", "sub", false)]
        [InlineData($"tools/{ToolsetInfo.CurrentTargetFramework}/any/subDirectory/tool.dll", "any/subDirectory", false)]
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
