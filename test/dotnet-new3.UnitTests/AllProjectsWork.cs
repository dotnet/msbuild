// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;
using Xunit.Abstractions;

namespace dotnet_new3.UnitTests
{
    public class AllProjectsWork : IClassFixture<AllProjectsWorkFixture>
    {
        private readonly AllProjectsWorkFixture _fixture;
        private readonly ITestOutputHelper _log;

        public AllProjectsWork(AllProjectsWorkFixture fixture, ITestOutputHelper log)
        {
            _fixture = fixture;
            _log = log;
        }

        [Theory]
        [InlineData("emptyweb_cs-50",  "web")]
        [InlineData("mvc_cs-50",  "mvc")]
        [InlineData("mvc_fs-50",  "mvc", "-lang", "F#")]
        [InlineData("api_cs-50",  "api")]
        [InlineData("emptyweb_cs-31",  "web", "-f", "netcoreapp3.1")]
        [InlineData("mvc_cs-31",  "mvc", "-f", "netcoreapp3.1")]
        [InlineData("mvc_fs-31",  "mvc", "-lang", "F#", "-f", "netcoreapp3.1")]
        [InlineData("api_cs-31",  "api", "-f", "netcoreapp3.1")]
        [InlineData("console_cs-31", "console", "-f", "netcoreapp3.1")]
        [InlineData("library_cs-50", "classlib", "-f", "net5.0")]
        public void AllWebProjectsRestoreAndBuild(string testName, params string[] args)
        {
            string workingDir = Path.Combine(_fixture.BaseWorkingDirectory, testName);
            Directory.CreateDirectory(workingDir);

            new DotnetNewCommand(_log, args)
                .WithWorkingDirectory(workingDir)
                .WithEnvironmentVariable(_fixture.HomeVariable, _fixture.HomeDirectory)
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
    }

    public sealed class AllProjectsWorkFixture : SharedHomeDirectory
    {
        public AllProjectsWorkFixture(IMessageSink messageSink) : base(messageSink)
        {
            BaseWorkingDirectory = TestUtils.CreateTemporaryFolder(nameof(AllProjectsWork));
            // create nuget.config file with nuget.org listed
            new DotnetNewCommand(Log, "nugetconfig")
                .WithWorkingDirectory(BaseWorkingDirectory)
                .WithEnvironmentVariable(HomeVariable, HomeDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr();

            InstallPackage("Microsoft.DotNet.Web.ProjectTemplates.5.0", BaseWorkingDirectory, "https://api.nuget.org/v3/index.json");
            InstallPackage("Microsoft.DotNet.Web.ProjectTemplates.3.1", BaseWorkingDirectory, "https://api.nuget.org/v3/index.json");
        }

        internal string BaseWorkingDirectory { get; private set; }
    }

}
