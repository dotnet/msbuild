// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Moq;

namespace Microsoft.DotNet.ShellShim.Tests
{
    public class ZshDetectorTests
    {
        [Theory]
        [InlineData("/bin/zsh")]
        [InlineData("/other-place/zsh")]
        public void GivenFollowingEnvironmentVariableValueItCanDetectZsh(string environmentVariableValue)
        {
            Mock<IEnvironmentProvider> provider = new(MockBehavior.Strict);

            provider
                .Setup(p => p.GetEnvironmentVariable("SHELL"))
                .Returns(environmentVariableValue);

            ZshDetector.IsZshTheUsersShell(provider.Object).Should().BeTrue();
        }

        [Theory]
        [InlineData("/bin/bash")]
        [InlineData("/other/value")]
        [InlineData(null)]
        public void GivenFollowingEnvironmentVariableValueItCanDetectItIsNotZsh(string environmentVariableValue)
        {
            Mock<IEnvironmentProvider> provider = new(MockBehavior.Strict);

            provider
                .Setup(p => p.GetEnvironmentVariable("SHELL"))
                .Returns(environmentVariableValue);

            ZshDetector.IsZshTheUsersShell(provider.Object).Should().BeFalse();
        }
    }
}
