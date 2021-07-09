// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;
using Xunit.Abstractions;

namespace Dotnet_new3.IntegrationTests
{
    public class DotnetNewInstantiate
    {
        private readonly ITestOutputHelper _log;

        public DotnetNewInstantiate(ITestOutputHelper log)
        {
            _log = log;
        }

        [Fact]
        public void CanInstantiateTemplate()
        {
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();

            new DotnetNewCommand(_log, "console")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("The template \"Console Application\" was created successfully.");
        }

        [Fact]
        public void CannotInstantiateUnknownTemplate()
        {
            var home = TestUtils.CreateTemporaryFolder("Home");

            new DotnetNewCommand(_log, "webapp")
                .WithCustomHive(home)
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute()
                .Should()
                .Fail()
                .And.NotHaveStdOut()
                .And.HaveStdErrContaining("No templates found matching: 'webapp'.")
                .And.HaveStdErrContaining($"To list installed templates, run:{Environment.NewLine}   dotnet new3 --list")
                .And.HaveStdErrContaining($"To search for the templates on NuGet.org, run:{Environment.NewLine}   dotnet new3 webapp --search");
        }

        [Fact]
        public void CannotInstantiateTemplateWithUnknownLanguage()
        {
            var home = TestUtils.CreateTemporaryFolder("Home");

            new DotnetNewCommand(_log, "console", "--language", "D#")
                .WithCustomHive(home)
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute()
                .Should()
                .Fail()
                .And.NotHaveStdOut()
                .And.HaveStdErrContaining("No templates found matching: 'console', language='D#'.")
                .And.HaveStdErrContaining($"To list installed templates, run:{Environment.NewLine}   dotnet new3 --list")
                .And.HaveStdErrContaining($"To search for the templates on NuGet.org, run:{Environment.NewLine}   dotnet new3 console --search");
        }

        [Fact]
        public void CanInstantiateTemplate_WithSingleNonDefaultLanguageChoice()
        {
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            Helpers.InstallTestTemplate("TemplateResolution/DifferentLanguagesGroup/BasicFSharp", _log, workingDirectory, home);

            new DotnetNewCommand(_log, "basic")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("The template \"Basic FSharp\" was created successfully.");
        }

        [Fact]
        public void CannotInstantiateTemplate_WhenAmbiguousLanguageChoice()
        {
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            Helpers.InstallTestTemplate("TemplateResolution/DifferentLanguagesGroup/BasicFSharp", _log, workingDirectory, home);
            Helpers.InstallTestTemplate("TemplateResolution/DifferentLanguagesGroup/BasicVB", _log, workingDirectory, home);

            new DotnetNewCommand(_log, "basic")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .Fail()
                .And.NotHaveStdOut()
                .And.HaveStdErrContaining("Unable to resolve the template, these templates matched your input:")
                .And.HaveStdErrContaining("Re-run the command specifying the language to use with --language option.")
                .And.HaveStdErrContaining("basic").And.HaveStdErrContaining("F#").And.HaveStdErrContaining("VB");
        }

        [Fact]
        public void CannotInstantiateTemplate_OnAmbiguousGroupChoice()
        {
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();

            new DotnetNewCommand(_log, "class")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should().Fail()
                .And.NotHaveStdOut()
                .And.NotHaveStdOutContaining("The template \"Class Library\" was created successfully.")
                .And.HaveStdErrContaining("No templates found matching: 'class'.")
                .And.HaveStdErrContaining($"To list installed templates, run:{Environment.NewLine}   dotnet new3 --list")
                .And.HaveStdErrContaining($"To search for the templates on NuGet.org, run:{Environment.NewLine}   dotnet new3 class --search");

            new DotnetNewCommand(_log, "conf")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .Fail()
                .And.NotHaveStdOut()
                .And.HaveStdErrContaining("No templates found matching: 'conf'.")
                .And.HaveStdErrContaining($"To list installed templates, run:{Environment.NewLine}   dotnet new3 --list")
                .And.HaveStdErrContaining($"To search for the templates on NuGet.org, run:{Environment.NewLine}   dotnet new3 conf --search");

            new DotnetNewCommand(_log, "file")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .Fail()
                .And.NotHaveStdOut()
                .And.HaveStdErrContaining("No templates found matching: 'file'.")
                .And.HaveStdErrContaining($"To list installed templates, run:{Environment.NewLine}   dotnet new3 --list")
                .And.HaveStdErrContaining($"To search for the templates on NuGet.org, run:{Environment.NewLine}   dotnet new3 file --search");
        }

