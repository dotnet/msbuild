// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Moq;
using Xunit;

namespace Microsoft.DotNet.ShellShim.Tests
{
    public class ZshDetectorTests
    {
        [Theory]
        [InlineData("/bin/zsh")]
        [InlineData("/other-place/zsh")]
        public void GivenFollowingEnvironmentVariableValueItCanDetectZsh(string environmentVariableValue)
        {
            Mock<IEnvironmentProvider> provider = new Mock<IEnvironmentProvider>(MockBehavior.Strict);

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
            Mock<IEnvironmentProvider> provider = new Mock<IEnvironmentProvider>(MockBehavior.Strict);

            provider
                .Setup(p => p.GetEnvironmentVariable("SHELL"))
                .Returns(environmentVariableValue);

            ZshDetector.IsZshTheUsersShell(provider.Object).Should().BeFalse();
        }
    }
}
