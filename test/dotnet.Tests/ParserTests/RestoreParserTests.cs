// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using FluentAssertions;
using Microsoft.DotNet.Cli.CommandLine;
using Xunit;
using Xunit.Abstractions;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.ParserTests
{
    public class RestoreParserTests
    {
        private readonly ITestOutputHelper output;

        public RestoreParserTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void RestoreCapturesArgumentsToForwardToMSBuildWhenTargetIsSpecified()
        {
            var parser = Parser.Instance["dotnet"];

            var result = parser.Parse(@"restore .\some.csproj --packages c:\.nuget\packages /p:SkipInvalidConfigurations=true");

            output.WriteLine(result.Diagram());

            result["restore"]
                .Arguments
                .Should()
                .BeEquivalentTo(@".\some.csproj", @"/p:SkipInvalidConfigurations=true");
        }

        [Fact]
        public void RestoreCapturesArgumentsToForwardToMSBuildWhenTargetIsNotSpecified()
        {
            var parser = Parser.Instance["dotnet"];

            var result = parser.Parse(@"restore --packages c:\.nuget\packages /p:SkipInvalidConfigurations=true");

            output.WriteLine(result.Diagram());

            result["restore"]
                .Arguments
                .Should()
                .BeEquivalentTo(@"/p:SkipInvalidConfigurations=true");
        }
    }
}