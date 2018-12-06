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
                .WithRuntime("win7-x64")
                .WithWorkingDirectory(testInstance.Root)
                .Execute()
                .Should().Pass();

            new FsiCommand()
                .WithWorkingDirectory(testInstance.Root)
                .Execute("--help")
                .Should().Pass();
        }
    }
}