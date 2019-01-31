// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using FluentAssertions;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Tools.Common;
using Microsoft.VisualBasic.CompilerServices;
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
        public void AddReferenceHasInteractiveFlag()
        {
            var command = Parser.Instance;

            var result = command.Parse("dotnet add reference my.csproj --interactive");

            AppliedOption appliedOption = result["dotnet"]["add"]["reference"];
            CommonOptionResult.GetInteractive(appliedOption).Should().BeTrue();
        }

        [Fact]
        public void AddReferenceDoesNotHaveInteractiveFlagByDefault()
        {
            var command = Parser.Instance;

            var result = command.Parse("dotnet add reference my.csproj");

            AppliedOption appliedOption = result["dotnet"]["add"]["reference"];
            CommonOptionResult.GetInteractive(appliedOption).Should().BeFalse();
        }

        private static bool GetInteractive(AppliedOption appliedOption)
        {
            return appliedOption["interactive"].Value<bool>();
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
