// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Threading.Tasks;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.TemplateEngine.TestHelper;
using VerifyTests;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.New.Tests
{
    [UsesVerify]
    [Collection("Verify Tests")]
    public class AllWebProjectsWork : SdkTest, IClassFixture<WebProjectsFixture>
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
            BaseWorkingDirectory = TestUtils.CreateTemporaryFolder(nameof(AllWebProjectsWork));
 
            InstallPackage(TemplatePackagesPaths.MicrosoftDotNetWebProjectTemplates31Path, BaseWorkingDirectory);
            InstallPackage(TemplatePackagesPaths.MicrosoftDotNetWebProjectTemplates50Path, BaseWorkingDirectory);
        }

        internal string BaseWorkingDirectory { get; private set; }
    }
}
