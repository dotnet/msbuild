// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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

        [Fact]
        public void CanInstallRemoteNuGetPackage()
        {
            new DotnetNewCommand(_log, "-i", "Microsoft.DotNet.Web.ProjectTemplates.5.0", "--quiet")
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

        [Fact]
        public void CanInstallRemoteNuGetPackage_LatestVariations()
        {
            var command1 = new DotnetNewCommand(_log, "-i", "Microsoft.DotNet.Common.ProjectTemplates.5.0", "--quiet")
                .WithCustomHive()
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute();

            var command2 = new DotnetNewCommand(_log, "-i", "Microsoft.DotNet.Common.ProjectTemplates.5.0::", "--quiet")
                .WithCustomHive()
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute();

            var command3 = new DotnetNewCommand(_log, "-i", "Microsoft.DotNet.Common.ProjectTemplates.5.0::*", "--quiet")
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

        [Fact]
        public void CanInstallRemoteNuGetPackageWithVersion()
        {
            new DotnetNewCommand(_log, "-i", "Microsoft.DotNet.Web.ProjectTemplates.5.0::5.0.0", "--quiet")
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
            new DotnetNewCommand(_log, "-i", "Take.Blip.Client.Templates::0.6.37-beta", "--quiet", "--nuget-source", "https://api.nuget.org/v3/index.json")
                .WithCustomHive()
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutContaining("The following template packages will be installed:")
                .And.HaveStdOutMatching($"Success: Take.Blip.Client.Templates::0.6.37-beta installed the following templates:")
                .And.HaveStdOutContaining("blip-console");
        }

        [Fact]
        public void CanInstallRemoteNuGetPackageWithNuGetSource()
        {
            var home = TestUtils.CreateTemporaryFolder("Home");
            new DotnetNewCommand(_log, "-i", "Take.Blip.Client.Templates", "--quiet", "--nuget-source", "https://api.nuget.org/v3/index.json")
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

            new DotnetNewCommand(_log, "-i", "Take.Blip.Client.Templates", "--quiet", "--add-source", "https://api.nuget.org/v3/index.json")
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

        [Fact]
        public void CanInstallLocalNuGetPackage()
        {
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
        }

        [Fact]
        public void CanPrintDebugOutputWhenInstalling()
        {
            new DotnetNewCommand(_log, "-i", "Microsoft.DotNet.Web.ProjectTemplates.5.0", "--quiet")
                .WithCustomHive()
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .WithEnvironmentVariable("DOTNET_CLI_CONTEXT_VERBOSE", "true")
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

        [Fact]
        public void CanInstallLocalFolder()
        {
            string basicFSharp = TestUtils.GetTestTemplateLocation("TemplateResolution/DifferentLanguagesGroup/BasicFSharp");
            new DotnetNewCommand(_log, "-i", basicFSharp)
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

        [Fact]
        public void PrintOnlyNewlyInstalledTemplates()
        {
            var home = TestUtils.CreateTemporaryFolder("Home");

            new DotnetNewCommand(_log, "-i", "Microsoft.DotNet.Common.ProjectTemplates.5.0", "--quiet")
               .WithCustomHive(home)
               .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
               .Execute()
               .Should()
               .ExitWith(0)
               .And
               .NotHaveStdErr()
               .And.HaveStdOutContaining("console")
               .And.HaveStdOutContaining("Console Application");

            new DotnetNewCommand(_log, "-i", "Microsoft.DotNet.Web.ProjectTemplates.5.0", "--quiet")
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

        [Fact]
        public void CannotInstallUnknownRemotePackage()
        {
            var home = TestUtils.CreateTemporaryFolder("Home");

            new DotnetNewCommand(_log, "-i", "BlaBlaBla", "--quiet")
               .WithCustomHive(home)
               .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
               .Execute()
               .Should().Fail()
               .And.HaveStdErrContaining("BlaBlaBla could not be installed, the package does not exist");
        }

        [Fact]
        public void CannotInstallRemotePackageWithIncorrectVersion()
        {
            var home = TestUtils.CreateTemporaryFolder("Home");

            new DotnetNewCommand(_log, "-i", "Microsoft.DotNet.Web.ProjectTemplates.5.0::16.0.0", "--quiet")
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

            new DotnetNewCommand(_log, "-i", "Microsoft.DotNet.Web.ProjectTemplates.5.0", "-i", "Microsoft.DotNet.Common.ProjectTemplates.5.0", "-i", basicFSharp, "-i", basicVB, "--quiet")
                .WithCustomHive(home)
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

        [Fact]
        public void CanUpdateSameSource_NuGet()
        {
            var home = TestUtils.CreateTemporaryFolder("Home");

            new DotnetNewCommand(_log, "-i", "Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.0", "--quiet")
                .WithCustomHive(home)
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutContaining("console")
                .And.HaveStdOutContaining("classlib");

            new DotnetNewCommand(_log, "-u")
                 .WithCustomHive(home)
                 .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                 .Execute()
                 .Should().ExitWith(0)
                 .And.NotHaveStdErr()
                 .And.HaveStdOutContaining("Microsoft.DotNet.Common.ProjectTemplates.5.0")
                 .And.HaveStdOutContaining("Version: 5.0.0")
                 .And.NotHaveStdOutContaining("Version: 5.0.1");

            Assert.True(File.Exists(Path.Combine(home, "packages", "Microsoft.DotNet.Common.ProjectTemplates.5.0.5.0.0.nupkg")));

            new DotnetNewCommand(_log, "-i", "Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.1")
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

            new DotnetNewCommand(_log, "-u")
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

        [Fact]
        public async Task InstallingSamePackageFromRemoteUpdatesLocal()
        {
            _messageSink.OnMessage(new DiagnosticMessage($"{nameof(InstallingSamePackageFromRemoteUpdatesLocal)} started."));
            var home = TestUtils.CreateTemporaryFolder("Home");

            using var packageManager = new PackageManager();
            string packageLocation = await packageManager.GetNuGetPackage(
                "Microsoft.DotNet.Common.ProjectTemplates.5.0",
                minimumVersion: new NuGet.Versioning.NuGetVersion(6, 0, 0),
                logger: new XunitNuGetLogger(_messageSink)).ConfigureAwait(false);

            _messageSink.OnMessage(new DiagnosticMessage($"{nameof(InstallingSamePackageFromRemoteUpdatesLocal)}: Microsoft.DotNet.Common.ProjectTemplates.5.0 is downloaded to {packageLocation}.)"));

            new DotnetNewCommand(_log, "-i", packageLocation)
                .WithCustomHive(home)
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute()
                .Should().ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutMatching($"Success: Microsoft\\.DotNet\\.Common\\.ProjectTemplates\\.5\\.0::([\\d\\.a-z-])+ installed the following templates:")
                .And.HaveStdOutContaining("console")
                .And.HaveStdOutContaining("classlib");

            new DotnetNewCommand(_log, "-u")
                 .WithCustomHive(home)
                 .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                 .Execute()
                 .Should().ExitWith(0)
                 .And.NotHaveStdErr()
                 .And.HaveStdOutContaining("Microsoft.DotNet.Common.ProjectTemplates.5.0")
                 .And.HaveStdOutContaining("Author: Microsoft")
                 .And.HaveStdOutContaining("Version:")
                 .And.NotHaveStdOutContaining("Version: 5.0.0");

            new DotnetNewCommand(_log, "-i", "Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.0")
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

            new DotnetNewCommand(_log, "-u")
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

        [Fact]
        public void CanExpandWhenInstall()
        {
            var home = TestUtils.CreateTemporaryFolder("Home");
            string codebase = typeof(Program).GetTypeInfo().Assembly.Location;
            string dir = Path.GetDirectoryName(codebase) ?? throw new System.Exception("Invalid assembly location");
            string testTemplateLocation = Path.Combine(dir, "..", "..", "..", "..", "..", "test", "Microsoft.TemplateEngine.TestTemplates", "test_templates");
            string testTemplateLocationAbsolute = Path.GetFullPath(testTemplateLocation);
            string pattern = testTemplateLocation + Path.DirectorySeparatorChar + "*";

            new DotnetNewCommand(_log, "-i", pattern)
                .WithCustomHive(home)
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
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

        [Fact]
        public void CannotInstallInvalidPackage()
        {
            var home = TestUtils.CreateTemporaryFolder("Home");
            string codebase = typeof(Program).GetTypeInfo().Assembly.Location;
            new DotnetNewCommand(_log, "-i", codebase)
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
    }
}
