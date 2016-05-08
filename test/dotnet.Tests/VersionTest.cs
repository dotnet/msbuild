// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using FluentAssertions;

namespace Microsoft.DotNet.Tests
{
    public class GivenDotnetSdk : TestBase
    {
        [Fact]
        public void VersionCommandDisplaysCorrectVersion()
        {
            CommandResult result = new DotnetCommand()
                    .ExecuteWithCapturedOutput("--version");

            result.Should().Pass();
            Regex.IsMatch(result.StdOut.Trim(), @"[0-9]{1}\.[0-9]{1}\.[0-9]{1}-[a-zA-Z0-9]+-[0-9]{6}$").Should()
                .BeTrue($"Unexpected dotnet sdk version - {result.StdOut}");

        }
    }
}
