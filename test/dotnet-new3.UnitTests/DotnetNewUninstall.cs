// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;
using Xunit.Abstractions;

namespace Dotnet_new3.IntegrationTests
{
    public class DotnetNewUninstall
    {
        private readonly ITestOutputHelper _log;

        public DotnetNewUninstall(ITestOutputHelper log)
        {
            _log = log;
        }

        [Fact]
        public void CanListInstalledSources_Folder()
        {
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            Helpers.InstallTestTemplate("TemplateResolution/DifferentLanguagesGroup/BasicFSharp", _log, workingDirectory, home);

            new DotnetNewCommand(_log, "-u")
                .WithCustomHive(home)
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutContaining($"TemplateResolution{Path.DirectorySeparatorChar}DifferentLanguagesGroup{Path.DirectorySeparatorChar}BasicFSharp")
                .And.HaveStdOutMatching($"^\\s*dotnet new3 --uninstall .*TemplateResolution{Regex.Escape(Path.DirectorySeparatorChar.ToString())}DifferentLanguagesGroup{Regex.Escape(Path.DirectorySeparatorChar.ToString())}BasicFSharp$", RegexOptions.Multiline);
        }

        [Fact]
        public void CanListInstalledSources_NuGet()
        {
            var home = TestUtils.CreateTemporaryFolder("Home");
            new DotnetNewCommand(_log, "-i", "Microsoft.DotNet.Web.ProjectTemplates.5.0::5.0.0")
                .WithCustomHive(home)
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.NotHaveStdOutContaining("Determining projects to restore...")
                .And.HaveStdOutContaining("web")
                .And.HaveStdOutContaining("blazorwasm");

            new DotnetNewCommand(_log, "-u")
                .WithCustomHive(home)
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutContaining("Microsoft.DotNet.Web.ProjectTemplates.5.0")
                .And.HaveStdOutContaining("Version: 5.0.0")
                .And.HaveStdOutContaining("Author: Microsoft")
                .And.HaveStdOutMatching("NuGetSource: [0-9.\\-A-Za-z]+")
                .And.HaveStdOutContaining("dotnet new3 --uninstall Microsoft.DotNet.Web.ProjectTemplates.5.0");
        }

        [Fact]
        public void CanListInstalledSources_WhenNothingIsInstalled()
        {
            var home = TestUtils.CreateTemporaryFolder("Home");
            new DotnetNewCommand(_log, "-u")
                .WithCustomHive(home)
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOut($"Currently installed items:{Environment.NewLine}(No Items)");
        }

        [Fact]
        public void CanUninstall_Folder()
        {
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            string templateLocation = Helpers.InstallTestTemplate("TemplateResolution/DifferentLanguagesGroup/BasicFSharp", _log, workingDirectory, home);

            new DotnetNewCommand(_log, "-u")
                .WithCustomHive(home)
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutContaining($"TemplateResolution{Path.DirectorySeparatorChar}DifferentLanguagesGroup{Path.DirectorySeparatorChar}BasicFSharp")
                .And.HaveStdOutMatching($"^\\s*dotnet new3 --uninstall .*TemplateResolution{Regex.Escape(Path.DirectorySeparatorChar.ToString())}DifferentLanguagesGroup{Regex.Escape(Path.DirectorySeparatorChar.ToString())}BasicFSharp$", RegexOptions.Multiline);

            new DotnetNewCommand(_log, "-u", templateLocation)
                .WithCustomHive(home)
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOut($"Success: {templateLocation} was uninstalled.");

            new DotnetNewCommand(_log, "-u")
                .WithCustomHive(home)
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOut($"Currently installed items:{Environment.NewLine}(No Items)");

            Assert.True(Directory.Exists(templateLocation));
        }

        [Fact]
        public void CanUninstall_NuGet()
        {
            var home = TestUtils.CreateTemporaryFolder("Home");
            new DotnetNewCommand(_log, "-i", "Microsoft.DotNet.Web.ProjectTemplates.5.0::5.0.0")
                .WithCustomHive(home)
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.NotHaveStdOutContaining("Determining projects to restore...")
                .And.HaveStdOutContaining("web")
                .And.HaveStdOutContaining("blazorwasm");

            new DotnetNewCommand(_log, "-u")
                .WithCustomHive(home)
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutContaining("Microsoft.DotNet.Web.ProjectTemplates.5.0")
                .And.HaveStdOutContaining("Version: 5.0.0")
                .And.HaveStdOutContaining("Author: Microsoft")
                .And.HaveStdOutContaining("dotnet new3 --uninstall Microsoft.DotNet.Web.ProjectTemplates.5.0");

            Assert.True(File.Exists(Path.Combine(home, "packages", "Microsoft.DotNet.Web.ProjectTemplates.5.0.5.0.0.nupkg")));

            new DotnetNewCommand(_log, "-u", "Microsoft.DotNet.Web.ProjectTemplates.5.0")
                .WithCustomHive(home)
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOut($"Success: Microsoft.DotNet.Web.ProjectTemplates.5.0::5.0.0 was uninstalled.");

            new DotnetNewCommand(_log, "-u")
                .WithCustomHive(home)
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOut($"Currently installed items:{Environment.NewLine}(No Items)");

            Assert.False(File.Exists(Path.Combine(home, "packages", "Microsoft.DotNet.Web.ProjectTemplates.5.0.5.0.0.nupkg")));
        }

