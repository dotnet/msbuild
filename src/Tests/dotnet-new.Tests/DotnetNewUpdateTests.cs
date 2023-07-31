// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    public partial class DotnetNewUpdateTests : BaseIntegrationTest
    {
        private readonly ITestOutputHelper _log;

        public DotnetNewUpdateTests(ITestOutputHelper log) : base(log)
        {
            _log = log;
        }

        [Theory]
        [InlineData("--update-check")]
        [InlineData("update --check-only")]
        [InlineData("update --dry-run")]
        public void CanCheckForUpdate(string testCase)
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            new DotnetNewCommand(_log, "install", "Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.0")
                .WithCustomHive(home).WithoutBuiltInTemplates()
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.NotHaveStdOutContaining("Determining projects to restore...")
                .And.HaveStdOutContaining("console")
                .And.HaveStdOutContaining("classlib");

            new DotnetNewCommand(_log, testCase.Split(" "))
                .WithCustomHive(home).WithoutBuiltInTemplates()
                .WithWorkingDirectory(CreateTemporaryFolder())
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
        }

        [Fact]
        public void ReportsErrorOnUpdateCheckOfLocalPackage()
        {
            string nugetName = "TestNupkgInstallTemplate";
            string nugetVersion = "0.0.1";
            string nugetFullName = $"{nugetName}::{nugetVersion}";
            string nugetFileName = $"{nugetName}.{nugetVersion}.nupkg";
            string workingDirectory = CreateTemporaryFolder();
            string home = CreateTemporaryFolder(folderName: "Home");

            InstallNuGetTemplate(
                Path.Combine(DotnetNewTestPackagesBasePath, nugetFileName),
                _log,
                home,
                workingDirectory);

            new DotnetNewCommand(_log, "--update-check")
                .WithCustomHive(home).WithoutBuiltInTemplates()
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .Fail()
                .And.HaveStdErr($@"Failed to check update for {nugetFullName}: the package is not available in configured NuGet feeds.


For details on the exit code, refer to https://aka.ms/templating-exit-codes#106");
        }

        [Theory]
        [InlineData("--update-check")]
        [InlineData("update --check-only")]
        [InlineData("update --dry-run")]
        public void DoesNotShowUpdatesWhenAllTemplatesAreUpToDate(string testCase)
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();
            InstallTestTemplate("TemplateResolution/DifferentLanguagesGroup/BasicFSharp", _log, home, workingDirectory);
            new DotnetNewCommand(_log, "install", "Microsoft.DotNet.Common.ProjectTemplates.5.0")
                .WithCustomHive(home)
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.NotHaveStdOutContaining("Determining projects to restore...")
                .And.HaveStdOutContaining("console")
                .And.HaveStdOutContaining("classlib");

            new DotnetNewCommand(_log, testCase.Split(" "))
                .WithCustomHive(home)
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutContaining("All template packages are up-to-date.");
        }

        [Fact]
        public void PrintInfoOnUpdateOnCreation()
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            new DotnetNewCommand(_log, "install", "Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.0")
                .WithCustomHive(home).WithoutBuiltInTemplates()
                .WithWorkingDirectory(CreateTemporaryFolder())
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
                  .WithWorkingDirectory(CreateTemporaryFolder())
                  .Execute()
                  .Should()
                  .ExitWith(0)
                  .And
                  .NotHaveStdErr()
                  .And.HaveStdOutContaining("The template \"Console Application\" was created successfully.")
                  .And.HaveStdOutContaining("An update for template package 'Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.0' is available")
                  .And.HaveStdOutContaining("To update the package use:")
                  .And.HaveStdOutMatching("   dotnet new install Microsoft.DotNet.Common.ProjectTemplates.5.0::([\\d\\.a-z-])+");
        }

        [Fact]
        public void DoesNotPrintUpdateInfoOnCreation_WhenNoUpdateCheckOption()
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            new DotnetNewCommand(_log, "install", "Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.0")
                .WithCustomHive(home).WithoutBuiltInTemplates()
                .WithWorkingDirectory(CreateTemporaryFolder())
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
                  .WithWorkingDirectory(CreateTemporaryFolder())
                  .Execute()
                  .Should()
                  .ExitWith(0)
                  .And
                  .NotHaveStdErr()
                  .And.HaveStdOutContaining("The template \"Console Application\" was created successfully.")
                  .And.NotHaveStdOutContaining("An update for template package 'Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.0' is available")
                  .And.NotHaveStdOutContaining("To update the package use:")
                  .And.NotHaveStdOutContaining("   dotnet new --install Microsoft.DotNet.Common.ProjectTemplates.5.0::([\\d\\.a-z-])+");

            new DotnetNewCommand(_log, "console", "-o", "update-check")
                  .WithCustomHive(home).WithoutBuiltInTemplates()
                  .WithWorkingDirectory(CreateTemporaryFolder())
                  .Execute()
                  .Should()
                  .ExitWith(0)
                  .And
                  .NotHaveStdErr()
                  .And.HaveStdOutContaining("The template \"Console Application\" was created successfully.")
                  .And.HaveStdOutContaining("An update for template package 'Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.0' is available")
                  .And.HaveStdOutContaining("To update the package use:")
                  .And.HaveStdOutMatching("   dotnet new install Microsoft.DotNet.Common.ProjectTemplates.5.0::([\\d\\.a-z-])+");
        }

        [Fact]
        public void DoesNotPrintUpdateInfoOnCreation_WhenLatestVersionIsInstalled()
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            new DotnetNewCommand(_log, "install", "Microsoft.DotNet.Common.ProjectTemplates.5.0")
                .WithCustomHive(home).WithoutBuiltInTemplates()
                .WithWorkingDirectory(CreateTemporaryFolder())
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
                  .WithWorkingDirectory(CreateTemporaryFolder())
                  .Execute()
                  .Should()
                  .ExitWith(0)
                  .And
                  .NotHaveStdErr()
                  .And.HaveStdOutContaining("The template \"Console App\" was created successfully.")
                  .And.NotHaveStdOutMatching("An update for template package 'Microsoft.DotNet.Common.ProjectTemplates.5.0::([\\d\\.a-z-])+' is available")
                  .And.NotHaveStdOutContaining("To update the package use:")
                  .And.NotHaveStdOutMatching("   dotnet new install Microsoft.DotNet.Common.ProjectTemplates.5.0::([\\d\\.a-z-])+");
        }

        [Fact]
        public void CanShowDeprecationMessage_WhenLegacyCommandIsUsed_Check()
        {
            const string deprecationMessage =
@"Warning: use of 'dotnet new --update-check' is deprecated. Use 'dotnet new update --check-only' instead.
For more information, run: 
   dotnet new update -h";

            string home = CreateTemporaryFolder(folderName: "Home");
            Utils.CommandResult commandResult = new DotnetNewCommand(_log, "--update-check")
                .WithCustomHive(home)
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.NotHaveStdErr();

            Assert.StartsWith(deprecationMessage, commandResult.StdOut);
        }

        [Fact]
        public void DoNotShowDeprecationMessage_WhenNewCommandIsUsed_Check()
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            Utils.CommandResult commandResult = new DotnetNewCommand(_log, "update", "--check-only")
                .WithCustomHive(home)
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.NotHaveStdOutContaining("Warning")
                .And.NotHaveStdOutContaining("deprecated");
        }

        [Theory]
        [InlineData("--update-apply")]
        [InlineData("update")]
        public void CanApplyUpdates(string testCase)
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            new DotnetNewCommand(_log, "install", "Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.0")
                .WithCustomHive(home).WithoutBuiltInTemplates()
                .WithWorkingDirectory(CreateTemporaryFolder())
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
                .WithWorkingDirectory(CreateTemporaryFolder())
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
                .WithWorkingDirectory(CreateTemporaryFolder())
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
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();
            string templateLocation = InstallTestTemplate("TemplateResolution/DifferentLanguagesGroup/BasicFSharp", _log, home, workingDirectory);
            new DotnetNewCommand(_log, "install", "Microsoft.DotNet.Common.ProjectTemplates.5.0")
                .WithCustomHive(home)
                .WithWorkingDirectory(CreateTemporaryFolder())
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
                .WithWorkingDirectory(CreateTemporaryFolder())
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

            string home = CreateTemporaryFolder(folderName: "Home");
            Utils.CommandResult commandResult = new DotnetNewCommand(_log, "--update-apply")
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
            string home = CreateTemporaryFolder(folderName: "Home");
            Utils.CommandResult commandResult = new DotnetNewCommand(_log, "update")
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
