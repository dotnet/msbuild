// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using FluentAssertions;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;
using Xunit.Abstractions;

namespace Dotnet_new3.IntegrationTests
{
    public class DotnetNewUpdateApply
    {
        private readonly ITestOutputHelper _log;

        public DotnetNewUpdateApply(ITestOutputHelper log)
        {
            _log = log;
        }

        [Theory]
        [InlineData("--update-apply")]
        [InlineData("update")]
        public void CanApplyUpdates(string testCase)
        {
            var home = TestUtils.CreateTemporaryFolder("Home");
            new DotnetNewCommand(_log, "install", "Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.0")
                .WithCustomHive(home).WithoutBuiltInTemplates()
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.NotHaveStdOutContaining("Determining projects to restore...")
                .And.HaveStdOutContaining("console")
                .And.HaveStdOutContaining("classlib");

            new DotnetNewCommand(_log, "update", "--check-only")
                .WithCustomHive(home).WithoutBuiltInTemplates()
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutContaining("An update for template packages is available:")
                .And.HaveStdOutMatching("Package\\s+Current\\s+Latest")
                .And.HaveStdOutMatching("Microsoft.DotNet.Common.ProjectTemplates.5.0\\s+5.0.0\\s+([\\d\\.a-z-])+")
                .And.HaveStdOutContaining("To update the package use:")
                .And.HaveStdOutContaining("   dotnet-new3 new3 install <package>::<version>")
                .And.HaveStdOutMatching("   dotnet-new3 new3 install Microsoft\\.DotNet\\.Common\\.ProjectTemplates\\.5\\.0::([\\d\\.a-z-])+");

            new DotnetNewCommand(_log, testCase)
                .WithCustomHive(home).WithoutBuiltInTemplates()
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
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
                .And.HaveStdOutContaining("Console App");
        }

        [Theory]
        [InlineData("--update-apply")]
        [InlineData("update")]
        public void DoesNotApplyUpdatesWhenAllTemplatesAreUpToDate(string commandName)
        {
            var home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            string templateLocation = Helpers.InstallTestTemplate("TemplateResolution/DifferentLanguagesGroup/BasicFSharp", _log, workingDirectory, home);
            new DotnetNewCommand(_log, "install", "Microsoft.DotNet.Common.ProjectTemplates.5.0")
                .WithCustomHive(home)
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.NotHaveStdOutContaining("Determining projects to restore...")
                .And.HaveStdOutContaining("console")
                .And.HaveStdOutContaining("classlib");

            new DotnetNewCommand(_log, commandName)
                .WithCustomHive(home)
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOut("All template packages are up-to-date.");
        }
    }
}
