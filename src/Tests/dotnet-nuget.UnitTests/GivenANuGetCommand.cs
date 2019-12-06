// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using FluentAssertions;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.DotNet.Tools.NuGet;
using Moq;
using NuGet.Frameworks;
using Xunit;
using Microsoft.NET.TestFramework;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Tools.Run.Tests
{
    public class GivenANuGetCommand : SdkTest
    {
        public GivenANuGetCommand(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData(new[] { "push", "foo.1.0.0.nupkg" }, 0)]
        [InlineData(new[] { "push", "foo.1.0.0.nupkg", "-k", "12345678-1234-1234-1234-123456789012" }, 0)]
        [InlineData(new[] { "push", "foo.1.0.0.nupkg",
                            "--api-key", "12345678-1234-1234-1234-123456789012",
                            "--source", "http://www.myget.org/foofeed" }, 0)]
        [InlineData(new[] { "push", "foo.1.0.0.nupkg",
                            "--api-key", "12345678-1234-1234-1234-123456789012",
                            "--source", "http://www.nuget.org/foofeed",
                            "--symbol-api-key", "12345678-1234-1234-1234-123456789012",
                            "--symbol-source", "https://nuget.smbsrc.net/foo",
                            "--timeout", "1000",
                            "--disable-buffering",
                            "--no-symbols" }, 0)] // Unlikely option given others, but testing max options edge case
        [InlineData(new[] { "delete", "foo.1.0.0.nupkg" }, 0)]
        [InlineData(new[] { "delete", "foo.1.0.0.nupkg",
                            "--non-interactive" }, 0)]
        [InlineData(new[] { "delete", "foo.1.0.0.nupkg",
                            "--api-key", "12345678-1234-1234-1234-123456789012",
                            "--source", "http://www.nuget.org/foofeed",
                            "--non-interactive" }, 0)]
        [InlineData(new[] { "locals" }, 0)]
        [InlineData(new[] { "locals", "http-cache", "packages-cache", "global-packages", "temp" }, 0)]
        public void ItPassesCommandIfSupported(string[] inputArgs, int result)
        {
            // Arrange
            string[] receivedArgs = null;
            var testCommandRunner = new Mock<ICommandRunner>();
            testCommandRunner
                .Setup(x => x.Run(It.IsAny<string[]>()))
                .Callback<string[]>(s => receivedArgs = s)
                .Returns(0);

            // Act
            var returned = NuGetCommand.Run(inputArgs, testCommandRunner.Object);

            // Assert
            receivedArgs.Should().BeEquivalentTo(inputArgs);
            returned.Should().Be(result);
        }
    }
}
