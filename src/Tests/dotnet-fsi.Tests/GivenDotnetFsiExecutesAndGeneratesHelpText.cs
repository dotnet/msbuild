// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using System.Linq;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Cli.Fsi.Tests
{
    public class GivenDotnetFsiExecutesAndGeneratesHelpText : SdkTest
    {
        public GivenDotnetFsiExecutesAndGeneratesHelpText(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void ItRuns()
        {
            new DotnetCommand(Log, "fsi")
                .Execute("--help")
                .Should().Pass();
        }
    }
}
