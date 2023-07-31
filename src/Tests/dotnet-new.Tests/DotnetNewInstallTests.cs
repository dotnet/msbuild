// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.TemplateEngine.TestHelper;
using DiagnosticMessage = Xunit.Sdk.DiagnosticMessage;

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    public partial class DotnetNewInstallTests : BaseIntegrationTest, IClassFixture<DiagnosticFixture>
    {
        private readonly ITestOutputHelper _log;
        private readonly IMessageSink _messageSink;

        public DotnetNewInstallTests(DiagnosticFixture diagnosisFixture, ITestOutputHelper log) : base(log)
        {
            _log = log;
            _messageSink = diagnosisFixture.DiagnosticSink;
        }

        [Theory]
        [InlineData("-i")]
        [InlineData("--install")]
        [InlineData("install")]
        public void CanInstallRemoteNuGetPackage(string commandName)
        {
            new DotnetNewCommand(_log, commandName, "Microsoft.DotNet.Web.ProjectTemplates.5.0")
                .WithCustomHive(CreateTemporaryFolder(folderName: "Home"))
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.NotHaveStdOutContaining("Determining projects to restore...")
                .And.HaveStdOutContaining("The following template packages will be installed:")
                .And.HaveStdOutMatching($"Success: Microsoft\\.DotNet\\.Web\\.ProjectTemplates\\.5\\.0::([\\d\\.a-z-])+ installed the following templates:")
                .And.HaveStdOutContaining("web")
                .And.HaveStdOutContaining("blazorwasm");
        }

        [Theory]
        [InlineData("-i")]
        [InlineData("install")]
        public void CanInstallRemoteNuGetPackage_LatestVariations(string commandName)
        {
            CommandResult command1 = new DotnetNewCommand(_log, commandName, "Microsoft.DotNet.Common.ProjectTemplates.5.0")
                .WithCustomHive(CreateTemporaryFolder(folderName: "Home"))
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute();

            CommandResult command2 = new DotnetNewCommand(_log, commandName, "Microsoft.DotNet.Common.ProjectTemplates.5.0::")
                .WithCustomHive(CreateTemporaryFolder(folderName: "Home"))
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute();

            CommandResult command3 = new DotnetNewCommand(_log, commandName, "Microsoft.DotNet.Common.ProjectTemplates.5.0::*")
                .WithCustomHive(CreateTemporaryFolder(folderName: "Home"))
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute();

            foreach (CommandResult commandResult in new[] { command1, command2, command3 })
            {
                commandResult.Should()
                    .ExitWith(0)
                    .And
                    .NotHaveStdErr()
                    .And.NotHaveStdOutContaining("Determining projects to restore...")
                    .And.HaveStdOutContaining("The following template packages will be installed:")
                    .And.HaveStdOutMatching($"Success: Microsoft\\.DotNet\\.Common\\.ProjectTemplates\\.5\\.0::([\\d\\.a-z-])+ installed the following templates:")
                    .And.HaveStdOutContaining("console")
                    .And.NotHaveStdOutContaining("web");
            }

            // Install command are expected to output the requested version literaly as they got it on input,
            //  but otherwise the outputs are expected to be equal
            string command3Out = command3.StdOut.Replace(
                "Microsoft.DotNet.Common.ProjectTemplates.5.0::*",
                "Microsoft.DotNet.Common.ProjectTemplates.5.0");

            Assert.Equal(command1.StdOut, command2.StdOut);
            Assert.Equal(command1.StdOut, command3Out);
        }

        [Theory]
        [InlineData("-i")]
        [InlineData("install")]
        public void CanInstallRemoteNuGetPackageWithVersion(string commandName)
        {
            new DotnetNewCommand(_log, commandName, "Microsoft.DotNet.Web.ProjectTemplates.5.0::5.0.0")
                .WithCustomHive(CreateTemporaryFolder(folderName: "Home"))
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.NotHaveStdOutContaining("Determining projects to restore...")
                .And.HaveStdOutContaining("The following template packages will be installed:")
                .And.HaveStdOutContaining("Microsoft.DotNet.Web.ProjectTemplates.5.0::5.0.0")
                .And.HaveStdOutContaining($"Success: Microsoft.DotNet.Web.ProjectTemplates.5.0::5.0.0 installed the following templates:")
                .And.HaveStdOutContaining("web")
                .And.HaveStdOutContaining("blazorwasm");
        }

        [Theory]
        [InlineData("-i")]
        [InlineData("install")]
        public void CanInstallRemoteNuGetPackageWithVersionWildcard(string commandName)
        {
            CommandResult command1 = new DotnetNewCommand(_log, commandName, "Microsoft.DotNet.Common.ProjectTemplates.5.0::5.*")
                .WithCustomHive(CreateTemporaryFolder(folderName: "Home"))
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute();

            CommandResult command2 = new DotnetNewCommand(_log, commandName, "Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.*")
                .WithCustomHive(CreateTemporaryFolder(folderName: "Home"))
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute();

            foreach (CommandResult commandResult in new[] { command1, command2 })
            {
                commandResult.Should()
                    .ExitWith(0)
                    .And
                    .NotHaveStdErr()
                    .And.NotHaveStdOutContaining("Determining projects to restore...")
                    .And.HaveStdOutContaining("The following template packages will be installed:")
                    .And.HaveStdOutMatching($"Success: Microsoft\\.DotNet\\.Common\\.ProjectTemplates\\.5\\.0::5\\.0([\\d\\.a-z-])+ installed the following templates:")
                    .And.HaveStdOutContaining("console")
                    .And.NotHaveStdOutContaining("web");
            }

            // Install command are expected to output the requested version literaly as they got it on input,
            //  but otherwise the outputs are expected to be equal
            string command1Out = command1.StdOut.Replace(
                "Microsoft.DotNet.Common.ProjectTemplates.5.0::5.*",
                "Microsoft.DotNet.Common.ProjectTemplates.5.0");

            string command2Out = command2.StdOut.Replace(
                "Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.*",
                "Microsoft.DotNet.Common.ProjectTemplates.5.0");

            Assert.Equal(command1Out, command2Out);
        }

        [Fact]
        public void CanInstallRemoteNuGetPackageWithPrereleaseVersion()
        {
            new DotnetNewCommand(_log, "-i", "Microsoft.Azure.WebJobs.ProjectTemplates::4.0.1844-preview1", "--nuget-source", "https://api.nuget.org/v3/index.json")
                .WithCustomHive(CreateTemporaryFolder(folderName: "Home"))
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutContaining("The following template packages will be installed:")
                .And.HaveStdOutMatching($"Success: Microsoft.Azure.WebJobs.ProjectTemplates::4.0.1844-preview1 installed the following templates:")
                .And.HaveStdOutContaining("func");
        }

        [Theory]
        [InlineData("-i")]
        [InlineData("install")]
        public void CanInstallRemoteNuGetPackageWithNuGetSource(string commandName)
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            new DotnetNewCommand(_log, commandName, "Take.Blip.Client.Templates", "--nuget-source", "https://api.nuget.org/v3/index.json")
                .WithCustomHive(home)
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutContaining("The following template packages will be installed:")
                .And.HaveStdOutMatching($"Success: Take\\.Blip\\.Client\\.Templates::([\\d\\.a-z-])+ installed the following templates:")
                .And.HaveStdOutContaining("blip-console");

            new DotnetNewCommand(_log, commandName, "Take.Blip.Client.Templates", "--add-source", "https://api.nuget.org/v3/index.json")
                .WithCustomHive(home)
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutContaining("The following template packages will be installed:")
                .And.HaveStdOutMatching($"Success: Take\\.Blip\\.Client\\.Templates::([\\d\\.a-z-])+ installed the following templates:")
                .And.HaveStdOutContaining("blip-console");
        }

        [Theory]
        [InlineData("-i")]
        [InlineData("--install")]
        [InlineData("install")]
        public void CanInstallLocalNuGetPackage(string commandName)
        {
            string packageLocation = PackTestNuGetPackage(_log);
            new DotnetNewCommand(_log, commandName, packageLocation)
                .WithCustomHive(CreateTemporaryFolder(folderName: "Home"))
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should().ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("The following template packages will be installed:")
                .And.HaveStdOutMatching($"Success: Microsoft\\.TemplateEngine\\.TestTemplates::([\\d\\.a-z-])+ installed the following templates:")
                .And.HaveStdOutContaining("TestAssets.TemplateWithTags")
                .And.HaveStdOutContaining("TestAssets.ConfigurationKitchenSink");
        }

        [Theory]
        [InlineData("-i")]
        [InlineData("install")]
        public void CanPrintDebugOutputWhenInstalling(string commandName)
        {
            new DotnetNewCommand(_log, commandName, "Microsoft.DotNet.Web.ProjectTemplates.5.0")
                .WithCustomHive(CreateTemporaryFolder(folderName: "Home")).WithDebug()
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.NotHaveStdOutContaining("Determining projects to restore...")
                .And.HaveStdOutContaining("The following template packages will be installed:")
                .And.HaveStdOutMatching($"Success: Microsoft\\.DotNet\\.Web\\.ProjectTemplates\\.5\\.0::([\\d\\.a-z-])+ installed the following templates:")
                .And.HaveStdOutContaining("web")
                .And.HaveStdOutContaining("blazorwasm")
                .And.HaveStdOutMatching("\\[\\d{4}\\-\\d{2}\\-\\d{2} \\d{2}:\\d{2}:\\d{2}\\.\\d{1,3}\\] " + Regex.Escape("[Debug] [Microsoft.TemplateEngine.Edge.Installers.NuGet.NuGetInstaller] => [Execute]: Microsoft.DotNet.Web.ProjectTemplates.5.0 is not a local NuGet package."))
                .And.HaveStdOutMatching("\\[\\d{4}\\-\\d{2}\\-\\d{2} \\d{2}:\\d{2}:\\d{2}\\.\\d{1,3}\\] " + Regex.Escape("[Debug] [Microsoft.TemplateEngine.Edge.Installers.NuGet.NuGetInstaller] => [Execute]: Microsoft.DotNet.Web.ProjectTemplates.5.0 is identified as the downloadable NuGet package."));
        }

        [Theory]
        [InlineData("-i")]
        [InlineData("--install")]
        [InlineData("install")]
        public void CanInstallLocalFolder(string commandName)
        {
            string basicFSharp = GetTestTemplateLocation("TemplateResolution/DifferentLanguagesGroup/BasicFSharp");
            new DotnetNewCommand(_log, commandName, basicFSharp)
                .WithCustomHive(CreateTemporaryFolder(folderName: "Home"))
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutContaining("The following template packages will be installed:")
                .And.HaveStdOutContaining($"Success: {basicFSharp} installed the following templates:")
                .And.HaveStdOutContaining("basic");
        }

        [Theory]
        [InlineData("-i")]
        [InlineData("install")]
        public void PrintOnlyNewlyInstalledTemplates(string commandName)
        {
            string home = CreateTemporaryFolder(folderName: "Home");

            new DotnetNewCommand(_log, commandName, "Microsoft.DotNet.Common.ProjectTemplates.5.0")
               .WithCustomHive(home)
               .WithWorkingDirectory(CreateTemporaryFolder())
               .Execute()
               .Should()
               .ExitWith(0)
               .And
               .NotHaveStdErr()
               .And.HaveStdOutContaining("console")
               .And.HaveStdOutContaining("Console App");

            new DotnetNewCommand(_log, commandName, "Microsoft.DotNet.Web.ProjectTemplates.5.0")
                .WithCustomHive(home)
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutContaining("web")
                .And.HaveStdOutContaining("blazorwasm")
                .And.NotHaveStdOutContaining("console");
        }

        [Theory]
        [InlineData("-i")]
        [InlineData("install")]
        public void CannotInstallUnknownRemotePackage(string commandName)
        {
            string home = CreateTemporaryFolder(folderName: "Home");

            new DotnetNewCommand(_log, commandName, "BlaBlaBla")
               .WithCustomHive(home)
               .WithWorkingDirectory(CreateTemporaryFolder())
               .Execute()
               .Should().Fail()
               .And.HaveStdErrContaining("BlaBlaBla could not be installed, the package does not exist");
        }

        [Theory]
        [InlineData("-i")]
        [InlineData("install")]
        public void CannotInstallRemotePackageWithIncorrectVersion(string commandName)
        {
            string home = CreateTemporaryFolder(folderName: "Home");

            new DotnetNewCommand(_log, commandName, "Microsoft.DotNet.Web.ProjectTemplates.5.0::16.0.0")
               .WithCustomHive(home)
               .WithWorkingDirectory(CreateTemporaryFolder())
               .Execute()
               .Should().Fail()
               .And.HaveStdErrContaining("Microsoft.DotNet.Web.ProjectTemplates.5.0::16.0.0 could not be installed, the package does not exist");
        }

        [Fact]
        public void CanInstallSeveralSources()
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            string basicFSharp = GetTestTemplateLocation("TemplateResolution/DifferentLanguagesGroup/BasicFSharp");
            string basicVB = GetTestTemplateLocation("TemplateResolution/DifferentLanguagesGroup/BasicVB");

            new DotnetNewCommand(_log, "-i", "Microsoft.DotNet.Web.ProjectTemplates.5.0", "-i", "Microsoft.DotNet.Common.ProjectTemplates.5.0", "-i", basicFSharp, "-i", basicVB)
                .WithCustomHive(home).WithDebug()
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.NotHaveStdOutContaining("Determining projects to restore...")
                .And.HaveStdOutContaining("web")
                .And.HaveStdOutContaining("blazorwasm")
                .And.HaveStdOutContaining("console")
                .And.HaveStdOutContaining("classlib")
                .And.HaveStdOutContaining("basic");
        }

        [Fact]
        public void CanInstallSeveralSources_V2()
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            string basicFSharp = GetTestTemplateLocation("TemplateResolution/DifferentLanguagesGroup/BasicFSharp");
            string basicVB = GetTestTemplateLocation("TemplateResolution/DifferentLanguagesGroup/BasicVB");

            new DotnetNewCommand(_log, "install", "Microsoft.DotNet.Web.ProjectTemplates.5.0", "Microsoft.DotNet.Common.ProjectTemplates.5.0", basicFSharp, basicVB)
                .WithCustomHive(home).WithDebug()
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.NotHaveStdOutContaining("Determining projects to restore...")
                .And.HaveStdOutContaining("web")
                .And.HaveStdOutContaining("blazorwasm")
                .And.HaveStdOutContaining("console")
                .And.HaveStdOutContaining("classlib")
                .And.HaveStdOutContaining("basic");
        }

        [Theory]
        [InlineData("-i", "-u")]
        [InlineData("install", "uninstall")]
        public void CanUpdateSameSource_NuGet(string installCommandName, string uninstallCommandName)
        {
            string home = CreateTemporaryFolder(folderName: "Home");

            new DotnetNewCommand(_log, installCommandName, "Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.0")
                .WithCustomHive(home)
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutContaining("console")
                .And.HaveStdOutContaining("classlib");

            new DotnetNewCommand(_log, uninstallCommandName)
                 .WithCustomHive(home)
                 .WithWorkingDirectory(CreateTemporaryFolder())
                 .Execute()
                 .Should().ExitWith(0)
                 .And.NotHaveStdErr()
                 .And.HaveStdOutContaining("Microsoft.DotNet.Common.ProjectTemplates.5.0")
                 .And.HaveStdOutContaining("Version: 5.0.0")
                 .And.NotHaveStdOutContaining("Version: 5.0.1");

            Assert.True(File.Exists(Path.Combine(home, "packages", "Microsoft.DotNet.Common.ProjectTemplates.5.0.5.0.0.nupkg")));

            new DotnetNewCommand(_log, installCommandName, "Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.1")
                 .WithCustomHive(home)
                 .WithWorkingDirectory(CreateTemporaryFolder())
                 .Execute()
                 .Should().ExitWith(0)
                 .And.NotHaveStdErr()
                 .And.HaveStdOutContaining("The following template packages will be installed:")
                 .And.HaveStdOutContaining("Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.1")
                 .And.HaveStdOutContaining("Microsoft.DotNet.Common.ProjectTemplates.5.0 (version 5.0.0) is already installed, it will be replaced with version 5.0.1")
                 .And.HaveStdOutContaining("Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.0 was successfully uninstalled")
                 .And.HaveStdOutContaining($"Success: Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.1 installed the following templates:")
                 .And.HaveStdOutContaining("console")
                 .And.HaveStdOutContaining("classlib");

            new DotnetNewCommand(_log, uninstallCommandName)
                 .WithCustomHive(home)
                 .WithWorkingDirectory(CreateTemporaryFolder())
                 .Execute()
                 .Should().ExitWith(0)
                 .And.NotHaveStdErr()
                 .And.HaveStdOutContaining("Microsoft.DotNet.Common.ProjectTemplates.5.0")
                 .And.HaveStdOutContaining("Version: 5.0.1")
                 .And.NotHaveStdOutContaining("Version: 5.0.0");

            Assert.False(File.Exists(Path.Combine(home, "packages", "Microsoft.DotNet.Common.ProjectTemplates.5.0.5.0.0.nupkg")));
            Assert.True(File.Exists(Path.Combine(home, "packages", "Microsoft.DotNet.Common.ProjectTemplates.5.0.5.0.1.nupkg")));
        }

        [Theory]
        [InlineData("-i", "-u")]
        [InlineData("install", "uninstall")]
        public async Task InstallingSamePackageFromRemoteUpdatesLocal(string installCommandName, string uninstallCommandName)
        {
            _messageSink.OnMessage(new DiagnosticMessage($"{nameof(InstallingSamePackageFromRemoteUpdatesLocal)} started."));
            string home = CreateTemporaryFolder(folderName: "Home");

            using var packageManager = new PackageManager();
            string packageLocation = await packageManager.GetNuGetPackage(
                "Microsoft.DotNet.Common.ProjectTemplates.5.0",
                minimumVersion: new NuGet.Versioning.NuGetVersion(6, 0, 0),
                logger: new XunitNuGetLogger(_messageSink)).ConfigureAwait(false);

            _messageSink.OnMessage(new DiagnosticMessage($"{nameof(InstallingSamePackageFromRemoteUpdatesLocal)}: Microsoft.DotNet.Common.ProjectTemplates.5.0 is downloaded to {packageLocation}.)"));

            new DotnetNewCommand(_log, installCommandName, packageLocation)
                .WithCustomHive(home)
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should().ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutMatching($"Success: Microsoft\\.DotNet\\.Common\\.ProjectTemplates\\.5\\.0::([\\d\\.a-z-])+ installed the following templates:")
                .And.HaveStdOutContaining("console")
                .And.HaveStdOutContaining("classlib");

            new DotnetNewCommand(_log, uninstallCommandName)
                 .WithCustomHive(home)
                 .WithWorkingDirectory(CreateTemporaryFolder())
                 .Execute()
                 .Should().ExitWith(0)
                 .And.NotHaveStdErr()
                 .And.HaveStdOutContaining("Microsoft.DotNet.Common.ProjectTemplates.5.0")
                 .And.HaveStdOutContaining("Author: Microsoft")
                 .And.HaveStdOutContaining("Version:")
                 .And.NotHaveStdOutContaining("Version: 5.0.0");

            new DotnetNewCommand(_log, installCommandName, "Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.0")
                .WithCustomHive(home)
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should().ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("The following template packages will be installed:")
                .And.HaveStdOutContaining("Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.0")
                .And.HaveStdOutMatching("Microsoft\\.DotNet\\.Common\\.ProjectTemplates\\.5\\.0 \\(version ([\\d\\.a-z-])+\\) is already installed, it will be replaced with version 5\\.0\\.0")
                .And.HaveStdOutMatching("Microsoft\\.DotNet\\.Common\\.ProjectTemplates\\.5\\.0::([\\d\\.a-z-])+ was successfully uninstalled")
                .And.HaveStdOutContaining($"Success: Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.0 installed the following templates:")
                .And.HaveStdOutContaining("console")
                .And.HaveStdOutContaining("classlib");

            new DotnetNewCommand(_log, uninstallCommandName)
                .WithCustomHive(home)
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should().ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("Microsoft.DotNet.Common.ProjectTemplates.5.0")
                .And.HaveStdOutContaining("Author: Microsoft")
                .And.HaveStdOutContaining("Version: 5.0.0");

            _messageSink.OnMessage(new DiagnosticMessage($"{nameof(InstallingSamePackageFromRemoteUpdatesLocal)} finished."));
        }

        [Theory]
        [InlineData("-i")]
        [InlineData("install")]
        public void CanExpandWhenInstall(string commandName)
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            string testTemplateLocation = GetTestTemplateLocation(string.Empty);
            string testTemplateLocationAbsolute = Path.GetFullPath(testTemplateLocation);
            string pattern = testTemplateLocation + Path.DirectorySeparatorChar + "*";

            new DotnetNewCommand(_log, commandName, pattern)
                .WithCustomHive(home)
                .Execute()
                .Should().ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("The following template packages will be installed:")
                .And.HaveStdOutContaining(Path.Combine(testTemplateLocationAbsolute, "ConfigurationKitchenSink"))
                .And.HaveStdOutContaining(Path.Combine(testTemplateLocationAbsolute, "TemplateResolution"))
                .And.HaveStdOutContaining(Path.Combine(testTemplateLocationAbsolute, "TemplateWithSourceName"))
                .And.HaveStdOutContaining($"Success: {Path.Combine(testTemplateLocationAbsolute, "ConfigurationKitchenSink")} installed the following templates:")
                .And.HaveStdOutContaining($"Success: {Path.Combine(testTemplateLocationAbsolute, "TemplateResolution")} installed the following templates:")
                .And.HaveStdOutContaining($"Success: {Path.Combine(testTemplateLocationAbsolute, "TemplateWithSourceName")} installed the following templates:")
                .And.HaveStdOutContaining("basic")
                .And.HaveStdOutContaining("TestAssets.ConfigurationKitchenSink");
        }

        [Theory]
        [InlineData("-i")]
        [InlineData("install")]
        public void CannotInstallInvalidPackage(string commandName)
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            string codebase = typeof(Program).GetTypeInfo().Assembly.Location;
            new DotnetNewCommand(_log, commandName, codebase)
                .WithCustomHive(home)
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should().Fail()
                .And.HaveStdErrContaining($"{codebase} is not supported");
        }

        [Fact]
        public void ReinstallDoesntRemoveTemplates()
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            string packageLocation = PackTestNuGetPackage(_log);

            new DotnetNewCommand(_log, "-i", packageLocation)
                .WithCustomHive(CreateTemporaryFolder(folderName: "Home"))
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should().ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("The following template packages will be installed:")
                .And.HaveStdOutMatching($"Success: Microsoft\\.TemplateEngine\\.TestTemplates::([\\d\\.a-z-])+ installed the following templates:")
                .And.HaveStdOutContaining("TestAssets.TemplateWithTags")
                .And.HaveStdOutContaining("TestAssets.ConfigurationKitchenSink");

            new DotnetNewCommand(_log, "-i", packageLocation)
                .WithCustomHive(CreateTemporaryFolder(folderName: "Home"))
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should().ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("The following template packages will be installed:")
                .And.HaveStdOutMatching($"Success: Microsoft\\.TemplateEngine\\.TestTemplates::([\\d\\.a-z-])+ installed the following templates:")
                .And.HaveStdOutContaining("TestAssets.TemplateWithTags")
                .And.HaveStdOutContaining("TestAssets.ConfigurationKitchenSink");
        }

        [Fact]
        public void CannotInstallTemplateWithoutMandatoryConfig()
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            string invalidTemplatePath = GetTestTemplateLocation("Invalid/MissingMandatoryConfig");
            new DotnetNewCommand(_log, "-i", invalidTemplatePath)
                .WithCustomHive(home)
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining($"Error: Failed to load template from {invalidTemplatePath}")
                .And.HaveStdOutContaining($"Details: The template root is outside the specified install source location.")
                .And.HaveStdOutContaining($"No templates were found in the package {invalidTemplatePath}.");
        }

        [Fact]
        public void CanShowWarning_WhenHostDataIsIncorrect()
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            string invalidTemplatePath = GetTestTemplateLocation("Invalid/InvalidHostData");
            new DotnetNewCommand(_log, "-i", invalidTemplatePath)
                .WithDebug()
                .WithCustomHive(home)
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("Warning: Failed to load host data ")
                .And.HaveStdOutContaining($"Success: {invalidTemplatePath} installed the following templates:")
                .And.HaveStdOutContaining("TestAssets.Invalid.InvalidHostData");
        }
    }
}
