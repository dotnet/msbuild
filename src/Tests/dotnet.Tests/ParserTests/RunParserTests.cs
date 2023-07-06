// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using FluentAssertions;
using Microsoft.DotNet.Tools.Run;
using Xunit;
using Xunit.Abstractions;
using System;

namespace Microsoft.DotNet.Tests.ParserTests
{
    public class RunParserTests
    {
        public RunParserTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        private readonly ITestOutputHelper output;

        [Fact]
        public void RunParserCanGetArgumentFromDoubleDash()
        {
            var runCommand = RunCommand.FromArgs(new[]{ "--", "foo" });
            runCommand.Args.Single().Should().Be("foo");
        }
    }
}
