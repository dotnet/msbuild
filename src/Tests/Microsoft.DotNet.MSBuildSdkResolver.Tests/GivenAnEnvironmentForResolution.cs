// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Utils.Tests
{
    public class GivenAnEnvironmentForResolution
    {
        [Fact]
        public void ItIgnoresInvalidPath()
        {
            Func<string, string> getPathEnvVarFunc = (string var) => { return $"{Directory.GetCurrentDirectory()}Dir{Path.GetInvalidPathChars().First()}Name"; };
            var environmentProvider = new NativeWrapper.EnvironmentProvider(getPathEnvVarFunc);
            var pathResult = environmentProvider.GetCommandPath("nonexistantCommand");
            pathResult.Should().BeNull();
        }

        [Fact]
        public void ItDoesNotReturnNullDotnetRootOnExtraPathSeparator()
        {
            File.Create(Path.Combine(Directory.GetCurrentDirectory(), "dotnet.exe"));
            Func<string, string> getPathEnvVarFunc = (input) => input.Equals("PATH") ? $"fake{Path.PathSeparator}" : string.Empty;
            var result = NativeWrapper.EnvironmentProvider.GetDotnetExeDirectory(getPathEnvVarFunc);
            result.Should().NotBeNullOrWhiteSpace();
        }
    }
}