        [Fact]
        public void CannotInstantiateTemplate_WhenFullNameIsUsed()
        {
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();

            new DotnetNewCommand(_log, "Console Application")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should().Fail()
                .And.NotHaveStdOut()
                .And.NotHaveStdOutContaining("The template \"Console Application\" was created successfully.")
                .And.HaveStdErrContaining("No templates found matching: 'Console Application'.")
                .And.HaveStdErrContaining($"To list installed templates, run:{Environment.NewLine}   dotnet new3 --list")
                .And.HaveStdErrContaining($"To search for the templates on NuGet.org, run:{Environment.NewLine}   dotnet new3 'Console Application' --search");
        }

        [Fact]
        public void CannotInstantiateTemplate_WhenParameterIsInvalid()
        {
            string expectedCta =
@"For more information, run:
   dotnet new3 console -h"; 

            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();

            new DotnetNewCommand(_log, "console", "--fake")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .Fail()
                .And.NotHaveStdOut()
                .And.HaveStdErrContaining("Error: Invalid option(s):")
                .And.HaveStdErrContaining("   '--fake' is not a valid option")
                .And.HaveStdErrContaining(expectedCta);

            new DotnetNewCommand(_log, "console", "--framework", "fake")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .Fail()
                .And.NotHaveStdOut()
                .And.HaveStdErrContaining("Error: Invalid option(s):")
                .And.HaveStdErrContaining("   'fake' is not a valid value for --framework. The possible values are:")
                .And.HaveStdErrContaining("      net5.0          - Target net5.0")
                .And.HaveStdErrContaining("      netcoreapp3.1   - Target netcoreapp3.1")
                .And.HaveStdErrContaining(expectedCta);

            new DotnetNewCommand(_log, "console", "--framework", "netcoreapp")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .Fail()
                .And.NotHaveStdOut()
                .And.HaveStdErrContaining("Error: Invalid option(s):")
                .And.HaveStdErrContaining("--framework netcoreapp")
                .And.HaveStdErrContaining("   'netcoreapp' is not a valid value for --framework. The possible values are:")
                .And.HaveStdErrContaining("      netcoreapp2.1   - Target netcoreapp2.1")
                .And.HaveStdErrContaining("      netcoreapp3.1   - Target netcoreapp3.1")
                .And.HaveStdErrContaining(expectedCta);

            new DotnetNewCommand(_log, "console", "--framework", "netcoreapp", "--fake")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .Fail()
                .And.NotHaveStdOut()
                .And.HaveStdErrContaining("Error: Invalid option(s):")
                .And.HaveStdErrContaining("   'netcoreapp' is not a valid value for --framework. The possible values are:")
                .And.HaveStdErrContaining("      netcoreapp2.1   - Target netcoreapp2.1")
                .And.HaveStdErrContaining("      netcoreapp3.1   - Target netcoreapp3.1")
                .And.HaveStdErrContaining("   '--fake' is not a valid option")
                .And.HaveStdErrContaining(expectedCta);
        }

