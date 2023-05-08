// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Build;
using Microsoft.DotNet.Tools.Publish;
using System.CommandLine;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Tests.CommandLineParserTests
{
    public class MSBuildArgumentCommandLineParserTests
    {
        private readonly ITestOutputHelper output;

        public MSBuildArgumentCommandLineParserTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Theory]
        [InlineData(new string[] { "-property:prop1=true", "-p:prop2=false" }, true)]
        [InlineData(new string[] { "-property:prop1=true", "-p:prop2=false" }, false)]
        [InlineData(new string[] { "-p:teamcity_buildConfName=\"Build, Test and Publish\"" }, false)]
        [InlineData(new string[] { "-p:teamcity_buildConfName=\"Build, Test and Publish\"" }, true)]
        [InlineData(new string[] { "-detailedSummary" }, true)]
        [InlineData(new string[] { "-clp:NoSummary" }, true)]
        [InlineData(new string[] { "-orc" }, true)]
        [InlineData(new string[] { "-orc" }, false)]
        public void MSBuildArgumentsAreForwardedCorrectly(string[] arguments, bool buildCommand)
        {
            RestoringCommand command = buildCommand ?
                BuildCommand.FromArgs(arguments) :
                PublishCommand.FromArgs(arguments);
            var expectedArguments = arguments.Select(a => a.Replace("-property:", "--property:").Replace("-p:", "--property:"));
            var argString = command.MSBuildArguments;

            foreach (var expectedArg in expectedArguments)
            {
                argString.Should().Contain(expectedArg);
            }
        }

        [Theory]
        [InlineData(new string[] { "-p:teamcity_buildConfName=\"Build, Test and Publish\"" }, new string[] { "--property:teamcity_buildConfName=\"Build, Test and Publish\"" })]
        [InlineData(new string[] { "-p:prop1=true", "-p:prop2=false" }, new string[] { "--property:prop1=true", "--property:prop2=false" })]
        [InlineData(new string[] { "-p:prop1=\".;/opt/usr\"" }, new string[] { "--property:prop1=\".%3B/opt/usr\"" })]
        public void Can_pass_msbuild_properties_safely(string[] tokens, string[] forwardedTokens) {
            var forwardingFunction = (CommonOptions.PropertiesOption as ForwardedOption<string[]>).GetForwardingFunction();
            var result = CommonOptions.PropertiesOption.Parse(tokens);
            var parsedTokens = forwardingFunction(result);
            parsedTokens.Should().BeEquivalentTo(forwardedTokens);
        }
    }
}
