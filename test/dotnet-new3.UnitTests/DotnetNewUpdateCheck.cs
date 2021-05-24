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
    public class DotnetNewUpdateCheck
    {
        private readonly ITestOutputHelper _log;

        public DotnetNewUpdateCheck(ITestOutputHelper log)
        {
            _log = log;
        }

        [Fact]
        public void CanCheckForUpdate()
        {
            var home = TestUtils.CreateTemporaryFolder("Home");
            new DotnetNewCommand(_log, "-i", "Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.0")
                .WithCustomHive(home).WithoutBuiltInTemplates().Quietly()
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.NotHaveStdOutContaining("Determining projects to restore...")
                .And.HaveStdOutContaining("console")
                .And.HaveStdOutContaining("classlib");

            new DotnetNewCommand(_log, "--update-check")
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
                .And.HaveStdOutContaining("   dotnet new3 --install <PACKAGE_ID>::<VERSION>")
                .And.HaveStdOutMatching("   dotnet new3 --install Microsoft\\.DotNet\\.Common\\.ProjectTemplates\\.5\\.0::([\\d\\.a-z-])+");
        }

        [Fact]
        public void DoesNotShowUpdatesWhenAllTemplatesAreUpToDate()
        {
            var home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            Helpers.InstallTestTemplate("TemplateResolution/DifferentLanguagesGroup/BasicFSharp", _log, workingDirectory, home);
            new DotnetNewCommand(_log, "-i", "Microsoft.DotNet.Common.ProjectTemplates.5.0")
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

            new DotnetNewCommand(_log, "--update-check")
                .WithCustomHive(home)
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOut("All template packages are up-to-date.");
        }

        [Fact]
        public void PrintInfoOnUpdateOnCreation()
        {
            var home = TestUtils.CreateTemporaryFolder("Home");
            new DotnetNewCommand(_log, "-i", "Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.0")
                .WithCustomHive(home).WithoutBuiltInTemplates().Quietly()
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutContaining("Success:")
                .And.HaveStdOutContaining("console")
                .And.HaveStdOutContaining("classlib");

            new DotnetNewCommand(_log, "console")
                  .WithCustomHive(home).WithoutBuiltInTemplates()
                  .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                  .Execute()
                  .Should()
                  .ExitWith(0)
                  .And
                  .NotHaveStdErr()
                  .And.HaveStdOutContaining("The template \"Console Application\" was created successfully.")
                  .And.HaveStdOutContaining("An update for template package 'Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.0' is available")
                  .And.HaveStdOutContaining("To update the package use:")
                  .And.HaveStdOutMatching("   dotnet new3 --install Microsoft.DotNet.Common.ProjectTemplates.5.0::([\\d\\.a-z-])+");
        }

        [Fact]
        public void DoesNotPrintUpdateInfoOnCreation_WhenNoUpdateCheckOption()
        {
            var home = TestUtils.CreateTemporaryFolder("Home");
            new DotnetNewCommand(_log, "-i", "Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.0")
                .WithCustomHive(home).WithoutBuiltInTemplates().Quietly()
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutContaining("Success:")
                .And.HaveStdOutContaining("console")
                .And.HaveStdOutContaining("classlib");

            new DotnetNewCommand(_log, "console", "--no-update-check", "-o", "no-update-check")
                  .WithCustomHive(home).WithoutBuiltInTemplates()
                  .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                  .Execute()
                  .Should()
                  .ExitWith(0)
                  .And
                  .NotHaveStdErr()
                  .And.HaveStdOutContaining("The template \"Console Application\" was created successfully.")
                  .And.NotHaveStdOutContaining("An update for template package 'Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.0' is available")
                  .And.NotHaveStdOutContaining("To update the package use:")
                  .And.NotHaveStdOutContaining("   dotnet new3 --install Microsoft.DotNet.Common.ProjectTemplates.5.0::([\\d\\.a-z-])+");

            new DotnetNewCommand(_log, "console", "-o", "update-check")
                  .WithCustomHive(home).WithoutBuiltInTemplates()
                  .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                  .Execute()
                  .Should()
                  .ExitWith(0)
                  .And
                  .NotHaveStdErr()
                  .And.HaveStdOutContaining("The template \"Console Application\" was created successfully.")
                  .And.HaveStdOutContaining("An update for template package 'Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.0' is available")
                  .And.HaveStdOutContaining("To update the package use:")
                  .And.HaveStdOutMatching("   dotnet new3 --install Microsoft.DotNet.Common.ProjectTemplates.5.0::([\\d\\.a-z-])+");
        }

        [Fact]
        public void DoesNotPrintUpdateInfoOnCreation_WhenLatestVersionIsInstalled()
        {
            var home = TestUtils.CreateTemporaryFolder("Home");
            new DotnetNewCommand(_log, "-i", "Microsoft.DotNet.Common.ProjectTemplates.5.0")
                .WithCustomHive(home).WithoutBuiltInTemplates().Quietly()
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutContaining("Success:")
                .And.HaveStdOutContaining("console")
                .And.HaveStdOutContaining("classlib");

            new DotnetNewCommand(_log, "console")
                  .WithCustomHive(home).WithoutBuiltInTemplates()
                  .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                  .Execute()
                  .Should()
                  .ExitWith(0)
                  .And
                  .NotHaveStdErr()
                  .And.HaveStdOutContaining("The template \"Console Application\" was created successfully.")
                  .And.NotHaveStdOutContaining("An update for template package 'Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.0' is available")
                  .And.NotHaveStdOutContaining("To update the package use:")
                  .And.NotHaveStdOutMatching("    dotnet new3 --install Microsoft.DotNet.Common.ProjectTemplates.5.0::([\\d\\.a-z-])+");
        }
    }
}