        [Fact]
        public void CannotInstantiateTemplate_WhenPrecedenceIsSame()
        {
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            Helpers.InstallTestTemplate("TemplateResolution/SamePrecedenceGroup/BasicTemplate1", _log, workingDirectory, home);
            Helpers.InstallTestTemplate("TemplateResolution/SamePrecedenceGroup/BasicTemplate2", _log, workingDirectory, home);

            new DotnetNewCommand(_log, "basic")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .Fail()
                .And.NotHaveStdOut()
                .And.HaveStdErrContaining("Unable to resolve the template, the following installed templates are conflicting:")
                .And.HaveStdErrContaining("Uninstall the templates or the packages to keep only one template from the list.")
                .And.HaveStdErrContaining("TestAssets.SamePrecedenceGroup.BasicTemplate2")
                .And.HaveStdErrContaining("TestAssets.SamePrecedenceGroup.BasicTemplate1")
                .And.HaveStdErrContaining("basic")
                .And.HaveStdErrContaining("C#")
                .And.HaveStdErrContaining("Test Asset")
                .And.HaveStdErrContaining("100")
                .And.HaveStdErrContaining($"{Path.DirectorySeparatorChar}test_templates{Path.DirectorySeparatorChar}TemplateResolution{Path.DirectorySeparatorChar}SamePrecedenceGroup{Path.DirectorySeparatorChar}BasicTemplate2")
                .And.HaveStdErrContaining($"{Path.DirectorySeparatorChar}test_templates{Path.DirectorySeparatorChar}TemplateResolution{Path.DirectorySeparatorChar}SamePrecedenceGroup{Path.DirectorySeparatorChar}BasicTemplate1");
        }

        [Fact]
        public void CanInstantiateTemplate_WithAlias()
        {
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();

            new DotnetNewCommand(_log, "console", "--alias", "csharpconsole")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("Successfully created alias named 'csharpconsole' with value 'console'");

            new DotnetNewCommand(_log, "console", "-n", "MyConsole", "-o", "no-alias")
             .WithCustomHive(home)
             .WithWorkingDirectory(workingDirectory)
             .Execute()
             .Should()
             .ExitWith(0)
             .And.NotHaveStdErr()
             .And.HaveStdOutContaining("The template \"Console Application\" was created successfully.");

            new DotnetNewCommand(_log, "csharpconsole", "-n", "MyConsole", "-o", "alias")
               .WithCustomHive(home)
               .WithWorkingDirectory(workingDirectory)
               .Execute()
               .Should()
               .ExitWith(0)
               .And.NotHaveStdErr()
               .And.HaveStdOutContaining("The template \"Console Application\" was created successfully.")
               .And.HaveStdOutContaining("After expanding aliases, the command is:")
               .And.HaveStdOutContaining("dotnet new3 console -n MyConsole -o alias");

            Assert.Equal(
                new DirectoryInfo(Path.Combine(workingDirectory, "no-alias")).EnumerateFileSystemInfos().Select(fi => fi.Name),
                new DirectoryInfo(Path.Combine(workingDirectory, "alias")).EnumerateFileSystemInfos().Select(fi => fi.Name));

        }
        
        [Fact]
        public void CannotOverwriteFilesWithoutForce()
        {
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();

            new DotnetNewCommand(_log, "console")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("The template \"Console Application\" was created successfully.");

            new DotnetNewCommand(_log, "console")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should().Fail()
                .And.HaveStdErrContaining("Creating this template will make changes to existing files:")
                .And.HaveStdErrMatching(@$"  Overwrite   \.[\\\/]{Path.GetFileName(workingDirectory)}\.csproj")
                .And.HaveStdErrMatching(@"  Overwrite   \.[\\\/]Program\.cs")
                .And.HaveStdErrContaining("Rerun the command and pass --force to accept and create.");
        }

        [Fact]
        public void CanOverwriteFilesWithForce()
        {
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();

            var commandResult = new DotnetNewCommand(_log, "console", "--no-restore")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("The template \"Console Application\" was created successfully.");

            var forceCommandResult = new DotnetNewCommand(_log, "console", "--no-restore", "--force")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            forceCommandResult.Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("The template \"Console Application\" was created successfully.");

            Assert.Equal(commandResult.StdOut, forceCommandResult.StdOut);
        }

