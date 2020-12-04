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
            string relTo = new Uri(typeof(AllProjectsWork).GetTypeInfo().Assembly.CodeBase, UriKind.Absolute).LocalPath;
            relTo = Path.GetDirectoryName(relTo);
            return Path.Combine(relTo, callerName, _tempDirParent.ToString(), suffix);
        }

        [Theory]
        [InlineData("emptyweb_cs-10", "web")]
        [InlineData("mvc_cs-10", "mvc")]
        [InlineData("mvc_fs-10", "mvc", "-lang", "F#")]
        [InlineData("api_cs-10", "api")]
        [InlineData("emptyweb_cs-11", "web", "-f", "1.1")]
        [InlineData("mvc_cs-11", "mvc", "-f", "1.1")]
        [InlineData("mvc_fs-11", "mvc", "-lang", "F#", "-f", "1.1")]
        [InlineData("api_cs-11", "api", "-f", "1.1")]
        [InlineData("console_cs-10", "console")]
        [InlineData("library_cs-10", "library")]
        public void AllWebProjectsRestoreAndBuild(string testName, params string[] args)
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
