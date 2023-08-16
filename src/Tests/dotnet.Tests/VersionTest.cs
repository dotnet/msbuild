// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Microsoft.DotNet.Cli.Utils;


namespace Microsoft.DotNet.Tests
{
    public class GivenDotnetSdk : SdkTest
    {
        public GivenDotnetSdk(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void VersionCommandDisplaysCorrectVersion()
        {
            var assemblyMetadata = typeof(GivenDotnetSdk).Assembly
                .GetCustomAttributes(typeof(AssemblyMetadataAttribute))
                .Cast<AssemblyMetadataAttribute>()
                .ToDictionary(a => a.Key, a => a.Value);

            var expectedVersion = assemblyMetadata["SdkVersion"];

            CommandResult result = new DotnetCommand(Log)
                    .Execute("--version");

            result.Should().Pass();
            result.StdOut.Trim().Should().Be(expectedVersion);
        }

        [Fact]
        public void VersionIsNotDisplayedFollowingUnrecognizedCommand()
        {
            var result = new DotnetCommand(Log)
                .Execute(new string[] { "faketool", "--version" });

            result.ExitCode.Should().Be(1);
        }
    }
}
