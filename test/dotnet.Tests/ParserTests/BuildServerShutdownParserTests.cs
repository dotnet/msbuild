// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using FluentAssertions;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;
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

            var options = result["dotnet"]["build-server"]["shutdown"];
            options.ValueOrDefault<bool>("msbuild").Should().Be(false);
            options.ValueOrDefault<bool>("vbcscompiler").Should().Be(false);
            options.ValueOrDefault<bool>("razor").Should().Be(false);
        }

        [Fact]
        public void GivenMSBuildOptionIsItTrue()
        {
            var result = Parser.Instance.Parse("dotnet build-server shutdown --msbuild");

            var options = result["dotnet"]["build-server"]["shutdown"];
            options.ValueOrDefault<bool>("msbuild").Should().Be(true);
            options.ValueOrDefault<bool>("vbcscompiler").Should().Be(false);
            options.ValueOrDefault<bool>("razor").Should().Be(false);
        }

        [Fact]
        public void GivenVBCSCompilerOptionIsItTrue()
        {
            var result = Parser.Instance.Parse("dotnet build-server shutdown --vbcscompiler");

            var options = result["dotnet"]["build-server"]["shutdown"];
            options.ValueOrDefault<bool>("msbuild").Should().Be(false);
            options.ValueOrDefault<bool>("vbcscompiler").Should().Be(true);
            options.ValueOrDefault<bool>("razor").Should().Be(false);
        }

        [Fact]
        public void GivenRazorOptionIsItTrue()
        {
            var result = Parser.Instance.Parse("dotnet build-server shutdown --razor");

            var options = result["dotnet"]["build-server"]["shutdown"];
            options.ValueOrDefault<bool>("msbuild").Should().Be(false);
            options.ValueOrDefault<bool>("vbcscompiler").Should().Be(false);
            options.ValueOrDefault<bool>("razor").Should().Be(true);
        }

        [Fact]
        public void GivenMultipleOptionsThoseAreTrue()
        {
            var result = Parser.Instance.Parse("dotnet build-server shutdown --razor --msbuild");

            var options = result["dotnet"]["build-server"]["shutdown"];
            options.ValueOrDefault<bool>("msbuild").Should().Be(true);
            options.ValueOrDefault<bool>("vbcscompiler").Should().Be(false);
            options.ValueOrDefault<bool>("razor").Should().Be(true);
        }
    }
}
