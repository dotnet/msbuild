// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using System;
using System.IO;
using FluentAssertions;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.MSBuild.IntegrationTests
{
    public class GivenDotnetInvokesMSBuild : TestBase
    {
        [Theory]
        [InlineData("build")]
        [InlineData("clean")]
        [InlineData("msbuild")]
        [InlineData("pack")]
        [InlineData("publish")]
        [InlineData("test")]
        public void When_dotnet_command_invokes_msbuild_Then_env_vars_and_m_are_passed(string command)
        {
            var testInstance = TestAssets.Get("MSBuildIntegration")
                .CreateInstance(identifier: command)
                .WithSourceFiles();

            new DotnetCommand()
                .WithWorkingDirectory(testInstance.Root)
                .Execute(command)
                .Should().Pass();
        }
    }
}
