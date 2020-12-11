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
    public class AllProjectsWork
    {
        private readonly ITestOutputHelper log;

        public AllProjectsWork(ITestOutputHelper log)
        {
            this.log = log;
        }


        private static readonly Guid _tempDirParent = Guid.NewGuid();

        private static string GetWorkingDirectoryName(string suffix, [CallerMemberName] string callerName = "")
        {
            return Path.Combine(Path.GetTempPath(), _tempDirParent.ToString(), "WorkingDirectories", callerName, suffix);
        }
        private static string GetUserHomeName(string suffix, [CallerMemberName] string callerName = "")
        {
            return Path.Combine(Path.GetTempPath(), _tempDirParent.ToString(), "Homes", callerName, suffix);
        }

        [Theory]
        [InlineData("emptyweb_cs-50", "Microsoft.DotNet.Web.ProjectTemplates.5.0", "web")]
        [InlineData("mvc_cs-50", "Microsoft.DotNet.Web.ProjectTemplates.5.0", "mvc")]
        [InlineData("mvc_fs-50", "Microsoft.DotNet.Web.ProjectTemplates.5.0", "mvc", "-lang", "F#")]
        [InlineData("api_cs-50", "Microsoft.DotNet.Web.ProjectTemplates.5.0", "api")]
        [InlineData("emptyweb_cs-31", "Microsoft.DotNet.Web.ProjectTemplates.3.1", "web", "-f", "netcoreapp3.1")]
        [InlineData("mvc_cs-31", "Microsoft.DotNet.Web.ProjectTemplates.3.1", "mvc", "-f", "netcoreapp3.1")]
        [InlineData("mvc_fs-31", "Microsoft.DotNet.Web.ProjectTemplates.3.1", "mvc", "-lang", "F#", "-f", "netcoreapp3.1")]
        [InlineData("api_cs-31", "Microsoft.DotNet.Web.ProjectTemplates.3.1", "api", "-f", "netcoreapp3.1")]
        [InlineData("console_cs-31", null, "console", "-f", "netcoreapp3.1")]
        [InlineData("library_cs-50", null, "library", "-f", "net5.0")]
        public void AllWebProjectsRestoreAndBuild(string testName, string installNuget, params string[] args)
        {
            var homeVariable = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "USERPROFILE" : "HOME";
            var homeDir = GetUserHomeName(testName);

            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DN3")))
            {
                var path = typeof(AllProjectsWork).Assembly.Location;
                while (path != null && !File.Exists(Path.Combine(path, "Microsoft.TemplateEngine.sln")))
                {
                    path = Path.GetDirectoryName(path);
                }
                if (path == null)
                    throw new Exception("Couldn't find repository root, because \"Microsoft.TemplateEngine.sln\" is not in any of parent directories.");
                // DummyFolder Path just represents folder next to "artifacts" and "template_feed", so paths inside
                // defaultinstall.package.list correctly finds packages in "../artifacts/"
                // via %DN3%\..\template_feed\ or %DN3%\..\artifacts\packages\Microsoft.TemplateEngine.Core.*
                path = Path.Combine(path, "DummyFolder");
                Environment.SetEnvironmentVariable("DN3", path);
            }

            string workingDir = GetWorkingDirectoryName(testName);
            Directory.CreateDirectory(workingDir);

            if (!string.IsNullOrEmpty(installNuget))
                new DotnetNewCommand(log, "-i", installNuget)
                    .WithWorkingDirectory(workingDir)
                    .WithEnvironmentVariable(homeVariable, homeDir)
                    .Execute()
                    .Should()
                    .ExitWith(0)
                    .And
                    .NotHaveStdErr();

            new DotnetNewCommand(log, args)
                .WithWorkingDirectory(workingDir)
                .WithEnvironmentVariable(homeVariable, homeDir)
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
            Directory.Delete(homeDir, true);
        }
    }
}
