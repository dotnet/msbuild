using System;
using System.IO;
using FluentAssertions;
using System.Linq;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Common;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Tests
{
    public class AddReferenceParserTests
    {
        private readonly ITestOutputHelper output;

        public AddReferenceParserTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void dotnet_add_reference_has_default_argument_set_to_current_directory()
        {
            var command = ParserFor.DotnetCommand;

            var result = command.Parse("dotnet add reference my.csproj");

            output.WriteLine(result.Diagram());

            result["dotnet"]["add"]
                .Arguments
                .Should()
                .BeEquivalentTo(
                    PathUtility.EnsureTrailingSlash(Directory.GetCurrentDirectory()));
        }

        [Fact]
        public void dotnet_add_reference_without_argument_results_in_an_error()
        {
            var command = ParserFor.DotnetCommand["add"];

            var result = command.Parse("add reference");

            output.WriteLine(result.Diagram());

            result
                .Errors
                .Select(e => e.Message)
                .Should()
                .BeEquivalentTo("Required argument missing for command: reference");
        }
    }
}