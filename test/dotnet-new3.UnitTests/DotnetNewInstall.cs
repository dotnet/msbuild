// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Dotnet_new3.IntegrationTests
{
    public class DotnetNewInstallTests : IClassFixture<DiagnosticFixture>
    {
        private readonly ITestOutputHelper _log;
        private readonly IMessageSink _messageSink;

        public DotnetNewInstallTests(DiagnosticFixture diagnosisFixture, ITestOutputHelper log)
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
                .WithCustomHive()
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
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
            var command1 = new DotnetNewCommand(_log, commandName, "Microsoft.DotNet.Common.ProjectTemplates.5.0")
                .WithCustomHive()
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute();

            var command2 = new DotnetNewCommand(_log, commandName, "Microsoft.DotNet.Common.ProjectTemplates.5.0::")
                .WithCustomHive()
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute();

            var command3 = new DotnetNewCommand(_log, commandName, "Microsoft.DotNet.Common.ProjectTemplates.5.0::*")
                .WithCustomHive()
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute();

            foreach (var commandResult in new[] { command1, command2, command3 })
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

            Assert.True(command1.StdOut.Equals(command2.StdOut));
            Assert.True(command1.StdOut.Equals(command3.StdOut));
        }

        [Theory]
        [InlineData("-i")]
        [InlineData("install")]
        public void CanInstallRemoteNuGetPackageWithVersion(string commandName)
        {
            new DotnetNewCommand(_log, commandName, "Microsoft.DotNet.Web.ProjectTemplates.5.0::5.0.0")
                .WithCustomHive()
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
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

        [Fact]
        public void CanInstallRemoteNuGetPackageWithPrereleaseVersion()
        {
            new DotnetNewCommand(_log, "-i", "Microsoft.Azure.WebJobs.ProjectTemplates::4.0.1844-preview1", "--nuget-source", "https://api.nuget.org/v3/index.json")
                .WithCustomHive()
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
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
            var home = TestUtils.CreateTemporaryFolder("Home");
            new DotnetNewCommand(_log, commandName, "Take.Blip.Client.Templates", "--nuget-source", "https://api.nuget.org/v3/index.json")
                .WithCustomHive(home)
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
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
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
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
            using var packageManager = new PackageManager();
            string packageLocation = packageManager.PackTestTemplatesNuGetPackage();

            new DotnetNewCommand(_log, commandName, packageLocation)
                .WithCustomHive()
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
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
                .WithCustomHive().WithDebug()
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
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
            string basicFSharp = TestUtils.GetTestTemplateLocation("TemplateResolution/DifferentLanguagesGroup/BasicFSharp");
            new DotnetNewCommand(_log, commandName, basicFSharp)
                .WithCustomHive()
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
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
            var home = TestUtils.CreateTemporaryFolder("Home");

            new DotnetNewCommand(_log, commandName, "Microsoft.DotNet.Common.ProjectTemplates.5.0")
               .WithCustomHive(home)
               .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
               .Execute()
               .Should()
               .ExitWith(0)
               .And
               .NotHaveStdErr()
               .And.HaveStdOutContaining("console")
               .And.HaveStdOutContaining("Console App");

            new DotnetNewCommand(_log, commandName, "Microsoft.DotNet.Web.ProjectTemplates.5.0")
                .WithCustomHive(home)
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
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
            var home = TestUtils.CreateTemporaryFolder("Home");

            new DotnetNewCommand(_log, commandName, "BlaBlaBla")
               .WithCustomHive(home)
               .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
               .Execute()
               .Should().Fail()
               .And.HaveStdErrContaining("BlaBlaBla could not be installed, the package does not exist");
        }

        [Theory]
        [InlineData("-i")]
        [InlineData("install")]
        public void CannotInstallRemotePackageWithIncorrectVersion(string commandName)
        {
            var home = TestUtils.CreateTemporaryFolder("Home");

            new DotnetNewCommand(_log, commandName, "Microsoft.DotNet.Web.ProjectTemplates.5.0::16.0.0")
               .WithCustomHive(home)
               .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
               .Execute()
               .Should().Fail()
               .And.HaveStdErrContaining("Microsoft.DotNet.Web.ProjectTemplates.5.0::16.0.0 could not be installed, the package does not exist");
        }

        [Fact]
        public void CanInstallSeveralSources()
        {
            var home = TestUtils.CreateTemporaryFolder("Home");
            string basicFSharp = TestUtils.GetTestTemplateLocation("TemplateResolution/DifferentLanguagesGroup/BasicFSharp");
            string basicVB = TestUtils.GetTestTemplateLocation("TemplateResolution/DifferentLanguagesGroup/BasicVB");

            new DotnetNewCommand(_log, "-i", "Microsoft.DotNet.Web.ProjectTemplates.5.0", "-i", "Microsoft.DotNet.Common.ProjectTemplates.5.0", "-i", basicFSharp, "-i", basicVB)
                .WithCustomHive(home).WithDebug()
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
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
            var home = TestUtils.CreateTemporaryFolder("Home");
            string basicFSharp = TestUtils.GetTestTemplateLocation("TemplateResolution/DifferentLanguagesGroup/BasicFSharp");
            string basicVB = TestUtils.GetTestTemplateLocation("TemplateResolution/DifferentLanguagesGroup/BasicVB");

            new DotnetNewCommand(_log, "install", "Microsoft.DotNet.Web.ProjectTemplates.5.0",  "Microsoft.DotNet.Common.ProjectTemplates.5.0", basicFSharp, basicVB)
                .WithCustomHive(home).WithDebug()
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
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
        public void CannotInstallSameSourceTwice_NuGet()
        {
            var home = TestUtils.CreateTemporaryFolder("Home");

            new DotnetNewCommand(_log, "-i", "Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.0")
                .WithCustomHive(home)
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutContaining("console")
                .And.HaveStdOutContaining("classlib");

            new DotnetNewCommand(_log, "-i", "Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.0")
                 .WithCustomHive(home)
                 .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                 .Execute()
                 .Should().Fail()
                 .And.HaveStdErrContaining("Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.0 is already installed");
        }

        [Fact]
        public void CannotInstallSameSourceTwice_Folder()
        {
            var home = TestUtils.CreateTemporaryFolder("Home");
            string basicFSharp = TestUtils.GetTestTemplateLocation("TemplateResolution/DifferentLanguagesGroup/BasicFSharp");
            new DotnetNewCommand(_log, "-i", basicFSharp)
                .WithCustomHive(home)
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutContaining("basic");

            new DotnetNewCommand(_log, "-i", basicFSharp)
                 .WithCustomHive(home)
                 .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                 .Execute()
                 .Should().Fail()
                 .And.HaveStdErrContaining($"{basicFSharp} is already installed");
        }

        [Theory]
        [InlineData("-i", "-u")]
        [InlineData("install", "uninstall")]
        public void CanUpdateSameSource_NuGet(string installCommandName, string uninstallCommandName)
        {
            var home = TestUtils.CreateTemporaryFolder("Home");

            new DotnetNewCommand(_log, installCommandName, "Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.0")
                .WithCustomHive(home)
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutContaining("console")
                .And.HaveStdOutContaining("classlib");

            new DotnetNewCommand(_log, uninstallCommandName)
                 .WithCustomHive(home)
                 .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                 .Execute()
                 .Should().ExitWith(0)
                 .And.NotHaveStdErr()
                 .And.HaveStdOutContaining("Microsoft.DotNet.Common.ProjectTemplates.5.0")
                 .And.HaveStdOutContaining("Version: 5.0.0")
                 .And.NotHaveStdOutContaining("Version: 5.0.1");

            Assert.True(File.Exists(Path.Combine(home, "packages", "Microsoft.DotNet.Common.ProjectTemplates.5.0.5.0.0.nupkg")));

            new DotnetNewCommand(_log, installCommandName, "Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.1")
                 .WithCustomHive(home)
                 .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                 .Execute()
                 .Should().ExitWith(0)
                 .And.NotHaveStdErr()
                 .And.HaveStdOutContaining("The following template packages will be installed:")
                 .And.HaveStdOutContaining("Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.1")
                 .And.HaveStdOutContaining("Microsoft.DotNet.Common.ProjectTemplates.5.0 is already installed, version: 5.0.0, it will be replaced with version 5.0.1")
                 .And.HaveStdOutContaining("Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.0 was successfully uninstalled")
                 .And.HaveStdOutContaining($"Success: Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.1 installed the following templates:")
                 .And.HaveStdOutContaining("console")
                 .And.HaveStdOutContaining("classlib");

            new DotnetNewCommand(_log, uninstallCommandName)
                 .WithCustomHive(home)
                 .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
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
            var home = TestUtils.CreateTemporaryFolder("Home");

            using var packageManager = new PackageManager();
            string packageLocation = await packageManager.GetNuGetPackage(
                "Microsoft.DotNet.Common.ProjectTemplates.5.0",
                minimumVersion: new NuGet.Versioning.NuGetVersion(6, 0, 0),
                logger: new XunitNuGetLogger(_messageSink)).ConfigureAwait(false);

            _messageSink.OnMessage(new DiagnosticMessage($"{nameof(InstallingSamePackageFromRemoteUpdatesLocal)}: Microsoft.DotNet.Common.ProjectTemplates.5.0 is downloaded to {packageLocation}.)"));

            new DotnetNewCommand(_log, installCommandName, packageLocation)
                .WithCustomHive(home)
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute()
                .Should().ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutMatching($"Success: Microsoft\\.DotNet\\.Common\\.ProjectTemplates\\.5\\.0::([\\d\\.a-z-])+ installed the following templates:")
                .And.HaveStdOutContaining("console")
                .And.HaveStdOutContaining("classlib");

            new DotnetNewCommand(_log, uninstallCommandName)
                 .WithCustomHive(home)
                 .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                 .Execute()
                 .Should().ExitWith(0)
                 .And.NotHaveStdErr()
                 .And.HaveStdOutContaining("Microsoft.DotNet.Common.ProjectTemplates.5.0")
                 .And.HaveStdOutContaining("Author: Microsoft")
                 .And.HaveStdOutContaining("Version:")
                 .And.NotHaveStdOutContaining("Version: 5.0.0");

            new DotnetNewCommand(_log, installCommandName, "Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.0")
                .WithCustomHive(home)
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute()
                .Should().ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("The following template packages will be installed:")
                .And.HaveStdOutContaining("Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.0")
                .And.HaveStdOutMatching("Microsoft\\.DotNet\\.Common\\.ProjectTemplates\\.5\\.0 is already installed, version: ([\\d\\.a-z-])+, it will be replaced with version 5\\.0\\.0")
                .And.HaveStdOutMatching("Microsoft\\.DotNet\\.Common\\.ProjectTemplates\\.5\\.0::([\\d\\.a-z-])+ was successfully uninstalled")
                .And.HaveStdOutContaining($"Success: Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.0 installed the following templates:")
                .And.HaveStdOutContaining("console")
                .And.HaveStdOutContaining("classlib");

            new DotnetNewCommand(_log, uninstallCommandName)
                .WithCustomHive(home)
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
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
            var home = TestUtils.CreateTemporaryFolder("Home");
            string testTemplateLocation = Path.Combine("..", "..", "..", "..", "..", "test", "Microsoft.TemplateEngine.TestTemplates", "test_templates");
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
            var home = TestUtils.CreateTemporaryFolder("Home");
            string codebase = typeof(Program).GetTypeInfo().Assembly.Location;
            new DotnetNewCommand(_log, commandName, codebase)
                .WithCustomHive(home)
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute()
                .Should().Fail()
                .And.HaveStdErrContaining($"{codebase} is not supported");
        }

        [Fact]
        public void ReinstallDoesntRemoveTemplates()
        {
            var home = TestUtils.CreateTemporaryFolder("Home");
            using var packageManager = new PackageManager();
            string packageLocation = packageManager.PackTestTemplatesNuGetPackage();

            new DotnetNewCommand(_log, "-i", packageLocation)
                .WithCustomHive()
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute()
                .Should().ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("The following template packages will be installed:")
                .And.HaveStdOutMatching($"Success: Microsoft\\.TemplateEngine\\.TestTemplates::([\\d\\.a-z-])+ installed the following templates:")
                .And.HaveStdOutContaining("TestAssets.TemplateWithTags")
                .And.HaveStdOutContaining("TestAssets.ConfigurationKitchenSink");

            new DotnetNewCommand(_log, "-i", packageLocation)
                .WithCustomHive()
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
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
            var home = TestUtils.CreateTemporaryFolder("Home");
            string invalidTemplatePath = TestUtils.GetTestTemplateLocation("Invalid/MissingMandatoryConfig");
            new DotnetNewCommand(_log, "-i", invalidTemplatePath)
                .WithCustomHive(home)
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining($"Error: Failed to load template from {invalidTemplatePath}")
                .And.HaveStdOutContaining($"  Missing 'identity'.")
                .And.HaveStdOutContaining($"  Missing 'name'.")
                .And.HaveStdOutContaining($"  Missing 'shortName'.")
                .And.HaveStdOutContaining($"  The template root is outside the specified install source location.")
                .And.HaveStdOutContaining($"  Source location './' is outside the specified install source location.")
                .And.HaveStdOutContaining($"No templates were found in the package {invalidTemplatePath}.");
        }

        [Fact]
        public void CanShowWarning_WhenHostDataIsIncorrect()
        {
            string home = TestUtils.CreateTemporaryFolder("Home");
            string invalidTemplatePath = TestUtils.GetTestTemplateLocation("Invalid/InvalidHostData");
            new DotnetNewCommand(_log, "-i", invalidTemplatePath)
                .WithCustomHive(home)
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
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
