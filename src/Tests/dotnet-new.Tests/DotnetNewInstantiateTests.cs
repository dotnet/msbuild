// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    public partial class DotnetNewInstantiateTests : BaseIntegrationTest, IClassFixture<SharedHomeDirectory>
    {
        private readonly SharedHomeDirectory _fixture;
        private readonly ITestOutputHelper _log;

        public DotnetNewInstantiateTests(SharedHomeDirectory fixture, ITestOutputHelper log) : base(log)
        {
            _fixture = fixture;
            _log = log;
        }

        [Fact]
        public void CanInstantiateTemplate()
        {
            string workingDirectory = CreateTemporaryFolder();

            new DotnetNewCommand(_log, "console")
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("The template \"Console App\" was created successfully.");
        }

#pragma warning disable xUnit1004 // Test methods should not be skipped
        [Fact(Skip = "alias feature is not implemented yet")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        public void CanInstantiateTemplate_WithAlias()
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();

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
             .And.HaveStdOutContaining("The template \"Console App\" was created successfully.");

            new DotnetNewCommand(_log, "csharpconsole", "-n", "MyConsole", "-o", "alias")
               .WithCustomHive(home)
               .WithWorkingDirectory(workingDirectory)
               .Execute()
               .Should()
               .ExitWith(0)
               .And.NotHaveStdErr()
               .And.HaveStdOutContaining("The template \"Console App\" was created successfully.")
               .And.HaveStdOutContaining("After expanding aliases, the command is:")
               .And.HaveStdOutContaining("dotnet new console -n MyConsole -o alias");

            Assert.Equal(
                new DirectoryInfo(Path.Combine(workingDirectory, "no-alias")).EnumerateFileSystemInfos().Select(fi => fi.Name),
                new DirectoryInfo(Path.Combine(workingDirectory, "alias")).EnumerateFileSystemInfos().Select(fi => fi.Name));

        }

        [Fact]
        public void CanInstantiateTemplate_WithSingleNonDefaultLanguageChoice()
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();
            InstallTestTemplate("TemplateResolution/DifferentLanguagesGroup/BasicFSharp", _log, home, workingDirectory);

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
        public void CanOverwriteFilesWithForce()
        {
            string workingDirectory = CreateTemporaryFolder();

            Utils.CommandResult commandResult = new DotnetNewCommand(_log, "console", "--no-restore")
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult.Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("The template \"Console App\" was created successfully.");

            Utils.CommandResult forceCommandResult = new DotnetNewCommand(_log, "console", "--no-restore", "--force")
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            forceCommandResult.Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("The template \"Console App\" was created successfully.");

            Assert.Equal(commandResult.StdOut, forceCommandResult.StdOut);
        }

        [Fact]
        public void CanInstantiateTemplateWithSecondShortName()
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();
            InstallNuGetTemplate("Microsoft.DotNet.Web.ProjectTemplates.5.0", _log, home, workingDirectory);

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
            string workingDirectory = CreateTemporaryFolder();
            string home = CreateTemporaryFolder(folderName: "Home");
            string templateLocation = GetTestTemplateLocation("TemplateWithBinaryFile");

            InstallTestTemplate(templateLocation, _log, home, workingDirectory);

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
            string templateLocation = GetTestTemplateLocation("TemplateWithBinaryFile");
            string workingDirectory = CreateTemporaryFolder();
            string home = CreateTemporaryFolder(folderName: "Home");

            string packageLocation = PackTestNuGetPackage(_log);
            InstallNuGetTemplate(packageLocation, _log, home, workingDirectory);

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
        public void CanInstantiateTemplate_WithParamsSharingPrefix()
        {
            string workingDirectory = CreateTemporaryFolder();
            string home = CreateTemporaryFolder(folderName: "Home");
            string templateLocation = GetTestTemplateLocation("TemplateWithParamsSharingPrefix");

            InstallTestTemplate(templateLocation, _log, home, workingDirectory);

            // not asserting on actual generated content - as there is none
            new DotnetNewCommand(_log, "TestAssets.TemplateWithParamsSharingPrefix")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .Pass()
                .And.NotHaveStdErr();
        }

        [Theory]
        [InlineData(".dockerignore", "singleHash", false)]
        [InlineData(".editorconfig", "singleHash", false)]
        [InlineData(".gitattributes", "singleHash", false)]
        [InlineData(".gitignore", "singleHash", false)]
        [InlineData("Dockerfile", "singleHash", false)]
        [InlineData("nuget.config", "xml", false)]
        [InlineData("cake", "cSharpNoComments")]
        [InlineData("sln", "singleHash")]
        [InlineData("yaml", "singleHash")]
        [InlineData("md", "xml")]
        public void CanInstantiateTemplate_WithConditions_BasedOnFileName(string testCase, string conditionType, bool useAsExtension = true)
        {
            string expectedCommandFormat = conditionType switch
            {
                "singleHash" => "# comment {0}",
                "xml" => "<!-- comment {0} -->",
                "cSharpNoComments" => "// comment {0}",
                _ => throw new NotSupportedException($"conditionType {conditionType} is not supported")
            };

            string fileName = useAsExtension ? $"test.{testCase}" : testCase;

            //sln always has CRLF line ending, as per .gitattributes settings
            string expectedEol = testCase == "sln" ? "\r\n" : Environment.NewLine;

            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();

            //The template has the following conditions defined in various file types: non actionable on parameter A and actionable on parameter B
            //#if (A)
            //# comment foo
            //foo
            //#endif
            //##if (B)
            //## comment bar
            //#bar
            //#endif
            //baz
            //For extension test cases the template has 'test.<extension>' file defined.

            InstallTestTemplate("TemplateWithConditions", _log, home, workingDirectory);
            new DotnetNewCommand(_log, "TestAssets.TemplateWithConditions", "--A", "true")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("The template \"TemplateWithConditions\" was created successfully.");

            string testFile = Path.Combine(workingDirectory, fileName);
            Assert.True(File.Exists(testFile));
            Assert.Equal($"{string.Format(expectedCommandFormat, "foo")}{expectedEol}foo{expectedEol}baz{expectedEol}", File.ReadAllText(testFile));

            workingDirectory = CreateTemporaryFolder();
            new DotnetNewCommand(_log, "TestAssets.TemplateWithConditions", "--A", "false")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("The template \"TemplateWithConditions\" was created successfully.");

            testFile = Path.Combine(workingDirectory, fileName);
            Assert.True(File.Exists(testFile));
            Assert.Equal($"baz{expectedEol}", File.ReadAllText(testFile));

            workingDirectory = CreateTemporaryFolder();
            new DotnetNewCommand(_log, "TestAssets.TemplateWithConditions", "--B", "true")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("The template \"TemplateWithConditions\" was created successfully.");

            testFile = Path.Combine(workingDirectory, fileName);
            Assert.True(File.Exists(testFile));
            Assert.Equal($"{string.Format(expectedCommandFormat, "bar")}{expectedEol}bar{expectedEol}baz{expectedEol}", File.ReadAllText(testFile));
        }

        [Theory]
        [InlineData("", "theDefaultName.cs")]
        [InlineData("newName", "newName.cs")]
        public void CanInstantiateTemplate_WithDefaultName(string name, string expectedFileName)
        {
            string workingDirectory = CreateTemporaryFolder();
            string home = CreateTemporaryFolder(folderName: "Home");
            InstallTestTemplate("TemplateWithPreferDefaultName", _log, home, workingDirectory);

            workingDirectory = CreateTemporaryFolder();
            new DotnetNewCommand(_log, "TestAssets.TemplateWithPreferDefaultName", "-n", name)
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("The template \"TemplateWithPreferDefaultName\" was created successfully.");

            string testFile = Path.Combine(workingDirectory, expectedFileName);
            Assert.True(File.Exists(testFile));
        }

        [Fact]
        public void DoesNotReportErrorOnDefaultUpdateCheckOfLocalPackageDuringInstantiation()
        {
            string nugetName = "TestNupkgInstallTemplate";
            string nugetVersion = "0.0.1";
            string nugetFullName = $"{nugetName}::{nugetVersion}";
            string nugetFileName = $"{nugetName}.{nugetVersion}.nupkg";
            string templateName = "nupkginstall";
            string workingDirectory = CreateTemporaryFolder();
            string home = CreateTemporaryFolder(folderName: "Home");

            InstallNuGetTemplate(
                Path.Combine(DotnetNewTestPackagesBasePath, nugetFileName),
                _log,
                home,
                workingDirectory);

            new DotnetNewCommand(_log, templateName, "--dry-run")
                .WithCustomHive(home).WithoutBuiltInTemplates()
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .Pass()
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("File actions would have been taken:");
        }

        [Fact]
        public void WhenSwitchIsSkippedThenItPrintsError()
        {
            Utils.CommandResult cmd = new DotnetNewCommand(Log)
                .WithVirtualHive()
                .Execute("Web1.1");

            cmd.ExitCode.Should().NotBe(0);

            if (!TestContext.IsLocalized())
            {
                cmd.StdErr.Should().StartWith("No templates or subcommands found");
            }
        }

        [Fact]
        public void ItCanCreateTemplate()
        {
            string tempDir = CreateTemporaryFolder();
            Utils.CommandResult cmd = new DotnetNewCommand(Log)
                .WithVirtualHive()
                .Execute("console", "-o", tempDir);
            cmd.Should().Pass();
        }

        [Fact]
        public void ItCanShowHelp()
        {
            Utils.CommandResult cmd = new DotnetNewCommand(Log)
                .WithVirtualHive()
                .Execute("--help");
            cmd.Should().Pass()
                .And.HaveStdOutContaining("Usage:")
                .And.HaveStdOutContaining("dotnet new [command] [options]");
        }

        [Fact]
        public void ItCanShowHelpForTemplate()
        {
            Utils.CommandResult cmd = new DotnetNewCommand(Log)
                .WithVirtualHive()
                .Execute("classlib", "--help");

            cmd.Should().Pass()
                .And.NotHaveStdOutContaining("Usage: new [options]")
                .And.HaveStdOutContaining("Class Library (C#)")
                .And.HaveStdOutContaining("--framework");
        }

        [Theory]
        [InlineData("-lang", "F#", "--use-program-main")]
        [InlineData("--language", "F#", "--use-program-main")]
        [InlineData("-lang", "C#", "--no-exist")]
        public void ExampleHasLanguageForSepecifiedLanguageWithInvalidOption(string languageOption, string language, string invalidOption)
        {
            CommandResult cmd = new DotnetNewCommand(Log, "console", languageOption, language, invalidOption)
                .WithVirtualHive()
                .Execute();
            cmd.Should().Fail()
                .And.HaveStdErrContaining($"'{invalidOption}' is not a valid option")
                .And.HaveStdErrContaining("For more information, run:")
                .And.HaveStdErrContaining($"dotnet new console --language {language} -h");
        }

        [Fact]
        public void ItCanShowParseError()
        {
            Utils.CommandResult cmd = new DotnetNewCommand(Log)
                .WithVirtualHive()
                .Execute("update", "--bla");
            cmd.Should().ExitWith(127)
                .And.HaveStdErrContaining("Unrecognized command or argument '--bla'")
                .And.HaveStdOutContaining("dotnet new update [options]");
        }

        [Fact]
        public void WhenTemplateNameIsNotUniquelyMatchedThenItIndicatesProblemToUser()
        {
            Utils.CommandResult cmd = new DotnetNewCommand(Log)
                .WithVirtualHive()
                .Execute("c");

            cmd.ExitCode.Should().NotBe(0);

            if (!TestContext.IsLocalized())
            {
                cmd.StdErr.Should().StartWith("No templates or subcommands found matching: 'c'.");
            }
        }

        [Fact]
        public void When_dotnet_new_is_invoked_multiple_times_it_should_fail()
        {
            string rootPath = CreateTemporaryFolder();

            new DotnetNewCommand(Log)
                .WithVirtualHive()
                .WithWorkingDirectory(rootPath)
                .Execute($"console", "--no-restore");

            DateTime expectedState = Directory.GetLastWriteTime(rootPath);

            CommandResult result = new DotnetNewCommand(Log)
                .WithVirtualHive()
                .WithWorkingDirectory(rootPath)
                .Execute($"console", "--no-restore");

            DateTime actualState = Directory.GetLastWriteTime(rootPath);

            Assert.Equal(expectedState, actualState);

            result.Should().Fail();
        }

        [Fact]
        public void When_dotnet_new_is_invoked_with_preferred_lang_env_var_set()
        {
            string rootPath = CreateTemporaryFolder();

            new DotnetNewCommand(Log)
                .WithVirtualHive()
                .WithWorkingDirectory(rootPath)
                .WithEnvironmentVariable("DOTNET_NEW_PREFERRED_LANG", "F#")
                .Execute($"console", "--no-restore", "-n", "f1")
                .Should().Pass();

            string expectedFsprojPath = Path.Combine(rootPath, "f1", "f1.fsproj");
            Assert.True(File.Exists(expectedFsprojPath), $"expected '{expectedFsprojPath}' but was not found");
        }

        [Fact]
        public void When_dotnet_new_is_invoked_default_is_csharp()
        {
            string rootPath = CreateTemporaryFolder();

            new DotnetNewCommand(Log)
                .WithVirtualHive()
                .WithWorkingDirectory(rootPath)
                .Execute($"console", "--no-restore", "-n", "c1")
                .Should().Pass();

            string expectedCsprojPath = Path.Combine(rootPath, "c1", "c1.csproj");
            Assert.True(File.Exists(expectedCsprojPath), $"expected '{expectedCsprojPath}' but was not found");
        }

        [Fact]
        public void Dotnet_new_can_be_invoked_with_lang_option()
        {
            string rootPath = CreateTemporaryFolder();

            new DotnetNewCommand(Log)
                .WithVirtualHive()
                .WithWorkingDirectory(rootPath)
                .Execute($"console", "--no-restore", "-n", "vb1", "-lang", "vb")
                .Should().Pass();

            string expectedCsprojPath = Path.Combine(rootPath, "vb1", "vb1.vbproj");
            Assert.True(File.Exists(expectedCsprojPath), $"expected '{expectedCsprojPath}' but was not found");
        }

        [Fact]
        public void When_dotnet_new_is_invoked_with_preferred_lang_env_var_empty()
        {
            string rootPath = CreateTemporaryFolder();

            new DotnetNewCommand(Log)
                .WithVirtualHive()
                .WithWorkingDirectory(rootPath)
                .WithEnvironmentVariable("DOTNET_NEW_PREFERRED_LANG", "")
                .Execute($"console", "--no-restore", "-n", "c1")
                .Should().Pass();

            string expectedCsprojPath = Path.Combine(rootPath, "c1", "c1.csproj");
            Assert.True(File.Exists(expectedCsprojPath), $"expected '{expectedCsprojPath}' but was not found");
        }
    }
}
