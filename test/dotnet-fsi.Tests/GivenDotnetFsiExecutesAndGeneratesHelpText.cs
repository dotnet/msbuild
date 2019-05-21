// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using System.Linq;

namespace Microsoft.DotNet.Cli.Fsi.Tests
{
    public class GivenDotnetFsiExecutesAndGeneratesHelpText : TestBase
    {
        [Fact]
        public void ItRuns()
        {
            new FsiCommand()
                .Execute("--help")
                .Should().Pass();
        }
    }
}
