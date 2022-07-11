// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using FluentAssertions;
using Microsoft.DotNet.Cli;
using Xunit;
using System.CommandLine;
using System.CommandLine.Parsing;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.ParserTests
{
    public class ListPackageParserTests
    {
        [Fact]
        public void ListPackageCanForwardInteractiveFlag()
        {
            var result = Parser.Instance.Parse("dotnet list package --interactive");

            result.OptionValuesToBeForwarded(ListPackageReferencesCommandParser.GetCommand()).Should().ContainSingle("--interactive");
            result.Errors.Should().BeEmpty();
        }

        [Theory]
        [InlineData("--verbosity", "foo")]
        [InlineData("--verbosity", "")]
        [InlineData("-v", "foo")]
        [InlineData("-v", "")]
        public void ListPackageRejectsInvalidVerbosityFlags(string inputOption, string value)
        {
            var result = Parser.Instance.Parse($"dotnet list package {inputOption} {value}");

            result.Errors.Should().NotBeEmpty();
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
            var result = Parser.Instance.Parse($"dotnet list package {inputOption} {value}");

            result
                .OptionValuesToBeForwarded(ListPackageReferencesCommandParser.GetCommand())
                .Should()
                .Contain($"--verbosity:{value.ToLowerInvariant()}");
            result.Errors.Should().BeEmpty();
        }

        [Fact]
        public void ListPackageDoesNotForwardVerbosityByDefault()
        {
            var result = Parser.Instance.Parse($"dotnet list package");

            result
                .OptionValuesToBeForwarded(ListPackageReferencesCommandParser.GetCommand())
                .Should()
                .NotContain(i => i.Contains("--verbosity", StringComparison.OrdinalIgnoreCase))
                .And.NotContain(i => i.Contains("-v", StringComparison.OrdinalIgnoreCase));
            result.Errors.Should().BeEmpty();
        }
    }
}
