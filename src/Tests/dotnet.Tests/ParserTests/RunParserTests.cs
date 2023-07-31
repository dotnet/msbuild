// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools.Run;

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
