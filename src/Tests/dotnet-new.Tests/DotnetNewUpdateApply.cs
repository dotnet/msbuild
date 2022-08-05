// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.New.Tests
{
    public class DotnetNewUpdateApply : SdkTest
    {
        private readonly ITestOutputHelper _log;

        public DotnetNewUpdateApply(ITestOutputHelper log) : base(log)
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
                .And.HaveStdOutContaining("   dotnet new install <package>::<version>")
                .And.HaveStdOutMatching("   dotnet new install Microsoft\\.DotNet\\.Common\\.ProjectTemplates\\.5\\.0::([\\d\\.a-z-])+");

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
            string templateLocation = Helpers.InstallTestTemplate("TemplateResolution/DifferentLanguagesGroup/BasicFSharp", _log, home, workingDirectory);
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
                .And.HaveStdOutContaining("All template packages are up-to-date.");
        }

        [Fact]
        public void CanShowDeprecationMessage_WhenLegacyCommandIsUsed()
        {
            const string deprecationMessage =
@"Warning: use of 'dotnet new --update-apply' is deprecated. Use 'dotnet new update' instead.
For more information, run: 
   dotnet new update -h";

            var home = TestUtils.CreateTemporaryFolder("Home");
            var commandResult = new DotnetNewCommand(_log, "--update-apply")
                .WithCustomHive(home)
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.NotHaveStdErr();

            Assert.StartsWith(deprecationMessage, commandResult.StdOut);
        }

        [Fact]
        public void DoNotShowDeprecationMessage_WhenNewCommandIsUsed()
        {
            var home = TestUtils.CreateTemporaryFolder("Home");
            var commandResult = new DotnetNewCommand(_log, "update")
                .WithCustomHive(home)
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.NotHaveStdOutContaining("Warning")
                .And.NotHaveStdOutContaining("deprecated");
        }
    }
}