        [Fact]
        public void CanInstantiateTemplateWithSecondShortName()
        {
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            Helpers.InstallNuGetTemplate("Microsoft.DotNet.Web.ProjectTemplates.5.0", _log, workingDirectory, home);

            new DotnetNewCommand(_log, "webapp", "-o", "webapp")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("The template \"ASP.NET Core Web App\" was created successfully.");

            new DotnetNewCommand(_log, "razor", "-o", "razor")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("The template \"ASP.NET Core Web App\" was created successfully.");
        }

        [Fact]
        public void CanInstantiateTemplate_WithBinaryFile_FromFolder()
        {
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            string home = TestUtils.CreateTemporaryFolder("Home");
            string templateLocation = TestUtils.GetTestTemplateLocation("TemplateWithBinaryFile");

            Helpers.InstallTestTemplate(templateLocation, _log, workingDirectory, home);

            new DotnetNewCommand(_log, "TestAssets.TemplateWithBinaryFile")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should().Pass();

            string sourceImage = Path.Combine(templateLocation, "image.png");
            string targetImage = Path.Combine(workingDirectory, "image.png");

            Assert.True(File.Exists(targetImage));

            Assert.Equal(
                new FileInfo(sourceImage).Length,
                new FileInfo(targetImage).Length);
            Assert.True(TestUtils.CompareFiles(sourceImage, targetImage), $"The content of {sourceImage} and {targetImage} is not same.");
        }

        [Fact]
        public void CanInstantiateTemplate_WithBinaryFile_FromPackage()
        {
            string templateLocation = TestUtils.GetTestTemplateLocation("TemplateWithBinaryFile");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            string home = TestUtils.CreateTemporaryFolder("Home");

            using var packageManager = new PackageManager();
            string packageLocation = packageManager.PackTestTemplatesNuGetPackage();
            Helpers.InstallNuGetTemplate(packageLocation, _log, workingDirectory, home);

            new DotnetNewCommand(_log, "TestAssets.TemplateWithBinaryFile")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should().Pass();

            string sourceImage = Path.Combine(templateLocation, "image.png");
            string targetImage = Path.Combine(workingDirectory, "image.png");

            Assert.True(File.Exists(targetImage));

            Assert.Equal(
                new FileInfo(sourceImage).Length,
                new FileInfo(targetImage).Length);
            Assert.True(TestUtils.CompareFiles(sourceImage, targetImage), $"The content of {sourceImage} and {targetImage} is not same.");
        }

        [Fact]
        public async Task CanInstantiateTemplate_Angular_CanReplaceTextInLargeFile()
        {
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            string home = TestUtils.CreateTemporaryFolder("Home");
            using var packageManager = new PackageManager();
            string packageLocation = await packageManager.GetNuGetPackage("Microsoft.DotNet.Web.Spa.ProjectTemplates.6.0").ConfigureAwait(false);
            Helpers.InstallNuGetTemplate(packageLocation, _log, workingDirectory, home);

            new DotnetNewCommand(_log, "angular", "-o", "angular")
               .WithCustomHive(home)
               .WithWorkingDirectory(workingDirectory)
               .Execute()
               .Should().Pass();

            string reactPackageLockJson = Path.Combine(workingDirectory, "angular", "ClientApp", "package-lock.json");
            var targetText = File.ReadAllText(reactPackageLockJson);

            using (ZipArchive archive = ZipFile.OpenRead(packageLocation))
            {
                var reactPackageLockJsonEntry = archive.GetEntry("Angular-CSharp/ClientApp/package-lock.json");
                Assert.NotNull(reactPackageLockJsonEntry);
                using var sourceStream = new StreamReader(reactPackageLockJsonEntry!.Open());
                var sourceText = sourceStream.ReadToEnd();
                //sourceText = sourceText.Replace("company.webapplication1", "angular");
                Assert.Equal(sourceText, targetText);
            }
        }
    }
}
