// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.ParserTests
{
    public class BuildServerShutdownParserTests
    {
        private readonly ITestOutputHelper output;

        public BuildServerShutdownParserTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void GivenNoOptionsAllFlagsAreFalse()
        {
            var result = Parser.Instance.Parse("dotnet build-server shutdown");

            result.GetValue<bool>(ServerShutdownCommandParser.MSBuildOption).Should().Be(false);
            result.GetValue<bool>(ServerShutdownCommandParser.VbcsOption).Should().Be(false);
            result.GetValue<bool>(ServerShutdownCommandParser.RazorOption).Should().Be(false);
        }

        [Fact]
        public void GivenMSBuildOptionIsItTrue()
        {
            var result = Parser.Instance.Parse("dotnet build-server shutdown --msbuild");

            result.GetValue<bool>(ServerShutdownCommandParser.MSBuildOption).Should().Be(true);
            result.GetValue<bool>(ServerShutdownCommandParser.VbcsOption).Should().Be(false);
            result.GetValue<bool>(ServerShutdownCommandParser.RazorOption).Should().Be(false);
        }

        [Fact]
        public void GivenVBCSCompilerOptionIsItTrue()
        {
            var result = Parser.Instance.Parse("dotnet build-server shutdown --vbcscompiler");

            result.GetValue<bool>(ServerShutdownCommandParser.MSBuildOption).Should().Be(false);
            result.GetValue<bool>(ServerShutdownCommandParser.VbcsOption).Should().Be(true);
            result.GetValue<bool>(ServerShutdownCommandParser.RazorOption).Should().Be(false);
        }

        [Fact]
        public void GivenRazorOptionIsItTrue()
        {
            var result = Parser.Instance.Parse("dotnet build-server shutdown --razor");

            result.GetValue<bool>(ServerShutdownCommandParser.MSBuildOption).Should().Be(false);
            result.GetValue<bool>(ServerShutdownCommandParser.VbcsOption).Should().Be(false);
            result.GetValue<bool>(ServerShutdownCommandParser.RazorOption).Should().Be(true);
        }

        [Fact]
        public void GivenMultipleOptionsThoseAreTrue()
        {
            var result = Parser.Instance.Parse("dotnet build-server shutdown --razor --msbuild");

            result.GetValue<bool>(ServerShutdownCommandParser.MSBuildOption).Should().Be(true);
            result.GetValue<bool>(ServerShutdownCommandParser.VbcsOption).Should().Be(false);
            result.GetValue<bool>(ServerShutdownCommandParser.RazorOption).Should().Be(true);
        }
    }
}
