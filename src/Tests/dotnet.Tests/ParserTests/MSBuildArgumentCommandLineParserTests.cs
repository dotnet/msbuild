// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Build;
using Microsoft.DotNet.Tools.Publish;
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
        [InlineData(new string[] { "-detailedSummary" }, true)]
        [InlineData(new string[] { "-clp:NoSummary" }, true)]
        [InlineData(new string[] { "-orc" }, true)]
        [InlineData(new string[] { "-orc" }, false)]
        public void MSBuildArgumentsAreForwardedCorrectly(string[] arguments, bool buildCommand)
        {
            RestoringCommand command = buildCommand ? 
                BuildCommand.FromArgs(arguments) : 
                PublishCommand.FromArgs(arguments);
            command.GetArgumentsToMSBuild().Split(' ')
                .Should()
                .Contain(arguments);
        }
    }
}
