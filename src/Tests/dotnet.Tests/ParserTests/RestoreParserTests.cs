// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.DotNet.Cli;
using Xunit;
using Xunit.Abstractions;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.CommandLineParserTests
{
    public class RestoreCommandLineParserTests
    {
        private readonly ITestOutputHelper output;

        public RestoreCommandLineParserTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact] 
        public void RestoreCapturesArgumentsToForwardToMSBuildWhenTargetIsSpecified()
        {
            var result = Parser.Instance.Parse(@"dotnet restore .\some.csproj --packages c:\.nuget\packages /p:SkipInvalidConfigurations=true");

            result.GetValueForArgument<IEnumerable<string>>(RestoreCommandParser.SlnOrProjectArgument).Should().BeEquivalentTo(@".\some.csproj");
            result.OptionValuesToBeForwarded(RestoreCommandParser.GetCommand()).Should().Contain(@"--property:SkipInvalidConfigurations=true");
        }

        [Fact]
        public void RestoreCapturesArgumentsToForwardToMSBuildWhenTargetIsNotSpecified()
        {
            var result = Parser.Instance.Parse(@"dotnet restore --packages c:\.nuget\packages /p:SkipInvalidConfigurations=true");

            result.OptionValuesToBeForwarded(RestoreCommandParser.GetCommand()).Should().Contain(@"--property:SkipInvalidConfigurations=true");
        }

        [Fact]
        public void RestoreDistinguishesRepeatSourceArgsFromCommandArgs()
        {
            var restore =
                Parser.Instance
                      .Parse(
                          @"dotnet restore --no-cache --packages ""D:\OSS\corefx\packages"" --source https://dotnet.myget.org/F/dotnet-buildtools/api/v3/index.json --source https://dotnet.myget.org/F/dotnet-core/api/v3/index.json --source https://api.nuget.org/v3/index.json D:\OSS\corefx\external\runtime\runtime.depproj");

            restore.GetValueForArgument(RestoreCommandParser.SlnOrProjectArgument);

            restore.GetValueForOption(RestoreCommandParser.SourceOption)
                .Should()
                .BeEquivalentTo(
                    "https://dotnet.myget.org/F/dotnet-buildtools/api/v3/index.json",
                    "https://dotnet.myget.org/F/dotnet-core/api/v3/index.json",
                    "https://api.nuget.org/v3/index.json");
        }
    }
}
