// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    [UsesVerify]
    public partial class DotnetNewInstantiateTests
    {
        [Fact]
        public Task CannotInstantiateUnknownTemplate()
        {
            CommandResult commandResult = new DotnetNewCommand(_log, "unknownapp")
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute();

            commandResult
                .Should()
                .Fail()
                .And.NotHaveStdOut();

            return Verify(commandResult.StdErr);
        }

        [Fact]
        public Task CannotInstantiateTemplateWithUnknownLanguage()
        {
            CommandResult commandResult = new DotnetNewCommand(_log, "console", "--language", "D#")
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute();

            commandResult
                .Should()
                .Fail()
                .And.NotHaveStdOut();

            return Verify(commandResult.StdErr);
        }

        [Fact]
        public Task CannotInstantiateTemplateWithUnknownType()
        {
            CommandResult commandResult = new DotnetNewCommand(_log, "console", "--type", "item")
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(CreateTemporaryFolder())
                .Execute();

            commandResult
                .Should()
                .Fail()
                .And.NotHaveStdOut();

            return Verify(commandResult.StdErr);
        }

        [Fact]
        public Task CannotInstantiateTemplate_WhenAmbiguousLanguageChoice()
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();
            InstallTestTemplate("TemplateResolution/DifferentLanguagesGroup/BasicFSharp", _log, home, workingDirectory);
            InstallTestTemplate("TemplateResolution/DifferentLanguagesGroup/BasicVB", _log, home, workingDirectory);

            CommandResult commandResult = new DotnetNewCommand(_log, "basic")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult
                .Should()
                .Fail()
                .And.NotHaveStdOut();

            return Verify(commandResult.StdErr);
        }

        [Fact]
        public Task CannotInstantiateTemplate_WhenAmbiguousShortNameChoice()
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();
            string templateOneLocation = InstallTestTemplate("TemplateResolution/SameShortName/BasicFSharp", _log, home, workingDirectory);
            string templateTwoLocation = InstallTestTemplate("TemplateResolution/SameShortName/BasicVB", _log, home, workingDirectory);

            CommandResult commandResult = new DotnetNewCommand(_log, "basic")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult
                .Should()
                .Fail()
                .And.NotHaveStdOut();

            return Verify(commandResult.StdErr)
                .AddScrubber(output =>
                {
                    //package locaions are machine specific so we cannot use them in approval tests
                    output.Replace(templateOneLocation, "%TEMPLATE ONE LOCATION%");
                    output.Replace(templateTwoLocation, "%TEMPLATE TWO LOCATION%");

                    //removes the delimiter line as we don't know the length of last columns containing paths above
                    output.ScrubTableHeaderDelimiter();
                    //removes the spaces after "Package" column header as we don't know the amount of spaces after it (depends on the paths above)
                    output.ScrubByRegex("Package *", "Package");
                });
        }

        [Fact]
        public Task CannotInstantiateTemplate_WhenFullNameIsUsed()
        {
            string workingDirectory = CreateTemporaryFolder();

            CommandResult commandResult = new DotnetNewCommand(_log, "Console App")
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult
                .Should().Fail()
                .And.NotHaveStdOut();

            return Verify(commandResult.StdErr);
        }

        [Fact]
        public Task CannotInstantiateTemplate_WhenNoDefaultNameSpecified()
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();
            InstallTestTemplate("TemplateWithPreferDefaultNameButNoDefaultName", _log, home, workingDirectory);

            CommandResult commandResult = new DotnetNewCommand(_log, "TestAssets.TemplateWithPreferDefaultNameButNoDefaultName")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult
                .Should()
                .Fail()
                .And.NotHaveStdOut();

            return Verify(commandResult.StdErr);
        }

        [Fact]
        public Task CannotInstantiateTemplate_WhenParameterIsInvalid()
        {
            string workingDirectory = CreateTemporaryFolder();

            CommandResult commandResult = new DotnetNewCommand(_log, "console", "--fake")
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult
                .Should()
                .Fail()
                .And.NotHaveStdOut();

            return Verify(commandResult.StdErr);
        }

        [Fact]
        public Task CannotInstantiateTemplate_WhenChoiceParameterValueIsInvalid()
        {
            string workingDirectory = CreateTemporaryFolder();

            CommandResult commandResult = new DotnetNewCommand(_log, "console", "--framework", "fake")
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult
                .Should()
                .Fail()
                .And.NotHaveStdOut();

            return Verify(commandResult.StdErr);
        }

        [Fact]
        public Task CannotInstantiateTemplate_WhenChoiceParameterValueIsNotComplete()
        {
            string workingDirectory = CreateTemporaryFolder();

            var commandResult = new DotnetNewCommand(_log, "console", "--framework", "net")
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult
                .Should()
                .Fail()
                .And.NotHaveStdOut();

            return Verify(commandResult.StdErr);
        }

        [Fact]
        public Task CannotInstantiateTemplate_OnMultipleParameterErrors()
        {
            string workingDirectory = CreateTemporaryFolder();

            CommandResult commandResult = new DotnetNewCommand(_log, "console", "--framework", "netcoreapp", "--fake")
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult
                .Should()
                .Fail()
                .And.NotHaveStdOut();

            return Verify(commandResult.StdErr);
        }

        [Fact]
        public Task CannotInstantiateTemplate_WhenPrecedenceIsSame()
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();
            string templateOneLocation = InstallTestTemplate("TemplateResolution/SamePrecedenceGroup/BasicTemplate1", _log, home, workingDirectory);
            string templateTwoLocation = InstallTestTemplate("TemplateResolution/SamePrecedenceGroup/BasicTemplate2", _log, home, workingDirectory);

            CommandResult commandResult = new DotnetNewCommand(_log, "basic")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult
                .Should()
                .Fail()
                .And.NotHaveStdOut()
                .And.HaveStdErrContaining(templateOneLocation)
                .And.HaveStdErrContaining(templateTwoLocation);

            return Verify(commandResult.StdErr)
                .AddScrubber(output =>
                {
                    //package locaions are machine specific so we cannot use them in approval tests
                    output.Replace(templateOneLocation, "%TEMPLATE ONE LOCATION%");
                    output.Replace(templateTwoLocation, "%TEMPLATE TWO LOCATION%");

                    //removes the delimiter line as we don't know the length of last columns containing paths above
                    output.ScrubTableHeaderDelimiter();
                    //removes the spaces after "Package" column header as we don't know the amount of spaces after it (depends on the paths above)
                    output.ScrubByRegex("Package *", "Package");
                });
        }

        [Fact]
        public async Task CanInstantiateTemplate_MultiValueChoiceParameterConditions()
        {
            // We cannot use Data-driven unit test (InlineData) as it's not supported by verifier framework (unless separate file per parameters is supplied)
            await MultiValueChoiceParameterConditionsExecutor(new[] { "TestAssets.TemplateWithMultiValueChoice", "--Platform", "MacOS", "--Platform", "iOS" }).ConfigureAwait(false);
            await MultiValueChoiceParameterConditionsExecutor(new[] { "TestAssets.TemplateWithMultiValueChoice", "--Platform", "MacOS", "iOS" }).ConfigureAwait(false);
        }

        private Task MultiValueChoiceParameterConditionsExecutor(string[] args)
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();
            InstallTestTemplate("TemplateWithMultiValueChoice", _log, home, workingDirectory);

            CommandResult commandResult = new DotnetNewCommand(_log, args)
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult
                .Should()
                .Pass()
                .And.NotHaveStdErr()
                .And.HaveStdOutMatching("The template \"TemplateWithMultiValueChoice\" was created successfully\\.");

            string resultFileContent = File.ReadAllText(Path.Combine(workingDirectory, "Test.cs"));

            return Verify(resultFileContent)
                .DisableRequireUniquePrefix();
        }

        [Fact]
        public Task CanInstantiateTemplate_MultiValueChoiceParameterExplicitlyUnset()
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();
            InstallTestTemplate("TemplateWithMultiValueChoice", _log, home, workingDirectory);

            CommandResult commandResult = new DotnetNewCommand(_log, "TestAssets.TemplateWithMultiValueChoice", "--Platform", "")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult
                .Should()
                .Pass()
                .And.NotHaveStdErr()
                .And.HaveStdOutMatching("The template \"TemplateWithMultiValueChoice\" was created successfully\\.");

            string resultFileContent = File.ReadAllText(Path.Combine(workingDirectory, "Test.cs"));

            return Verify(resultFileContent);
        }

        [Fact]
        public Task CanInstantiateTemplate_WithConditionalParameters_DisabledBehaveLikeNotSpecified()
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();
            InstallTestTemplate("TemplateWithConditionalParameters", _log, home, workingDirectory);

            CommandResult commandResult = new DotnetNewCommand(
                    _log,
                    "TestAssets.TemplateWithConditionalParameters",
                    "--A_enabled",
                    "true",
                    "--B_enabled",
                    "false",
                    "--paramA",
                    "true",
                    "--paramB",
                    "true")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult
                .Should()
                .Pass()
                .And.NotHaveStdErr()
                .And.HaveStdOutMatching("The template \"TemplateWithConditionalParameters\" was created successfully\\.");

            string resultFileContent = File.ReadAllText(Path.Combine(workingDirectory, "Test.cs"));

            return Verify(resultFileContent);
        }

        [Fact]
        public void CannotInstantiateTemplate_MultiValueChoiceParameterWithExplicitUnsetAndOtherChoice()
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();
            InstallTestTemplate("TemplateWithMultiValueChoice", _log, home, workingDirectory);

            CommandResult commandResult = new DotnetNewCommand(_log, "TestAssets.TemplateWithMultiValueChoice", "--Platform", "", "--Platform", "MacOS")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult
                .Should()
                .Fail()
                .And.HaveStdErrContaining("is not a valid value for --Platform.");
        }

        [Fact]
        public Task CanInstantiateTemplate_ConditionalProcessing()
        {
            string workingDirectory = CreateTemporaryFolder();
            string home = CreateTemporaryFolder(folderName: "Home");
            InstallTestTemplate($"TemplateConditionalProcessing", _log, home, workingDirectory);

            new DotnetNewCommand(_log, "TestAssets.TemplateConditionalProcessing")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("The template \"TemplateConditionalProcessing\" was created successfully.");

            string[] actualFiles = Directory.GetFiles(workingDirectory);

            return Task.WhenAll(
                actualFiles.Where(f => Path.GetExtension(f).Equals(".txt")).Select(
                    async (file) =>
                    await VerifyFile(file)
                    .UseMethodName($"CanInstantiateTemplate_ConditionalProcessing_{Path.GetFileName(file)}")
                    ));
        }

        [Fact]
        public Task DryRunRespectsTargetPathAndOutputDir()
        {
            const string _OUT_FOLDER = "folderF";
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();
            InstallTestTemplate("TemplateWithSourceNameAndCustomTargetPath", _log, home, workingDirectory);

            CommandResult commandResult = new DotnetNewCommand(_log, "TestAssets.TemplateWithSourceNameAndCustomTargetPath", "-o", _OUT_FOLDER, "--dry-run")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr();

            string[] expectedFiles = new[] { $"{_OUT_FOLDER}.name.txt", $"{_OUT_FOLDER}/{_OUT_FOLDER}.cs" };

            return Verify(commandResult.StdOut)
                .AddScrubber(output =>
                {
                    //unify directory separators
                    output = output.Replace("\\", "/");
                    //order of files may vary, replace filename with placeholders
                    //filenames are verified above
                    foreach (string file in expectedFiles)
                    {
                        output = output.Replace(file, "%FILENAME%");
                    }
                });
        }

        [Fact]
        public Task CannotOverwriteFilesWithoutForce()
        {
            string workingDirectory = CreateTemporaryFolder();

            new DotnetNewCommand(_log, "console", "--name", "overwrite-test", "-o", "folderA")
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("The template \"Console App\" was created successfully.");

            CommandResult commandResult = new DotnetNewCommand(_log, "console", "--name", "overwrite-test", "-o", "folderA")
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            string[] expectedFiles = new[] { "overwrite-test.csproj", "Program.cs" };

            commandResult
                .Should().Fail();

            foreach (string file in expectedFiles)
            {
                commandResult.Should().HaveStdErrContaining(file);
            }

            return Verify(commandResult.StdErr)
                .AddScrubber(output =>
                {
                    output = output.Replace(_fixture.HomeDirectory, "%HOME%");
                    //unify directory separators
                    output = output.Replace("\\", "/");
                    //order of files may vary, replace filename with placeholders
                    //filenames are verified above
                    foreach (string file in expectedFiles)
                    {
                        output = output.Replace(file, "%FILENAME%");
                    }
                });
        }

        [Fact]
        public Task CanShowWarning_WhenHostDataIsIncorrect()
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();
            string templateLocation = InstallTestTemplate("Invalid/InvalidHostData", _log, home, workingDirectory);

            CommandResult commandResult = new DotnetNewCommand(_log, "TestAssets.Invalid.InvalidHostData")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr();

            return Verify(commandResult.StdOut)
            .AddScrubber(output =>
            {
                //output contains path to host.json file - it is machine-specific.
                output.Replace($"{templateLocation}{Path.DirectorySeparatorChar}", "%TEMPLATE ROOT%");
                //details varies based on OS
                output.ScrubDetails();
            });
        }

        [Fact]
        public Task CanShowWarningIfPackageIsAvailableFromBuiltInSources()
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();
            new DotnetNewCommand(_log, "install", "Microsoft.DotNet.Common.ItemTemplates::6.0.100", "--force")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should().Pass();

            CommandResult commandResult = new DotnetNewCommand(_log, "gitignore")
                  .WithCustomHive(home)
                  .WithWorkingDirectory(workingDirectory)
                  .Execute();

            commandResult
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr();

            return Verify(commandResult.StdOut)
            .AddScrubber(output =>
            {
                output.ScrubByRegex("'Microsoft\\.DotNet\\.Common\\.ItemTemplates::[A-Za-z0-9.-]+' is available in", "'Microsoft.DotNet.Common.ItemTemplates::%VERSION%' is available in");
                output.ScrubByRegex("install Microsoft\\.DotNet\\.Common\\.ItemTemplates::[A-Za-z0-9.-]+", "install Microsoft.DotNet.Common.ItemTemplates::%VERSION%");
            });
        }

        [Fact]
        public Task CanShowError_OnTemplatesWithSameShortName()
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();
            string templateLocation = InstallTestTemplate("Invalid/SameShortName", _log, home, workingDirectory);

            CommandResult commandResult = new DotnetNewCommand(_log, "sameshortname")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult
                .Should()
                .Fail()
                .And.NotHaveStdOut();

            return Verify(commandResult.StdErr)
            .AddScrubber(output =>
            {
                //removes the delimiter line as we don't know the length of last columns containing paths above
                output.ScrubTableHeaderDelimiter();
                //removes the spaces after "Package" column header as we don't know the amount of spaces after it (depends on the paths above)
                output.ScrubByRegex("Package *", "Package");
                output = output.Replace(templateLocation, "%TEMPLATE LOCATION%");
            });
        }

        [Fact]
        public Task Constraints_Error_IfTemplateIsRestricted()
        {
            string customHivePath = CreateTemporaryFolder(folderName: "Home");
            InstallTestTemplate("Constraints/RestrictedTemplate", _log, customHivePath);

            CommandResult commandResult = new DotnetNewCommand(_log, "Constraints.RestrictedTemplate")
                  .WithCustomHive(customHivePath)
                  .Execute();

            commandResult
                .Should()
                .Fail();

            return Verify(commandResult.StdErr)
                .AddScrubber(output =>
                {
                    output.ScrubByRegex("\\-\\-debug\\:custom\\-hive [A-Za-z0-9\\-\\.\\\\\\/\\{\\}\\:_]+", "--debug:custom-hive %SETTINGS DIRECTORY%");
                    output.ScrubByRegex("dotnetcli \\(version: [A-Za-z0-9.-]+\\)", "dotnetcli (version: %VERSION%)");
                });
        }

        [Fact]
        public Task Constraints_CanIgnoreConstraints_WhenForceIsSpecified()
        {
            string customHivePath = CreateTemporaryFolder(folderName: "Home");
            InstallTestTemplate("Constraints/RestrictedTemplate", _log, customHivePath);

            CommandResult commandResult = new DotnetNewCommand(_log, "Constraints.RestrictedTemplate", "--force")
                  .WithCustomHive(customHivePath)
                  .Execute();

            commandResult
                .Should()
                .Pass();

            return Verify(commandResult.StdOut)
                .AddScrubber(output =>
                {
                    output.ScrubByRegex("dotnetcli \\(version: [A-Za-z0-9.-]+\\)", "dotnetcli (version: %VERSION%)");
                });
        }

        [Fact]
        public Task CannotInstantiateTemplateWhenFolderIsRemoved()
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDir = CreateTemporaryFolder();
            string templateLocation = Path.Combine(workingDir, "template");
            TestUtils.DirectoryCopy(GetTestTemplateLocation("TemplateWithSourceName"), templateLocation, true);

            new DotnetNewCommand(_log, "install", templateLocation)
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDir)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr();

            Directory.Delete(templateLocation, true);
            // Template should be removed from the template list, and it's unknown template now.
            CommandResult commandResult = new DotnetNewCommand(_log, "TestAssets.TemplateWithSourceName")
                .WithCustomHive(home)
                .Execute();

            commandResult
                .Should()
                .Fail();

            return Verify(commandResult.FormatOutputStreams())
                .AddScrubber(output =>
                {
                    // for Linux Verify.NET replaces sub path /tmp/ to be {TempPath} wrongly
                    output.Replace("{TempPath}", "/tmp/");
                    output.Replace(templateLocation, "%template location%");
                });
        }

        [Fact]
        public Task CanSuggestTypoCorrection_Template()
        {
            CommandResult commandResult = new DotnetNewCommand(_log, "cnsle")
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute();

            commandResult
                .Should()
                .Fail()
                .And.NotHaveStdOut();

            return Verify(commandResult.StdErr);
        }

        [Fact]
        public Task CanSuggestTypoCorrection_Command()
        {
            CommandResult commandResult = new DotnetNewCommand(_log, "uninstal")
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute();

            commandResult
                .Should()
                .Fail()
                .And.NotHaveStdOut();

            return Verify(commandResult.StdErr);
        }

        [Fact]
        public Task CanShowMessageInCaseShortNameConflict()
        {
            string customHivePath = CreateTemporaryFolder(folderName: "Home");
            InstallTestTemplate("TemplateWithSourceName", _log, customHivePath);
            InstallTestTemplate("TemplateWithConflictShortName", _log, customHivePath);

            CommandResult commandResult = new DotnetNewCommand(_log, "create", "TestAssets.TemplateWithSourceName")
                  .WithCustomHive(customHivePath)
                  .WithoutBuiltInTemplates()
                  .WithWorkingDirectory(CreateTemporaryFolder())
                  .Execute();

            commandResult
                .Should()
                .Pass();

            return Verify(commandResult.StdOut);
        }

        [Theory]
        [InlineData("TestAssets.TemplateWithRequiredParameters|--paramA|valA|--paramB|valB", "AB")]
        [InlineData("TestAssets.TemplateWithRequiredParameters|--paramA|valA|--paramB|valB|--paramC|valC|--enableC|true", "ABC")]
        public Task CanInstantiateTemplate_WithRequiredParams(string parameters, string setName)
        {
            string workingDirectory = CreateTemporaryFolder();
            string homeDirectory = CreateTemporaryFolder();
            InstallTestTemplate("TemplateWithRequiredParameters", _log, homeDirectory, workingDirectory);

            CommandResult commandResult = new DotnetNewCommand(_log, parameters.Split("|"))
                .WithCustomHive(homeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr();

            return Verify(File.ReadAllText(Path.Combine(workingDirectory, "Test.cs")))
                .UseParameters(setName);
        }

        [Theory]
        [InlineData("TestAssets.TemplateWithConditionalParameters|--paramA|true|--A_enabled", "A_Aenabled")]
        [InlineData("TestAssets.TemplateWithConditionalParameters|--paramA|true", "A")]
        [InlineData("TestAssets.TemplateWithConditionalParameters|--paramA|true|--paramB|true", "AB")]
        [InlineData("TestAssets.TemplateWithConditionalParameters|--paramA|true|--paramB|true|--A_enabled", "AB_Aenabled")]
        [InlineData("TestAssets.TemplateWithConditionalParameters|--paramA|true|--paramB|true|--A_enabled|--B_enabled", "AB_ABenabled")]
        public Task CanInstantiateTemplate_WithConditionallyEnabledParams(string parameters, string setName)
        {
            string workingDirectory = CreateTemporaryFolder();
            string homeDirectory = CreateTemporaryFolder();
            InstallTestTemplate("TemplateWithConditionalParameters", _log, homeDirectory, workingDirectory);

            CommandResult commandResult = new DotnetNewCommand(_log, parameters.Split("|"))
                .WithCustomHive(homeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr();

            return Verify(File.ReadAllText(Path.Combine(workingDirectory, "Test.cs")))
                .UseParameters(setName);
        }

        [Theory]
        [InlineData("TestAssets.TemplateWithRequiredParameters", "no-params")]
        [InlineData("TestAssets.TemplateWithRequiredParameters|--paramA|valA", "onlyA")]
        [InlineData("TestAssets.TemplateWithRequiredParameters|--paramA|valA|--paramB|valB|--enableC|true", "onlyAB")]
        [InlineData("TestAssets.TemplateWithRequiredParameters|--enableC|true", "no-params-C-enabled")]
        public Task CannotInstantiateTemplate_WithoutRequiredParams(string parameters, string setName)
        {
            string workingDirectory = CreateTemporaryFolder();
            string homeDirectory = CreateTemporaryFolder();
            InstallTestTemplate("TemplateWithRequiredParameters", _log, homeDirectory, workingDirectory);

            CommandResult commandResult = new DotnetNewCommand(_log, parameters.Split("|"))
                .WithCustomHive(homeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult
                .Should()
                .Fail();

            return Verify(commandResult.FormatOutputStreams())
                .UseParameters(setName);
        }
    }
}