        [Fact]
        public void CanUninstallSeveralSources()
        {
            var home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            string basicFSharp = Helpers.InstallTestTemplate("TemplateResolution/DifferentLanguagesGroup/BasicFSharp", _log, workingDirectory, home);
            string basicVB = Helpers.InstallTestTemplate("TemplateResolution/DifferentLanguagesGroup/BasicVB", _log, workingDirectory, home);

            new DotnetNewCommand(_log, "-i", "Microsoft.DotNet.Web.ProjectTemplates.5.0", "-i", "Microsoft.DotNet.Common.ProjectTemplates.5.0")
                .WithCustomHive(home).WithDebug()
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutContaining("web")
                .And.HaveStdOutContaining("blazorwasm")
                .And.HaveStdOutContaining("console")
                .And.HaveStdOutContaining("classlib");

            new DotnetNewCommand(_log, "-u", "Microsoft.DotNet.Common.ProjectTemplates.5.0", "-u", basicFSharp)
                .WithCustomHive(home)
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutMatching($"^Success: Microsoft\\.DotNet\\.Common\\.ProjectTemplates\\.5\\.0::([\\d\\.a-z-])+ was uninstalled\\.\\s*$", RegexOptions.Multiline)
                .And.HaveStdOutContaining($"Success: {basicFSharp} was uninstalled.");

            new DotnetNewCommand(_log, "-u")
                .WithCustomHive(home)
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutContaining("Microsoft.DotNet.Web.ProjectTemplates.5.0")
                .And.HaveStdOutContaining(basicVB)
                .And.NotHaveStdOutContaining("Microsoft.DotNet.Common.ProjectTemplates.5.0")
                .And.NotHaveStdOutContaining(basicFSharp);
        }

        [Fact]
        public void CannotUninstallUnknownPackage()
        {
            var home = TestUtils.CreateTemporaryFolder("Home");
            new DotnetNewCommand(_log, "-i", "Microsoft.DotNet.Web.ProjectTemplates.5.0::5.0.0")
                .WithCustomHive(home)
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.NotHaveStdOutContaining("Determining projects to restore...")
                .And.HaveStdOutContaining("web")
                .And.HaveStdOutContaining("blazorwasm");

            new DotnetNewCommand(_log, "-u", "Microsoft.DotNet.Common.ProjectTemplates.5.0")
                .WithCustomHive(home)
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute()
                .Should().Fail()
                .And.HaveStdErrContaining("The template package 'Microsoft.DotNet.Common.ProjectTemplates.5.0' is not found.")
                .And.HaveStdErrContaining("To list installed template packages use:")
                .And.HaveStdErrContaining("   dotnet new3 --uninstall");
        }

        [Fact]
        public void CannotUninstallByTemplateName()
        {
            var home = TestUtils.CreateTemporaryFolder("Home");
            new DotnetNewCommand(_log, "-i", "Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.0")
                .WithCustomHive(home)
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr();

            new DotnetNewCommand(_log, "-u", "console")
                .WithCustomHive(home)
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute()
                .Should().Fail()
                .And.HaveStdErrContaining("The template package 'console' is not found")
                .And.HaveStdErrContaining("The template 'console' is included to the packages:")
                .And.HaveStdErrContaining("   Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.0 (contains 2 templates)")
                //                .And.HaveStdErrContaining("To list the templates installed in a package, use dotnet new3 <new option> <package name>")
                .And.HaveStdErrContaining("To uninstall the template package use:")
                .And.HaveStdErrContaining("   dotnet new3 --uninstall Microsoft.DotNet.Common.ProjectTemplates.5.0");
        }

        [Fact]
        public void CannotUninstallByTemplateName_ShowsAllPackages()
        {
            var home = TestUtils.CreateTemporaryFolder("Home");
            new DotnetNewCommand(_log, "-i", "Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.0")
                .WithCustomHive(home).WithoutBuiltInTemplates()
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr();

            new DotnetNewCommand(_log, "-i", "Microsoft.DotNet.Common.ProjectTemplates.3.1::5.0.0")
                .WithCustomHive(home).WithoutBuiltInTemplates()
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr();

            new DotnetNewCommand(_log, "-u", "console")
                .WithCustomHive(home).WithoutBuiltInTemplates()
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute()
                .Should().Fail()
                .And.HaveStdErrContaining("The template package 'console' is not found")
                .And.HaveStdErrContaining("The template 'console' is included to the packages:")
                .And.HaveStdErrContaining("   Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.0 (contains 2 templates)")
                .And.HaveStdErrContaining("   Microsoft.DotNet.Common.ProjectTemplates.3.1::5.0.0 (contains 2 templates)")
                .And.HaveStdErrContaining("To uninstall the template package use:")
                .And.HaveStdErrContaining("   dotnet new3 --uninstall Microsoft.DotNet.Common.ProjectTemplates.");
        }
    }
}
