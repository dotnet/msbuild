// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Tools.NuGet;
using Moq;

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
        [InlineData(new[] { "verify", "foo.1.0.0.nupkg" }, 0)]
        [InlineData(new[] { "verify", "foo.1.0.0.nupkg", "--all" }, 0)]
        [InlineData(new[] { "verify", "foo.1.0.0.nupkg",
                            "--certificate-fingerprint", "CE40881FF5F0AD3E58965DA20A9F57",
                            "--certificate-fingerprint", "1EF1651A56933748E1BF1C99E537C4E039" }, 0)]
        [InlineData(new[] { "trust", "-v d" }, 0)]
        [InlineData(new[] { "trust", "certificate MyCompanyCert  CE40881FF5F0AD3E58965DA20A9F571EF1651A56933748E1BF1C99E537C4E039 --algorithm SHA256" }, 0)]
        [InlineData(new[] { "trust", "source NuGet --configfile ..\nuget.config" }, 0)]
        [InlineData(new[] { "trust", "remove Nuget" }, 0)]
        [InlineData(new[] { "sign", "foo.1.0.0.nupkg",
                            "--certificate-path", "certficate.pfx",
                            "--certificate-password", "PlaceholderPassword"}, 0)]
        [InlineData(new[] { "sign", "foo.1.0.0.nupkg",
                            "--certificate-path", "certficate.pfx",
                            "--certificate-password", "PlaceholderPassword",
                            "--overwrite" }, 0)]
        [InlineData(new[] { "sign", "foo.1.0.0.nupkg",
                            "--certificate-fingerprint", "CE40881FF5F0AD3E58965DA20A9F57",
                            "--certificate-password", "PlaceholderPassword"}, 0)]
        [InlineData(new[] { "sign", "foo.1.0.0.nupkg",
                            "--certificate-store-name", "My",
                            "--certificate-store-location", "CurrentUser",
                            "--certificate-fingerprint", "CE40881FF5F0AD3E58965DA20A9F57",
                            "--certificate-password", "PlaceholderPassword"}, 0)]
        [InlineData(new[] { "sign", "foo.1.0.0.nupkg",
                            "--certificate-store-name", "My",
                            "--certificate-store-location", "CurrentUser",
                            "--certificate-subject-name", "CE40881FF5F0AD3E58965DA20A9F57",
                            "--certificate-password", "PlaceholderPassword"}, 0)]
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

        [Fact]
        public void ItAcceptsPrefixedOption()
        {
            var rootPath = _testAssetsManager.CreateTestDirectory().Path;

            new DotnetCommand(Log, "nuget")
                .WithWorkingDirectory(rootPath)
                .Execute($"push", "-ss")
                .Should()
                .Fail()
                .And
                .HaveStdErrContaining("Required argument missing for option: '-ss'.");
        }
    }
}
