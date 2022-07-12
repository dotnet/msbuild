// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using System.CommandLine.Parsing;
using FluentAssertions;
using Microsoft.DotNet.Cli;
using Xunit;
using Xunit.Abstractions;
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

            result.GetValueForOption<bool>(ServerShutdownCommandParser.MSBuildOption).Should().Be(false);
            result.GetValueForOption<bool>(ServerShutdownCommandParser.VbcsOption).Should().Be(false);
            result.GetValueForOption<bool>(ServerShutdownCommandParser.RazorOption).Should().Be(false);
        }

        [Fact]
        public void GivenMSBuildOptionIsItTrue()
        {
            var result = Parser.Instance.Parse("dotnet build-server shutdown --msbuild");

            result.GetValueForOption<bool>(ServerShutdownCommandParser.MSBuildOption).Should().Be(true);
            result.GetValueForOption<bool>(ServerShutdownCommandParser.VbcsOption).Should().Be(false);
            result.GetValueForOption<bool>(ServerShutdownCommandParser.RazorOption).Should().Be(false);
        }

        [Fact]
        public void GivenVBCSCompilerOptionIsItTrue()
        {
            var result = Parser.Instance.Parse("dotnet build-server shutdown --vbcscompiler");

            result.GetValueForOption<bool>(ServerShutdownCommandParser.MSBuildOption).Should().Be(false);
            result.GetValueForOption<bool>(ServerShutdownCommandParser.VbcsOption).Should().Be(true);
            result.GetValueForOption<bool>(ServerShutdownCommandParser.RazorOption).Should().Be(false);
        }

        [Fact]
        public void GivenRazorOptionIsItTrue()
        {
            var result = Parser.Instance.Parse("dotnet build-server shutdown --razor");

            result.GetValueForOption<bool>(ServerShutdownCommandParser.MSBuildOption).Should().Be(false);
            result.GetValueForOption<bool>(ServerShutdownCommandParser.VbcsOption).Should().Be(false);
            result.GetValueForOption<bool>(ServerShutdownCommandParser.RazorOption).Should().Be(true);
        }

        [Fact]
        public void GivenMultipleOptionsThoseAreTrue()
        {
            var result = Parser.Instance.Parse("dotnet build-server shutdown --razor --msbuild");

            result.GetValueForOption<bool>(ServerShutdownCommandParser.MSBuildOption).Should().Be(true);
            result.GetValueForOption<bool>(ServerShutdownCommandParser.VbcsOption).Should().Be(false);
            result.GetValueForOption<bool>(ServerShutdownCommandParser.RazorOption).Should().Be(true);
        }
    }
}
