// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    public partial class DotnetNewUninstallTests : BaseIntegrationTest
    {
        private readonly ITestOutputHelper _log;

        public DotnetNewUninstallTests(ITestOutputHelper log) : base(log)
        {
            _log = log;
        }

        [Theory]
        [InlineData("-u")]
        [InlineData("--uninstall")]
        [InlineData("uninstall")]
        public void CanListInstalledSources_Folder(string commandName)
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();
            string testTemplate = InstallTestTemplate("TemplateResolution/DifferentLanguagesGroup/BasicFSharp", _log, home, workingDirectory);

            new DotnetNewCommand(_log, commandName)
                .WithCustomHive(home)
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutContaining($"TemplateResolution{Path.DirectorySeparatorChar}DifferentLanguagesGroup{Path.DirectorySeparatorChar}BasicFSharp")
                .And.HaveStdOutContaining($"         dotnet new uninstall {testTemplate}");
        }

        [Theory]
        [InlineData("-u")]
        [InlineData("--uninstall")]
        [InlineData("uninstall")]
        public void CanListInstalledSources_NuGet(string commandName)
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            new DotnetNewCommand(_log, "-i", "Microsoft.DotNet.Web.ProjectTemplates.5.0::5.0.0")
                .WithCustomHive(home)
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.NotHaveStdOutContaining("Determining projects to restore...")
                .And.HaveStdOutContaining("web")
                .And.HaveStdOutContaining("blazorwasm");

            new DotnetNewCommand(_log, commandName)
                .WithCustomHive(home)
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutContaining("Microsoft.DotNet.Web.ProjectTemplates.5.0")
                .And.HaveStdOutContaining("Version: 5.0.0")
                .And.HaveStdOutContaining("Author: Microsoft")
                .And.HaveStdOutMatching("NuGetSource: [0-9.\\-A-Za-z]+")
                .And.HaveStdOutContaining("         dotnet new uninstall Microsoft.DotNet.Web.ProjectTemplates.5.0");
        }

        [Theory]
        [InlineData("-u")]
        [InlineData("uninstall")]
        public void CanListInstalledSources_WhenNothingIsInstalled(string commandName)
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            new DotnetNewCommand(_log, commandName)
                .WithCustomHive(home)
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutContaining($"Currently installed items:{Environment.NewLine}(No Items)");
        }

        [Theory]
        [InlineData("-u")]
        [InlineData("uninstall")]
        [InlineData("--uninstall")]
        public void CanUninstall_Folder(string commandName)
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();
            string templateLocation = InstallTestTemplate("TemplateResolution/DifferentLanguagesGroup/BasicFSharp", _log, home, workingDirectory);

            new DotnetNewCommand(_log, commandName)
                .WithCustomHive(home)
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutContaining($"TemplateResolution{Path.DirectorySeparatorChar}DifferentLanguagesGroup{Path.DirectorySeparatorChar}BasicFSharp")
                .And.HaveStdOutMatching($"^\\s*dotnet new uninstall .*TemplateResolution{Regex.Escape(Path.DirectorySeparatorChar.ToString())}DifferentLanguagesGroup{Regex.Escape(Path.DirectorySeparatorChar.ToString())}BasicFSharp$", RegexOptions.Multiline);

            new DotnetNewCommand(_log, commandName, templateLocation)
                .WithCustomHive(home)
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutContaining($"Success: {templateLocation} was uninstalled.");

            new DotnetNewCommand(_log, commandName)
                .WithCustomHive(home)
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutContaining($"Currently installed items:{Environment.NewLine}(No Items)");

            Assert.True(Directory.Exists(templateLocation));
        }

        [Theory]
        [InlineData("-u")]
        [InlineData("uninstall")]
        [InlineData("--uninstall")]
        public void CanUninstall_NuGet(string commandName)
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            new DotnetNewCommand(_log, "-i", "Microsoft.DotNet.Web.ProjectTemplates.5.0::5.0.0")
                .WithCustomHive(home)
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.NotHaveStdOutContaining("Determining projects to restore...")
                .And.HaveStdOutContaining("web")
                .And.HaveStdOutContaining("blazorwasm");

            new DotnetNewCommand(_log, commandName)
                .WithCustomHive(home)
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutContaining("Microsoft.DotNet.Web.ProjectTemplates.5.0")
                .And.HaveStdOutContaining("Version: 5.0.0")
                .And.HaveStdOutContaining("Author: Microsoft")
                .And.HaveStdOutContaining("dotnet new uninstall Microsoft.DotNet.Web.ProjectTemplates.5.0");

            Assert.True(File.Exists(Path.Combine(home, "packages", "Microsoft.DotNet.Web.ProjectTemplates.5.0.5.0.0.nupkg")));

            // This tests proper uninstallation of package even if there is a clash with existing folder name
            //  (this used to fail - see #4613)
            string packageNameToUnisntall = "Microsoft.DotNet.Web.ProjectTemplates.5.0";
            string workingDir = CreateTemporaryFolder();
            Directory.CreateDirectory(Path.Combine(workingDir, packageNameToUnisntall));

            new DotnetNewCommand(_log, commandName, packageNameToUnisntall)
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDir)
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutContaining($"Success: Microsoft.DotNet.Web.ProjectTemplates.5.0::5.0.0 was uninstalled.");

            new DotnetNewCommand(_log, commandName)
                .WithCustomHive(home)
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutContaining($"Currently installed items:{Environment.NewLine}(No Items)");

            Assert.False(File.Exists(Path.Combine(home, "packages", "Microsoft.DotNet.Web.ProjectTemplates.5.0.5.0.0.nupkg")));
        }

        [Fact]
        public void CanUninstallSeveralSources_LegacySyntax()
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();
            string basicFSharp = InstallTestTemplate("TemplateResolution/DifferentLanguagesGroup/BasicFSharp", _log, home, workingDirectory);
            string basicVB = InstallTestTemplate("TemplateResolution/DifferentLanguagesGroup/BasicVB", _log, home, workingDirectory);

            new DotnetNewCommand(_log, "-i", "Microsoft.DotNet.Web.ProjectTemplates.5.0", "-i", "Microsoft.DotNet.Common.ProjectTemplates.5.0")
                .WithCustomHive(home).WithDebug()
                .WithWorkingDirectory(CreateTemporaryFolder())
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
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutMatching($"^Success: Microsoft\\.DotNet\\.Common\\.ProjectTemplates\\.5\\.0::([\\d\\.a-z-])+ was uninstalled\\.\\s*$", RegexOptions.Multiline)
                .And.HaveStdOutContaining($"Success: {basicFSharp} was uninstalled.");

            new DotnetNewCommand(_log, "-u")
                .WithCustomHive(home)
                .WithWorkingDirectory(CreateTemporaryFolder())
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
        public void CanUninstallSeveralSources()
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();
            string basicFSharp = InstallTestTemplate("TemplateResolution/DifferentLanguagesGroup/BasicFSharp", _log, home, workingDirectory);
            string basicVB = InstallTestTemplate("TemplateResolution/DifferentLanguagesGroup/BasicVB", _log, home, workingDirectory);

            new DotnetNewCommand(_log, "install", "Microsoft.DotNet.Web.ProjectTemplates.5.0", "Microsoft.DotNet.Common.ProjectTemplates.5.0")
                .WithCustomHive(home).WithDebug()
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutContaining("web")
                .And.HaveStdOutContaining("blazorwasm")
                .And.HaveStdOutContaining("console")
                .And.HaveStdOutContaining("classlib");

            new DotnetNewCommand(_log, "uninstall", "Microsoft.DotNet.Common.ProjectTemplates.5.0", basicFSharp)
                .WithCustomHive(home)
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutMatching($"^Success: Microsoft\\.DotNet\\.Common\\.ProjectTemplates\\.5\\.0::([\\d\\.a-z-])+ was uninstalled\\.\\s*$", RegexOptions.Multiline)
                .And.HaveStdOutContaining($"Success: {basicFSharp} was uninstalled.");

            new DotnetNewCommand(_log, "uninstall")
                .WithCustomHive(home)
                .WithWorkingDirectory(CreateTemporaryFolder())
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

        [Theory]
        [InlineData("-u")]
        [InlineData("uninstall")]
        public void CannotUninstallUnknownPackage(string commandName)
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            new DotnetNewCommand(_log, "install", "Microsoft.DotNet.Web.ProjectTemplates.5.0::5.0.0")
                .WithCustomHive(home)
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.NotHaveStdOutContaining("Determining projects to restore...")
                .And.HaveStdOutContaining("web")
                .And.HaveStdOutContaining("blazorwasm");

            new DotnetNewCommand(_log, commandName, "Microsoft.DotNet.Common.ProjectTemplates.5.0")
                .WithCustomHive(home)
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should().Fail()
                .And.HaveStdErrContaining("The template package 'Microsoft.DotNet.Common.ProjectTemplates.5.0' is not found.")
                .And.HaveStdErrContaining("To list installed template packages use:")
                .And.HaveStdErrContaining("   dotnet new uninstall");
        }

        [Theory]
        [InlineData("-u")]
        [InlineData("uninstall")]
        public void CannotUninstallByTemplateName(string commandName)
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            new DotnetNewCommand(_log, "install", "Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.0")
                .WithCustomHive(home)
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr();

            new DotnetNewCommand(_log, commandName, "console")
                .WithCustomHive(home)
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should().Fail()
                .And.HaveStdErrContaining("The template package 'console' is not found")
                .And.HaveStdErrContaining("The template 'console' is included to the packages:")
                .And.HaveStdErrContaining("   Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.0 (contains 2 templates)")
                //                .And.HaveStdErrContaining("To list the templates installed in a package, use dotnet new <new option> <package name>")
                .And.HaveStdErrContaining("To uninstall the template package use:")
                .And.HaveStdErrContaining("   dotnet new uninstall Microsoft.DotNet.Common.ProjectTemplates.5.0");
        }

        [Theory]
        [InlineData("-u")]
        [InlineData("uninstall")]
        public void CannotUninstallByTemplateName_ShowsAllPackages(string commandName)
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            new DotnetNewCommand(_log, "install", "Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.0")
                .WithCustomHive(home).WithoutBuiltInTemplates()
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr();

            new DotnetNewCommand(_log, "install", "Microsoft.DotNet.Common.ProjectTemplates.3.1::5.0.0")
                .WithCustomHive(home).WithoutBuiltInTemplates()
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr();

            new DotnetNewCommand(_log, commandName, "console")
                .WithCustomHive(home).WithoutBuiltInTemplates()
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should().Fail()
                .And.HaveStdErrContaining("The template package 'console' is not found")
                .And.HaveStdErrContaining("The template 'console' is included to the packages:")
                .And.HaveStdErrContaining("   Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.0 (contains 2 templates)")
                .And.HaveStdErrContaining("   Microsoft.DotNet.Common.ProjectTemplates.3.1::5.0.0 (contains 2 templates)")
                .And.HaveStdErrContaining("To uninstall the template package use:")
                .And.HaveStdErrContaining("   dotnet new uninstall Microsoft.DotNet.Common.ProjectTemplates.");
        }

        [Theory]
        [InlineData("-u")]
        [InlineData("uninstall")]
        public void CanExpandWhenUninstall(string commandName)
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            string testTemplateLocation = GetTestTemplateLocation(string.Empty);
            string testTemplateLocationAbsolute = Path.GetFullPath(testTemplateLocation);
            string pattern = testTemplateLocation + Path.DirectorySeparatorChar + "*";

            new DotnetNewCommand(_log, "install", pattern)
                .WithCustomHive(home).WithoutBuiltInTemplates()
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

            new DotnetNewCommand(_log, commandName)
                .WithCustomHive(home).WithoutBuiltInTemplates()
                .Execute()
                .Should().ExitWith(0)
                .And.NotHaveStdErr()
                .And.NotHaveStdOutContaining("(No Items)")
                .And.HaveStdOutContaining(Path.Combine(testTemplateLocationAbsolute, "ConfigurationKitchenSink"))
                .And.HaveStdOutContaining(Path.Combine(testTemplateLocationAbsolute, "TemplateResolution"))
                .And.HaveStdOutContaining(Path.Combine(testTemplateLocationAbsolute, "TemplateWithSourceName"));

            new DotnetNewCommand(_log, commandName, pattern)
                .WithCustomHive(home).WithoutBuiltInTemplates()
                .Execute()
                .Should().ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining(Path.Combine(testTemplateLocationAbsolute, "ConfigurationKitchenSink"))
                .And.HaveStdOutContaining(Path.Combine(testTemplateLocationAbsolute, "TemplateResolution"))
                .And.HaveStdOutContaining(Path.Combine(testTemplateLocationAbsolute, "TemplateWithSourceName"));

            new DotnetNewCommand(_log, commandName)
                .WithCustomHive(home).WithoutBuiltInTemplates()
                .Execute()
                .Should().ExitWith(0)
                .And.HaveStdOutContaining("(No Items)");
        }

        [Fact]
        public void CanResolveRelativePathOnUninstall()
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            string testTemplateLocation = GetTestTemplateLocation(string.Empty);
            string testTemplateLocationAbsolute = Path.GetFullPath(testTemplateLocation);
            string pattern = testTemplateLocation;

            new DotnetNewCommand(_log, "-i", pattern)
                .WithCustomHive(home).WithoutBuiltInTemplates()
                .Execute()
                .Should().ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("The following template packages will be installed:")
                .And.HaveStdOutContaining("ConfigurationKitchenSink")
                .And.HaveStdOutContaining("TemplateWithSourceName")
                .And.HaveStdOutContaining($"Success: {testTemplateLocationAbsolute} installed the following templates:")
                .And.HaveStdOutContaining("basic")
                .And.HaveStdOutContaining("TestAssets.ConfigurationKitchenSink");

            new DotnetNewCommand(_log, "-u")
                .WithCustomHive(home).WithoutBuiltInTemplates()
                .Execute()
                .Should().ExitWith(0)
                .And.NotHaveStdErr()
                .And.NotHaveStdOutContaining("(No Items)")
                .And.HaveStdOutContaining(testTemplateLocationAbsolute);

            new DotnetNewCommand(_log, "-u", pattern)
                .WithCustomHive(home).WithoutBuiltInTemplates()
                .Execute()
                .Should().ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining(testTemplateLocationAbsolute);

            new DotnetNewCommand(_log, "-u")
                .WithCustomHive(home).WithoutBuiltInTemplates()
                .Execute()
                .Should().ExitWith(0)
                .And.HaveStdOutContaining("(No Items)");
        }

        [Fact]
        public void CanListTemplateInstalledFromFolderWithSpace()
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();

            string testFolderWithSpace = Path.Combine(workingDirectory, "My Test Folder");

            Directory.CreateDirectory(testFolderWithSpace);
            TestUtils.DirectoryCopy(GetTestTemplateLocation("TemplateResolution/DifferentLanguagesGroup/BasicFSharp"), testFolderWithSpace, copySubDirs: true);
            InstallNuGetTemplate(testFolderWithSpace, _log, home, workingDirectory);

            string testFolderWithoutSpace = Path.Combine(workingDirectory, "MyTestFolder");

            Directory.CreateDirectory(testFolderWithoutSpace);
            TestUtils.DirectoryCopy(GetTestTemplateLocation("TemplateResolution/DifferentLanguagesGroup/BasicVB"), testFolderWithoutSpace, copySubDirs: true);
            InstallNuGetTemplate(testFolderWithoutSpace, _log, home, workingDirectory);

            new DotnetNewCommand(_log, "-u")
                .WithCustomHive(home)
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutContaining($"Basic FSharp (basic) F#")
                .And.HaveStdOutContaining($"         dotnet new uninstall '{testFolderWithSpace}'")
                .And.HaveStdOutContaining($"Basic VB (basic) VB")
                .And.HaveStdOutContaining($"         dotnet new uninstall {testFolderWithoutSpace}");
        }

        [Theory]
        [InlineData("-u")]
        [InlineData("--uninstall")]
        public void CanShowDeprecationMessage_WhenLegacyCommandIsUsed(string commandName)
        {
            const string deprecationMessage =
@"Warning: use of 'dotnet new --uninstall' is deprecated. Use 'dotnet new uninstall' instead.
For more information, run: 
   dotnet new uninstall -h";

            string home = CreateTemporaryFolder(folderName: "Home");
            CommandResult commandResult = new DotnetNewCommand(_log, commandName)
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
            CommandResult commandResult = new DotnetNewCommand(_log, "uninstall")
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
