using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.TestHelper;
using Microsoft.TemplateEngine.Utils;
using Xunit;

namespace dotnet_new3.UnitTests
{
    public class AllProjectsWork
    {
        private static readonly Guid _tempDirParent = Guid.NewGuid();

        private static string GetWorkingDirectoryName(string suffix, [CallerMemberName] string callerName = "")
        {
            return Path.Combine(Path.GetTempPath(), _tempDirParent.ToString(), callerName, suffix);
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
            string workingDir = GetWorkingDirectoryName(testName);
            Directory.CreateDirectory(workingDir);

            Program.Main(new[] { "--debug:reinit" });

            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DN3")))
            {
                string relTo = new Uri(typeof(Program).GetTypeInfo().Assembly.CodeBase, UriKind.Absolute).LocalPath;
                relTo = Path.GetDirectoryName(relTo);
                relTo = Path.Combine(relTo, @"..\..\..\..\..\dev");
                Environment.SetEnvironmentVariable("DN3", relTo);
            }

            if (!string.IsNullOrEmpty(installNuget))
                Command.Create("dotnet-new3", new[] { "-i", installNuget }, outputPath: Environment.GetEnvironmentVariable("DN3"))
                    .WorkingDirectory(workingDir)
                    .CaptureStdErr()
                    .CaptureStdOut()
                    .Execute()
                    .Should()
                    .ExitWith(0)
                    .And
                    .NotHaveStdErr();

            Command.Create("dotnet-new3", args, outputPath: Environment.GetEnvironmentVariable("DN3"))
                .WorkingDirectory(workingDir)
                .CaptureStdErr()
                .CaptureStdOut()
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr();

            Command.CreateDotNet("restore", new string[0])
                .WorkingDirectory(workingDir)
                .CaptureStdErr()
                .CaptureStdOut()
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr();

            Command.CreateDotNet("build", new string[0])
                .WorkingDirectory(workingDir)
                .CaptureStdErr()
                .CaptureStdOut()
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr();

            Directory.Delete(workingDir, true);
        }
    }
}
