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
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit.Abstractions;


namespace Microsoft.DotNet.Tests
{
    public class GivenDotnetSdk : SdkTest
    {
        public GivenDotnetSdk(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void VersionCommandDisplaysCorrectVersion()
        {
            var assemblyMetadata = typeof(GivenDotnetSdk).Assembly
                .GetCustomAttributes(typeof(AssemblyMetadataAttribute))
                .Cast<AssemblyMetadataAttribute>()
                .ToDictionary(a => a.Key, a => a.Value);

            var expectedVersion = assemblyMetadata["SdkVersion"];

            CommandResult result = new DotnetCommand(Log)
                    .Execute("--version");

            result.Should().Pass();
            result.StdOut.Trim().Should().Be(expectedVersion);
        }
    }
}
