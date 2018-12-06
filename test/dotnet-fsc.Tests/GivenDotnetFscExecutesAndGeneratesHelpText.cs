// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using System.Linq;

namespace Microsoft.DotNet.Cli.Fsc.Tests
{
    public class GivenDotnetFscExecutesAndGeneratesHelpText : TestBase
    {
        [Fact]
        public void ItRuns()
        {
            new FscCommand()
                .Execute()
                .Should().Pass();

            new FscCommand()
                .Execute("--help")
                .Should().Pass();
        }
    }
}