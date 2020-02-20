// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using Xunit.Abstractions;

namespace dotnet.Tests
{
    public class GivenThatTheUserRequestsHelp : SdkTest
    {
        public GivenThatTheUserRequestsHelp(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData("-h")]
        [InlineData("add -h")]
        [InlineData("add package -h")]
        [InlineData("add reference -h")]
        [InlineData("build -h")]
        [InlineData("clean -h")]
        [InlineData("list -h")]
        [InlineData("msbuild -h")]
        //  "new -h" test disabled until fix from https://github.com/dotnet/cli/pull/12899 flows to new repo
        //[InlineData("new -h")]
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
            var result = new DotnetCommand(Log)
                .Execute(commandLine.Split());

            result.ExitCode.Should().Be(0);
        }
    }
}
