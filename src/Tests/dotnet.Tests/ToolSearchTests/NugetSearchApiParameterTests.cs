// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using FluentAssertions;
using Microsoft.DotNet.NugetSearch;
using Xunit;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Parser = Microsoft.DotNet.Cli.Parser;


namespace dotnet.Tests.ToolSearchTests
{
    public class NugetSearchApiParameterTests
    {
        [Fact]
        public void ItShouldValidateSkipType()
        {
            var result = Parser.Instance.Parse("dotnet tool search mytool --skip wrongtype");

            var appliedOptions = result["dotnet"]["tool"]["search"];
            Action a = () => new NugetSearchApiParameter(appliedOptions);
            a.ShouldThrow<GracefulException>();
        }
        
        [Fact]
        public void ItShouldValidateTakeType()
        {
            var result = Parser.Instance.Parse("dotnet tool search mytool --take wrongtype");

            var appliedOptions = result["dotnet"]["tool"]["search"];
            Action a = () => new NugetSearchApiParameter(appliedOptions);
            a.ShouldThrow<GracefulException>();
        }
        
        [Fact]
        public void ItShouldNotThrowWhenInputIsValid()
        {
            var parseResult = Parser.Instance.Parse("dotnet tool search mytool --detail --skip 3 --take 4 --prerelease");

            var appliedOptions = parseResult["dotnet"]["tool"]["search"];
            var result = new NugetSearchApiParameter(appliedOptions);
            result.Prerelease.Should().Be(true);
            result.Skip.Should().Be(3);
            result.Take.Should().Be(4);
        }
    }
}
