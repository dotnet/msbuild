// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Tools;
using BuildCommand = Microsoft.DotNet.Tools.Build.BuildCommand;
using PublishCommand = Microsoft.DotNet.Tools.Publish.PublishCommand;

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
        [InlineData(new string[] { "-p:prop1=\".;/opt/usr\"" }, new string[] { "--property:prop1=\".;/opt/usr\"" })]
        [InlineData(new string[] { "-p:prop1=true;prop2=false;prop3=\"wut\";prop4=\"1;2;3\"" }, new string[] { "--property:prop1=true", "--property:prop2=false", "--property:prop3=\"wut\"", "--property:prop4=\"1;2;3\"" })]
        [InlineData(new string[] { "-p:prop4=\"1;2;3\"" }, new string[] { "--property:prop4=\"1;2;3\"" })]
        [InlineData(new string[] { "-p:prop4=\"1 ;2 ;3 \"" }, new string[] { "--property:prop4=\"1 ;2 ;3 \"" })]
        [InlineData(new string[] { "-p:RuntimeIdentifiers=linux-x64;linux-arm64" }, new string[] { "--property:RuntimeIdentifiers=linux-x64;linux-arm64" })]
        public void Can_pass_msbuild_properties_safely(string[] tokens, string[] forwardedTokens)
        {
            var forwardingFunction = (CommonOptions.PropertiesOption as ForwardedOption<string[]>).GetForwardingFunction();
            var result = new CliRootCommand() { CommonOptions.PropertiesOption }.Parse(tokens);
            var parsedTokens = forwardingFunction(result);
            parsedTokens.Should().BeEquivalentTo(forwardedTokens);
        }
    }
}
