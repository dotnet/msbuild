using FluentAssertions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Microsoft.NET.TestFramework.Assertions;

namespace dotnet_new3.UnitTests
{
    static class Helpers
    {
        public static string CreateTemporaryFolder([CallerMemberName] string name = "")
        {
            string workingDir = Path.Combine(Path.GetTempPath(), "DotnetNew3_Tests", Guid.NewGuid().ToString(), name);
            Directory.CreateDirectory(workingDir);
            return workingDir;
        }

        public static string HomeEnvironmentVariableName { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "USERPROFILE" : "HOME";

        internal static void InstallTestTemplate(string templateName, ITestOutputHelper log, string workingDirectory, string homeDirectory)
        {
            string codebase = typeof(Program).GetTypeInfo().Assembly.Location;
            Uri cb = new Uri(codebase);
            string asmPath = cb.LocalPath;
            string dir = Path.GetDirectoryName(asmPath);
            string testTemplate = Path.Combine(dir, "..", "..", "..", "..", "..", "test", "Microsoft.TemplateEngine.TestTemplates", "test_templates", templateName) + Path.DirectorySeparatorChar;

            new DotnetNewCommand(log, "-i", testTemplate)
                  .WithWorkingDirectory(workingDirectory)
                  .WithEnvironmentVariable(HomeEnvironmentVariableName, homeDirectory)
                  .Execute()
                  .Should()
                  .ExitWith(0)
                  .And
                  .NotHaveStdErr();
        }

        internal static void InstallNuGetTemplate(string packageName, ITestOutputHelper log, string workingDirectory, string homeDirectory)
        {
            new DotnetNewCommand(log, "-i", packageName)
                  .WithWorkingDirectory(workingDirectory)
                  .WithEnvironmentVariable(HomeEnvironmentVariableName, homeDirectory)
                  .Execute()
                  .Should()
                  .ExitWith(0)
                  .And
                  .NotHaveStdErr();
        }
    }
}
