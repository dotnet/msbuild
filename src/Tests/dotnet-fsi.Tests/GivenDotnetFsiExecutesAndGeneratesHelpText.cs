// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
