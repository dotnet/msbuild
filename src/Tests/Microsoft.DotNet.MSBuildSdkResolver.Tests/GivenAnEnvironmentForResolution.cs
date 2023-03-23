// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace Microsoft.DotNet.Cli.Utils.Tests
{
    public class GivenAnEnvironmentForResolution
    {
        [Fact]
        public void ItIgnoresInvalidPath()
        {
            Func<string, string> getPathEnvVarFunc = (string var) => { return $"{Directory.GetCurrentDirectory()}Dir{Path.GetInvalidPathChars().First()}Name"; };
            var environmentProvider = new NativeWrapper.EnvironmentProvider(getPathEnvVarFunc);
            var pathResult = environmentProvider.GetCommandPath("nonexistantCommand");
            pathResult.Should().BeNull();
        }

        [Fact]
        public void ItDoesNotReturnNullDotnetRootOnExtraPathSeparator()
        {
            File.Create(Path.Combine(Directory.GetCurrentDirectory(), "dotnet.exe"));
            Func<string, string> getPathEnvVarFunc = (input) => input.Equals("PATH") ? $"fake{Path.PathSeparator}" : string.Empty;
            var result = NativeWrapper.EnvironmentProvider.GetDotnetExeDirectory(getPathEnvVarFunc);
            result.Should().NotBeNullOrWhiteSpace();
        }
    }
}
