// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Tests.Commands
{
    public class CommandIntegrationTests : SdkTest
    {
        public CommandIntegrationTests(ITestOutputHelper log) : base(log) {}

        [Fact]
        public void GivenNoArgumentsProvided()
        {
            var cmd = new DotnetCommand(Log).Execute(string.Empty);
            cmd.StdErr.Should().BeEmpty();
        }

        [Fact]
        public void GivenOnlyArgumentProvidedIsDiagnosticsFlag()
        {
            var cmd = new DotnetCommand(Log).Execute("-d");
            cmd.ExitCode.Should().Be(0);
            cmd.StdErr.Should().BeEmpty();
        }
    }
}
