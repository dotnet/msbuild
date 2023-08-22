// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.NugetSearch;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace dotnet.Tests.ToolSearchTests
{
    public class NugetSearchApiParameterTests
    {
        [Fact]
        public void ItShouldValidateSkipType()
        {
            var result = Parser.Instance.Parse("dotnet tool search mytool --skip wrongtype");
            Action a = () => new NugetSearchApiParameter(result);
            a.Should().Throw<GracefulException>();
        }

        [Fact]
        public void ItShouldValidateTakeType()
        {
            var result = Parser.Instance.Parse("dotnet tool search mytool --take wrongtype");

            Action a = () => new NugetSearchApiParameter(result);
            a.Should().Throw<GracefulException>();
        }

        [Fact]
        public void ItShouldNotThrowWhenInputIsValid()
        {
            var parseResult = Parser.Instance.Parse("dotnet tool search mytool --detail --skip 3 --take 4 --prerelease");

            var result = new NugetSearchApiParameter(parseResult);
            result.Prerelease.Should().Be(true);
            result.Skip.Should().Be(3);
            result.Take.Should().Be(4);
        }
    }
}
