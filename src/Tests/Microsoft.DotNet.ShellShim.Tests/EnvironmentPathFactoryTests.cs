// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.NET.TestFramework;
using Moq;

namespace Microsoft.DotNet.ShellShim.Tests
{
    public class EnvironmentPathFactoryTests
    {
        [MacOsOnlyFact]
        public void GivenFollowingEnvironmentVariableValueItCanReturnOsxZshEnvironmentPathInstruction()
        {
            Mock<IEnvironmentProvider> provider = new Mock<IEnvironmentProvider>(MockBehavior.Strict);

            provider
                .Setup(p => p.GetEnvironmentVariable("SHELL"))
                .Returns("/bin/zsh");

            IEnvironmentPathInstruction result =
                EnvironmentPathFactory.CreateEnvironmentPathInstruction(provider.Object);
            (result is OsxZshEnvironmentPathInstruction).Should().BeTrue();
        }

        [MacOsOnlyFact]
        public void GivenFollowingEnvironmentVariableValueItShouldReturnOsxBashEnvironmentPath()
        {
            Mock<IEnvironmentProvider> provider = new Mock<IEnvironmentProvider>(MockBehavior.Strict);

            provider
                .Setup(p => p.GetEnvironmentVariable("SHELL"))
                .Returns("/bin/bash");

            IEnvironmentPathInstruction result =
                EnvironmentPathFactory.CreateEnvironmentPathInstruction(provider.Object);
            (result is OsxBashEnvironmentPath).Should().BeTrue();
        }

        [WindowsOnlyFact]
        public void GivenWindowsItShouldReturnOsxBashEnvironmentPath()
        {
            Mock<IEnvironmentProvider> provider = new Mock<IEnvironmentProvider>(MockBehavior.Loose);

            IEnvironmentPathInstruction result =
                EnvironmentPathFactory.CreateEnvironmentPathInstruction(provider.Object);
            (result is WindowsEnvironmentPath).Should().BeTrue();
        }

        [LinuxOnlyFact]
        public void GivenLinuxItShouldReturnOsxBashEnvironmentPath()
        {
            Mock<IEnvironmentProvider> provider = new Mock<IEnvironmentProvider>(MockBehavior.Loose);

            IEnvironmentPathInstruction result =
                EnvironmentPathFactory.CreateEnvironmentPathInstruction(provider.Object);
            (result is LinuxEnvironmentPath).Should().BeTrue();
        }
    }
}
