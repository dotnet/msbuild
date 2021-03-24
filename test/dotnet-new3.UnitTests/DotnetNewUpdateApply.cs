// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;
using Xunit.Abstractions;

namespace dotnet_new3.UnitTests
{
    public class DotnetNewUpdateApply
    {
        private readonly ITestOutputHelper _log;

        public DotnetNewUpdateApply(ITestOutputHelper log)
        {
            _log = log;
        }

        [Fact]
        public void CanApplyUpdates()
        {
            var home = TestUtils.CreateTemporaryFolder("Home");
            new DotnetNewCommand(_log, "-i", "Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.0", "--quiet")
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .WithEnvironmentVariable(TestUtils.HomeEnvironmentVariableName, home)
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.NotHaveStdOutContaining("Determining projects to restore...")
                .And.HaveStdOutContaining("console")
                .And.HaveStdOutContaining("classlib");

            new DotnetNewCommand(_log, "--update-check")
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .WithEnvironmentVariable(TestUtils.HomeEnvironmentVariableName, home)
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutContaining("An update for template package 'Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.0' is available.");

            new DotnetNewCommand(_log, "--update-apply")
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .WithEnvironmentVariable(TestUtils.HomeEnvironmentVariableName, home)
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutContaining("The following template packages will be updated:")
                .And.HaveStdOutContaining("Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.0 was successfully uninstalled")
                .And.NotHaveStdOutContaining("Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.0 is already installed and will be replaced with version")
                .And.HaveStdOutMatching($"^Success: Microsoft\\.DotNet\\.Common\\.ProjectTemplates\\.5\\.0::([\\d\\.a-z-])+ installed the following templates:\\s*$", System.Text.RegularExpressions.RegexOptions.Multiline)
                .And.HaveStdOutContaining("console")
                .And.HaveStdOutContaining("Console Application");
        }

        [Fact]
        public void DoesNotApplyUpdatesWhenAllTemplatesAreUpToDate()
        {
            var home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            string templateLocation = Helpers.InstallTestTemplate("TemplateResolution/DifferentLanguagesGroup/BasicFSharp", _log, workingDirectory, home);
            new DotnetNewCommand(_log, "-i", "Microsoft.DotNet.Common.ProjectTemplates.5.0", "--quiet")
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .WithEnvironmentVariable(TestUtils.HomeEnvironmentVariableName, home)
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.NotHaveStdOutContaining("Determining projects to restore...")
                .And.HaveStdOutContaining("console")
                .And.HaveStdOutContaining("classlib");

            new DotnetNewCommand(_log, "--update-apply")
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .WithEnvironmentVariable(TestUtils.HomeEnvironmentVariableName, home)
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOut("All template packages are up-to-date.");
        }
    }
}
