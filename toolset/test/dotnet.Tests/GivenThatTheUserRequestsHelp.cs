// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

namespace dotnet.Tests
{
    public class GivenThatTheUserRequestsHelp
    {
        [Theory]
        [InlineData("-h")]
        [InlineData("add -h")]
        [InlineData("add package -h")]
        [InlineData("add reference -h")]
        [InlineData("build -h")]
        [InlineData("clean -h")]
        [InlineData("list -h")]
        [InlineData("msbuild -h")]
        [InlineData("new -h")]
        [InlineData("nuget -h")]
        [InlineData("pack -h")]
        [InlineData("publish -h")]
        [InlineData("remove -h")]
        [InlineData("restore -h")]
        [InlineData("run -h")]
        [InlineData("sln -h")]
        [InlineData("sln add -h")]
        [InlineData("sln list -h")]
        [InlineData("sln remove -h")]
        [InlineData("store -h")]
        [InlineData("test -h")]
        public void TheResponseIsNotAnError(string commandLine)
        {
            var result = new DotnetCommand()
                .ExecuteWithCapturedOutput(commandLine);

            result.ExitCode.Should().Be(0);
        }
    }
}
