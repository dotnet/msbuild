// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Tools.Common;
using Xunit;
using Xunit.Abstractions;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.ParserTests
{
    public class AddReferenceParserTests
    {
        private readonly ITestOutputHelper output;

        public AddReferenceParserTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void AddReferenceHasDefaultArgumentSetToCurrentDirectory()
        {
            var command = Parser.Instance;

            var result = command.Parse("dotnet add reference my.csproj");

            result["dotnet"]["add"]
                .Arguments
                .Should()
                .BeEquivalentTo(
                    PathUtility.EnsureTrailingSlash(Directory.GetCurrentDirectory()));
        }

        [Fact]
        public void AddReferenceWithoutArgumentResultsInAnError()
        {
            var command = Parser.Instance;

            var result = command.Parse("dotnet add reference");

            result
                .Errors
                .Select(e => e.Message)
                .Should()
                .BeEquivalentTo(string.Format(LocalizableStrings.RequiredArgumentMissingForCommand, "reference"));
        }
    }
}