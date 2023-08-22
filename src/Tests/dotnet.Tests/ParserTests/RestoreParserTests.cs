// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli;
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

            result.GetValue<IEnumerable<string>>(RestoreCommandParser.SlnOrProjectArgument).Should().BeEquivalentTo(@".\some.csproj");
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

            restore.GetValue(RestoreCommandParser.SlnOrProjectArgument);

            restore.GetValue(RestoreCommandParser.SourceOption)
                .Should()
                .BeEquivalentTo(
                    "https://dotnet.myget.org/F/dotnet-buildtools/api/v3/index.json",
                    "https://dotnet.myget.org/F/dotnet-core/api/v3/index.json",
                    "https://api.nuget.org/v3/index.json");
        }
    }
}
