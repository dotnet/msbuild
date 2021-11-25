// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ApprovalTests;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;
using Xunit.Abstractions;

namespace Dotnet_new3.IntegrationTests
{
    public class AllWebProjectsWork : IClassFixture<WebProjectsFixture>
    {
        private readonly WebProjectsFixture _fixture;
        private readonly ITestOutputHelper _log;

        public AllWebProjectsWork(WebProjectsFixture fixture, ITestOutputHelper log)
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

            new DotnetCommand(_log, "restore")
                .WithWorkingDirectory(workingDir)
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr();

            new DotnetCommand(_log, "build")
                .WithWorkingDirectory(workingDir)
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr();

            Directory.Delete(workingDir, true);
        }

        [Fact]
        public void CanShowHelp_WebAPI()
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

            Approvals.Verify(commandResult.StdOut);
        }

        [Fact]
        public void CanShowHelp_Mvc()
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

            Approvals.Verify(commandResult.StdOut);
        }

        [Theory]
        [InlineData("webapp")]
        [InlineData("razor")]
        public void CanShowHelp_Webapp(string templateName)
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

            Approvals.Verify(commandResult.StdOut);
        }
    }

    public sealed class WebProjectsFixture : SharedHomeDirectory
    {
        public WebProjectsFixture(IMessageSink messageSink) : base(messageSink)
        {
            BaseWorkingDirectory = TestUtils.CreateTemporaryFolder(nameof(AllWebProjectsWork));
            // create nuget.config file with nuget.org listed
            new DotnetNewCommand(Log, "nugetconfig")
                .WithCustomHive(HomeDirectory)
                .WithWorkingDirectory(BaseWorkingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr();

            InstallPackage(TemplatePackagesPaths.MicrosoftDotNetWebProjectTemplates21Path, BaseWorkingDirectory);
            InstallPackage(TemplatePackagesPaths.MicrosoftDotNetWebProjectTemplates31Path, BaseWorkingDirectory);
            InstallPackage(TemplatePackagesPaths.MicrosoftDotNetWebProjectTemplates50Path, BaseWorkingDirectory);
        }

        internal string BaseWorkingDirectory { get; private set; }
    } 
}
