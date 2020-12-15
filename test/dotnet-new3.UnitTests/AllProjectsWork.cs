using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.TestHelper;
using Microsoft.TemplateEngine.Utils;
using Xunit;
using Xunit.Abstractions;

namespace dotnet_new3.UnitTests
{
    public class AllProjectsWork : IClassFixture<SharedHomeDirectory>
    {
        private readonly SharedHomeDirectory sharedHome;
        private readonly ITestOutputHelper log;

        public AllProjectsWork(SharedHomeDirectory sharedHome, ITestOutputHelper log)
        {
            this.sharedHome = sharedHome;
            this.log = log;
            sharedHome.InstallPackage("Microsoft.DotNet.Web.ProjectTemplates.5.0");
            sharedHome.InstallPackage("Microsoft.DotNet.Web.ProjectTemplates.3.1");
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
            string workingDir = Helpers.CreateTemporaryFolder(testName);

            new DotnetNewCommand(log, args)
                .WithWorkingDirectory(workingDir)
                .WithEnvironmentVariable(sharedHome.HomeVariable, sharedHome.HomeDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr();

            new DotnetCommand(log, "restore")
                .WithWorkingDirectory(workingDir)
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr();

            new DotnetCommand(log, "build")
                .WithWorkingDirectory(workingDir)
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr();

            Directory.Delete(workingDir, true);
        }
    }
}
