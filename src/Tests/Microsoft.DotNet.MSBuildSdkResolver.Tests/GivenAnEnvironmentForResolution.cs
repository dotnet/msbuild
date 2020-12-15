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
            var environmentProvider = new DotNetSdkResolver.EnvironmentProvider(getPathEnvVarFunc);
            var pathResult = environmentProvider.GetCommandPath("nonexistantCommand");
            pathResult.Should().BeNull();
        }
    }
}
