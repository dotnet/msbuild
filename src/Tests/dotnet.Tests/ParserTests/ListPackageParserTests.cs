// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using FluentAssertions;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;
using Xunit;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.ParserTests
{
    public class ListPackageParserTests
    {
        [Fact]
        public void ListPackageCanForwardInteractiveFlag()
        {
            var command = Parser.Instance;
            var result = command.Parse("dotnet list package --interactive");

            var appliedOptions = result["dotnet"]["list"]["package"];

            appliedOptions.OptionValuesToBeForwarded().Should().ContainSingle("--interactive");
            result.Errors.Should().BeEmpty();
        }

        [Theory]
        [InlineData("--verbosity", "foo")]
        [InlineData("--verbosity", "")]
        [InlineData("-v", "foo")]
        [InlineData("-v", "")]
        public void ListPackageRejectsInvalidVerbosityFlags(string inputOption, string value)
        {
            var command = Parser.Instance;

            var result = command.Parse($"dotnet list package {inputOption} {value}");

            result.Errors.Should().Contain(e => e.Option != null && e.Option.Name == "verbosity");
        }

        [Theory]
        [InlineData("--verbosity", "q")]
        [InlineData("--verbosity", "quiet")]
        [InlineData("--verbosity", "m")]
        [InlineData("--verbosity", "minimal")]
        [InlineData("--verbosity", "n")]
        [InlineData("--verbosity", "normal")]
        [InlineData("--verbosity", "d")]
        [InlineData("--verbosity", "detailed")]
        [InlineData("--verbosity", "diag")]
        [InlineData("--verbosity", "diagnostic")]
        [InlineData("--verbosity", "QUIET")]
        [InlineData("-v", "q")]
        [InlineData("-v", "QUIET")]
        public void ListPackageCanForwardVerbosityFlag(string inputOption, string value)
        {
            var command = Parser.Instance;
            var result = command.Parse($"dotnet list package {inputOption} {value}");

            var appliedOptions = result["dotnet"]["list"]["package"];

            appliedOptions
                .OptionValuesToBeForwarded()
                .Should()
                .Contain($"--verbosity:{value}");
            result.Errors.Should().BeEmpty();
        }

        [Fact]
        public void ListPackageDoesNotForwardVerbosityByDefault()
        {
            var command = Parser.Instance;
            var result = command.Parse($"dotnet list package");

            var appliedOptions = result["dotnet"]["list"]["package"];

            appliedOptions
                .OptionValuesToBeForwarded()
                .Should()
                .NotContain(i => i.Contains("--verbosity", StringComparison.OrdinalIgnoreCase))
                .And.NotContain(i => i.Contains("-v", StringComparison.OrdinalIgnoreCase));
            result.Errors.Should().BeEmpty();
        }
    }
}
