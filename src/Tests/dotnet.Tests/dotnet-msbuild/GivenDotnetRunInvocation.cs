// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools.Run;

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
        // The longhand --property option should never be treated as a project
        [InlineData(new string[] { "--property", "MyProject.csproj", "-p:prop1=true" }, new string[] { "-p:MyProject.csproj", "-p:prop1=true" })]
        [InlineData(new string[] { "--disable-build-servers" }, new string[] { "-p:UseRazorBuildServer=false", "-p:UseSharedCompilation=false", "/nodeReuse:false" })]
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
