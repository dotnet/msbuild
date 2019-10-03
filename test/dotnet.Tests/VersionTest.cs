// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using FluentAssertions;
using System.Reflection;
using System.Linq;

namespace Microsoft.DotNet.Tests
{
    public class GivenDotnetSdk : TestBase
    {
        [Fact]
        public void VersionCommandDisplaysCorrectVersion()
        {
            var assemblyMetadata = typeof(TestAssetInstanceExtensions).Assembly
                .GetCustomAttributes(typeof(AssemblyMetadataAttribute))
                .Cast<AssemblyMetadataAttribute>()
                .ToDictionary(a => a.Key, a => a.Value);

            var expectedVersion = assemblyMetadata["SdkVersion"];

            CommandResult result = new DotnetCommand()
                    .ExecuteWithCapturedOutput("--version");

            result.Should().Pass();
            result.StdOut.Trim().Should().Be(expectedVersion);
        }
    }
}
