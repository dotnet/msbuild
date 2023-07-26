// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    [UsesVerify]
    public class WebProjectsTests : BaseIntegrationTest, IClassFixture<WebProjectsFixture>
    {
        private readonly WebProjectsFixture _fixture;
        private readonly ITestOutputHelper _log;

        public WebProjectsTests(WebProjectsFixture fixture, ITestOutputHelper log) : base(log)
        {
            _fixture = fixture;
            _log = log;
        }

        [Theory]
        [InlineData("emptyweb_cs-latest", "web")]
        [InlineData("mvc_cs-latest", "mvc")]
        [InlineData("mvc_fs-latest", "mvc", "-lang", "F#")]
        [InlineData("api_cs-latest", "webapi")]
        [InlineData("emptyweb_cs-60", "web", "-f", "net6.0")]
        [InlineData("mvc_cs-60", "mvc", "-f", "net6.0")]
        [InlineData("mvc_fs-60", "mvc", "-lang", "F#", "-f", "net6.0")]
        [InlineData("api_cs-60", "webapi", "-f", "net6.0")]
        [InlineData("emptyweb_cs-70", "web", "-f", "net7.0")]
        [InlineData("mvc_cs-70", "mvc", "-f", "net7.0")]
        [InlineData("mvc_fs-70", "mvc", "-lang", "F#", "-f", "net7.0")]
        [InlineData("api_cs-70", "webapi", "-f", "net7.0")]
        public void AllWebProjectsRestoreAndBuild(string testName, params string[] args)
        {
            string workingDir = Path.Combine(_fixture.BaseWorkingDirectory, testName);
            Directory.CreateDirectory(workingDir);

            new DotnetNewCommand(_log, args)
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDir)
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr();

            new DotnetRestoreCommand(_log)
                .WithWorkingDirectory(workingDir)
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr();

            new DotnetBuildCommand(_log)
                .WithWorkingDirectory(workingDir)
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr();

            Directory.Delete(workingDir, true);
        }

        [Fact]
        public Task CanShowHelp_WebAPI()
        {
            CommandResult commandResult = new DotnetNewCommand(_log, "webapi", "-h")
               .WithCustomHive(_fixture.HomeDirectory)
               .WithWorkingDirectory(_fixture.BaseWorkingDirectory)
               .Execute();

            commandResult
               .Should()
               .ExitWith(0)
               .And
               .NotHaveStdErr();

            return Verify(commandResult.StdOut);
        }

        [Fact]
        public Task CanShowHelp_Mvc()
        {
            CommandResult commandResult = new DotnetNewCommand(_log, "mvc", "-h")
               .WithCustomHive(_fixture.HomeDirectory)
               .WithWorkingDirectory(_fixture.BaseWorkingDirectory)
               .Execute();

            commandResult
               .Should()
               .ExitWith(0)
               .And
               .NotHaveStdErr();

            return Verify(commandResult.StdOut)
                .AddScrubber(output => output.ScrubByRegex("[A-Za-z0-9\\.]+-third-party-notices", "%version%-third-party-notices"));
        }

        [Theory]
        [InlineData("webapp")]
        [InlineData("razor")]
        public Task CanShowHelp_Webapp(string templateName)
        {
            CommandResult commandResult = new DotnetNewCommand(_log, templateName, "-h")
               .WithCustomHive(_fixture.HomeDirectory)
               .WithWorkingDirectory(_fixture.BaseWorkingDirectory)
               .Execute();

            commandResult
               .Should()
               .ExitWith(0)
               .And
               .NotHaveStdErr();

            return Verify(commandResult.StdOut)
                .UseTextForParameters("common")
                .DisableRequireUniquePrefix()
                .AddScrubber(output => output.ScrubByRegex("[A-Za-z0-9\\.]+-third-party-notices", "%version%-third-party-notices"));
        }
    }

    public sealed class WebProjectsFixture : SharedHomeDirectory
    {
        public WebProjectsFixture(IMessageSink messageSink) : base(messageSink)
        {
            BaseWorkingDirectory = Utilities.CreateTemporaryFolder(nameof(WebProjectsTests));

            InstallPackage(TemplatePackagesPaths.MicrosoftDotNetWebProjectTemplates60Path, BaseWorkingDirectory);
            InstallPackage(TemplatePackagesPaths.MicrosoftDotNetWebProjectTemplates70Path, BaseWorkingDirectory);
        }

        internal string BaseWorkingDirectory { get; private set; }
    }
}
