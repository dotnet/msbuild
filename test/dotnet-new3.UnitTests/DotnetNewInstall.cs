using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace dotnet_new3.UnitTests
{
    public class DotnetNewInstallTests
    {
        private readonly ITestOutputHelper _log;

        public DotnetNewInstallTests(ITestOutputHelper log)
        {
            _log = log;
        }

        [Fact]
        public void OnlyPrintNewlyInstalledTemplates()
        {
            var home = Helpers.CreateTemporaryFolder("Home");
            // Do actual install...
            new DotnetNewCommand(_log, "-i", "Microsoft.DotNet.Web.ProjectTemplates.5.0", "--quiet")
                .WithWorkingDirectory(Helpers.CreateTemporaryFolder())
                .WithEnvironmentVariable(Helpers.HomeEnvironmentVariableName, home)
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And
                .NotHaveStdOutContaining("Determining projects to restore...");
        }
    }
}
