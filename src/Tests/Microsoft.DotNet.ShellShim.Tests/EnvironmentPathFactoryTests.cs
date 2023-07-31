// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
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
