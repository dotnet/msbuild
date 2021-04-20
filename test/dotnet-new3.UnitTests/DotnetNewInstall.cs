// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.TestFramework.Assertions;
using Microsoft.TemplateEngine.TestHelper;
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace dotnet_new3.IntegrationTests
{
    public class DotnetNewInstallTests
    {
        private readonly ITestOutputHelper _log;

        public DotnetNewInstallTests(ITestOutputHelper log)
        {
            _log = log;
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
                .And.HaveStdOutContaining("Microsoft.DotNet.Web.ProjectTemplates.5.0, version: 5.0.0")
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
                 .And.HaveStdOutContaining("Microsoft.DotNet.Common.ProjectTemplates.5.0, version: 5.0.1")
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
            var home = TestUtils.CreateTemporaryFolder("Home");

            using var packageManager = new PackageManager();
            string packageLocation = await packageManager.GetNuGetPackage("Microsoft.DotNet.Common.ProjectTemplates.5.0", _log).ConfigureAwait(false);

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
                .And.HaveStdOutContaining("Microsoft.DotNet.Common.ProjectTemplates.5.0, version: 5.0.0")
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
        }

        [Fact]
        public void CanExpandWhenInstall()
        {
            var home = TestUtils.CreateTemporaryFolder("Home");
            var outputFolder = TestUtils.CreateTemporaryFolder();

            string codebase = typeof(Program).GetTypeInfo().Assembly.Location;
            Uri cb = new Uri(codebase);
            string asmPath = cb.LocalPath;
            string dir = Path.GetDirectoryName(asmPath);
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
    }
}
