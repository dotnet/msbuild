// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Xunit;
using Microsoft.DotNet.Tools.Run;
using Microsoft.NET.TestFramework;

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    [Collection(TestConstants.UsesStaticTelemetryState)]
    public class GivenDotnetRunInvocation : IClassFixture<NullCurrentSessionIdFixture>
    {
        private static readonly string WorkingDirectory =
            TestPathUtilities.FormatAbsolutePath(nameof(GivenDotnetRunInvocation));

        [Theory]
        [InlineData(new string[] { "-p:prop1=true" }, new string[] { "-p:prop1=true" })]
        [InlineData(new string[] { "--property:prop1=true" }, new string[] { "-p:prop1=true" })]
        [InlineData(new string[] { "--property","prop1=true" }, new string[] { "-p:prop1=true" })]
        [InlineData(new string[] { "-p","prop1=true" }, new string[] { "-p:prop1=true" })]
        [InlineData(new string[] { "-p", "prop1=true", "-p", "prop2=false" }, new string[] { "-p:prop1=true", "-p:prop2=false" })]
        [InlineData(new string[] { "-p:prop1=true;prop2=false" }, new string[] { "-p:prop1=true;prop2=false" })]
        [InlineData(new string[] { "-p", "MyProject.csproj", "-p:prop1=true" }, new string[] { "-p:prop1=true" })]
        [InlineData(new string[] { "--property", "MyProject.csproj", "-p:prop1=true" }, // The longhand --property option should never be treated as a project
            new string[] { "-p:MyProject.csproj", "-p:prop1=true" })]
        public void MsbuildInvocationIsCorrect(string[] args, string[] expectedArgs)
        {
            CommandDirectoryContext.PerformActionWithBasePath(WorkingDirectory, () =>
            {
                var command = RunCommand.FromArgs(args);
                command.RestoreArgs
                    .Should()
                    .BeEquivalentTo(expectedArgs);
            });
        }
    }
}
