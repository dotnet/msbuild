// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    [UsesVerify]
    [Collection("Verify Tests")]
    public class AllWebProjectsWork : BaseIntegrationTest, IClassFixture<WebProjectsFixture>
    {
        private readonly WebProjectsFixture _fixture;
        private readonly ITestOutputHelper _log;

        public AllWebProjectsWork(WebProjectsFixture fixture, ITestOutputHelper log) : base(log)
        {
            _fixture = fixture;
            _log = log;
        }

        [Theory]
        [InlineData("emptyweb_cs-50", "web")]
        [InlineData("mvc_cs-50", "mvc")]
        [InlineData("mvc_fs-50", "mvc", "-lang", "F#")]
        [InlineData("api_cs-50", "webapi")]
        [InlineData("emptyweb_cs-31", "web", "-f", "netcoreapp3.1")]
        [InlineData("mvc_cs-31", "mvc", "-f", "netcoreapp3.1")]
        [InlineData("mvc_fs-31", "mvc", "-lang", "F#", "-f", "netcoreapp3.1")]
        [InlineData("api_cs-31", "webapi", "-f", "netcoreapp3.1")]
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
            var commandResult = new DotnetNewCommand(_log, "webapi", "-h")
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
            var commandResult = new DotnetNewCommand(_log, "mvc", "-h")
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
            var commandResult = new DotnetNewCommand(_log, templateName, "-h")
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
            BaseWorkingDirectory = Utilities.CreateTemporaryFolder(nameof(AllWebProjectsWork));
            InstallPackage(TemplatePackagesPaths.MicrosoftDotNetWebProjectTemplates31Path, BaseWorkingDirectory);
            InstallPackage(TemplatePackagesPaths.MicrosoftDotNetWebProjectTemplates50Path, BaseWorkingDirectory);
        }

        internal string BaseWorkingDirectory { get; private set; }
    }
}
