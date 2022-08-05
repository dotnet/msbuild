// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.TemplateEngine.TestHelper;
using VerifyTests;
using VerifyXunit;
using Xunit;

namespace Microsoft.DotNet.New.Tests
{
    [UsesVerify]
    [Collection("Verify Tests")]
    public partial class DotnetNewInstantiate
    {
        [Fact]
        public Task CannotInstantiateUnknownTemplate()
        {
            var commandResult = new DotnetNewCommand(_log, "unknownapp")
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
        public Task CannotInstantiateTemplateWithUnknownLanguage()
        {
            var commandResult = new DotnetNewCommand(_log, "console", "--language", "D#")
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
        public Task CannotInstantiateTemplateWithUnknownType()
        {
            var commandResult = new DotnetNewCommand(_log, "console", "--type", "item")
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
        public Task CannotInstantiateTemplate_WhenAmbiguousLanguageChoice()
        {
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            Helpers.InstallTestTemplate("TemplateResolution/DifferentLanguagesGroup/BasicFSharp", _log, home, workingDirectory);
            Helpers.InstallTestTemplate("TemplateResolution/DifferentLanguagesGroup/BasicVB", _log, home, workingDirectory);

            var commandResult = new DotnetNewCommand(_log, "basic")
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
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            string templateOneLocation = Helpers.InstallTestTemplate("TemplateResolution/SameShortName/BasicFSharp", _log, home, workingDirectory);
            string templateTwoLocation = Helpers.InstallTestTemplate("TemplateResolution/SameShortName/BasicVB", _log, home, workingDirectory);

            var commandResult = new DotnetNewCommand(_log, "basic")
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
            string workingDirectory = TestUtils.CreateTemporaryFolder();

            var commandResult = new DotnetNewCommand(_log, "Console App")
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult
                .Should().Fail()
                .And.NotHaveStdOut();

            return Verify(commandResult.StdErr);
        }

        [Fact]
        public Task CannotInstantiateTemplate_WhenParameterIsInvalid()
        {
            string workingDirectory = TestUtils.CreateTemporaryFolder();

            var commandResult = new DotnetNewCommand(_log, "console", "--fake")
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
            string workingDirectory = TestUtils.CreateTemporaryFolder();

            var commandResult = new DotnetNewCommand(_log, "console", "--framework", "fake")
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
            string workingDirectory = TestUtils.CreateTemporaryFolder();

            var commandResult = new DotnetNewCommand(_log, "console", "--framework", "netcoreapp")
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
            string workingDirectory = TestUtils.CreateTemporaryFolder();

            var commandResult = new DotnetNewCommand(_log, "console", "--framework", "netcoreapp", "--fake")
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
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            var templateOneLocation = Helpers.InstallTestTemplate("TemplateResolution/SamePrecedenceGroup/BasicTemplate1", _log, home, workingDirectory);
            var templateTwoLocation = Helpers.InstallTestTemplate("TemplateResolution/SamePrecedenceGroup/BasicTemplate2", _log, home, workingDirectory);

            var commandResult = new DotnetNewCommand(_log, "basic")
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
            await MultiValueChoiceParameterConditionsExecutor(new[] { "TestAssets.TemplateWithMultiValueChoice", "--Platform", "MacOS", "--Platform", "iOS" });
            await MultiValueChoiceParameterConditionsExecutor(new[] { "TestAssets.TemplateWithMultiValueChoice", "--Platform", "MacOS", "iOS" });
        }

        private Task MultiValueChoiceParameterConditionsExecutor(string[] args)
        {
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            Helpers.InstallTestTemplate("TemplateWithMultiValueChoice", _log, home, workingDirectory);

            var commandResult = new DotnetNewCommand(_log, args)
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
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            Helpers.InstallTestTemplate("TemplateWithMultiValueChoice", _log, home, workingDirectory);

            var commandResult = new DotnetNewCommand(_log, "TestAssets.TemplateWithMultiValueChoice", "--Platform", "")
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
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            Helpers.InstallTestTemplate("TemplateWithConditionalParameters", _log, home, workingDirectory);

            var commandResult = new DotnetNewCommand(
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
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            Helpers.InstallTestTemplate("TemplateWithMultiValueChoice", _log, home, workingDirectory);

            var commandResult = new DotnetNewCommand(_log, "TestAssets.TemplateWithMultiValueChoice", "--Platform", "", "--Platform", "MacOS")
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
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            string home = TestUtils.CreateTemporaryFolder("Home");
            Helpers.InstallTestTemplate($"TemplateConditionalProcessing", _log, home, workingDirectory);

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
                actualFiles.Select(
                    async (file) =>
                    await VerifyFile(file)
                    .UseMethodName($"CanInstantiateTemplate_ConditionalProcessing_{Path.GetFileName(file)}")
                    .UseExtension("txt")
                    ));
        }

        [Fact]
        public Task DryRunRespectsTargetPathAndOutputDir()
        {
            const string _OUT_FOLDER = "folderF";
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            Helpers.InstallTestTemplate("TemplateWithSourceNameAndCustomTargetPath", _log, home, workingDirectory);

            var commandResult = new DotnetNewCommand(_log, "TestAssets.TemplateWithSourceNameAndCustomTargetPath", "-o", _OUT_FOLDER, "--dry-run")
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
                    foreach (var file in expectedFiles)
                    {
                        output = output.Replace(file, "%FILENAME%");
                    }
                });
        }

        [Fact]
        public Task CannotOverwriteFilesWithoutForce()
        {
            string workingDirectory = TestUtils.CreateTemporaryFolder();

            new DotnetNewCommand(_log, "console", "--name", "overwrite-test", "-o", "folderA")
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("The template \"Console App\" was created successfully.");

            var commandResult = new DotnetNewCommand(_log, "console", "--name", "overwrite-test", "-o", "folderA")
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            string[] expectedFiles = new[] { "overwrite-test.csproj", "Program.cs" };

            commandResult
                .Should().Fail();

            foreach (var file in expectedFiles)
            {
                commandResult.Should().HaveStdErrContaining(file);
            }

            return Verify(commandResult.StdErr)
                .AddScrubber(output =>
                {
                    //unify directory separators
                    output = output.Replace("\\", "/");
                    //order of files may vary, replace filename with placeholders
                    //filenames are verified above
                    foreach (var file in expectedFiles)
                    {
                        output = output.Replace(file, "%FILENAME%");
                    }
                });
        }

        [Fact]
        public Task CanShowWarning_WhenHostDataIsIncorrect()
        {
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            var templateLocation = Helpers.InstallTestTemplate("Invalid/InvalidHostData", _log, home, workingDirectory);

            var commandResult = new DotnetNewCommand(_log, "TestAssets.Invalid.InvalidHostData")
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
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            new DotnetNewCommand(_log, "install", "Microsoft.DotNet.Common.ItemTemplates::6.0.100", "--force")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should().Pass();

            var commandResult = new DotnetNewCommand(_log, "gitignore")
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
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            string templateLocation = Helpers.InstallTestTemplate("Invalid/SameShortName", _log, home, workingDirectory);

            var commandResult = new DotnetNewCommand(_log, "sameshortname")
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
            var customHivePath = TestUtils.CreateTemporaryFolder();
            Helpers.InstallTestTemplate("Constraints/RestrictedTemplate", _log, customHivePath);

            var commandResult = new DotnetNewCommand(_log, "Constraints.RestrictedTemplate")
                  .WithCustomHive(customHivePath)
                  .Execute();

            commandResult
                .Should()
                .Fail();

            return Verify(commandResult.StdErr)
                .AddScrubber(output =>
                {
                    output.ScrubByRegex("\\-\\-debug\\:custom\\-hive [A-Za-z0-9\\-\\.\\\\\\/\\{\\}\\:_]+", "--debug:custom-hive %SETTINGS DIRECTORY%");
                    output.ScrubByRegex("dotnetcli \\(version: v[A-Za-z0-9.-]+\\)", "dotnetcli (version: v%VERSION%)");
                });
        }

        [Fact]
        public Task Constraints_CanIgnoreConstraints_WhenForceIsSpecified()
        {
            var customHivePath = TestUtils.CreateTemporaryFolder();
            Helpers.InstallTestTemplate("Constraints/RestrictedTemplate", _log, customHivePath);

            var commandResult = new DotnetNewCommand(_log, "Constraints.RestrictedTemplate", "--force")
                  .WithCustomHive(customHivePath)
                  .Execute();

            commandResult
                .Should()
                .Pass();

            return Verify(commandResult.StdOut)
                .AddScrubber(output =>
                {
                    output.ScrubByRegex("dotnetcli \\(version: v[A-Za-z0-9.-]+\\)", "dotnetcli (version: v%VERSION%)");
                });
        }

        [Fact]
        public Task CannotInstantiateTemplateWhenFolderIsRemoved()
        {
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDir = TestUtils.CreateTemporaryFolder();
            string templateLocation = Path.Combine(workingDir, "template");
            TestUtils.DirectoryCopy(TestUtils.GetTestTemplateLocation("TemplateWithSourceName"), templateLocation, true);

            new DotnetNewCommand(_log, "install", templateLocation)
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDir)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr();

            Directory.Delete(templateLocation, true);
            // Template should be removed from the template list, and it's unknown template now.
            var commandResult = new DotnetNewCommand(_log, "TestAssets.TemplateWithSourceName")
                .WithCustomHive(home)
                .Execute();

            commandResult
                .Should()
                .Fail();

            string testExecDirPatternForVerify = $"{{SolutionDirectory}}artifacts(\\\\|\\/)tmp(\\\\|\\/){TestUtils.Configuration}";
            return Verify(commandResult.FormatOutputStreams())
                .UniqueForOSPlatform()
                .ScrubInlineGuids()
                .AddScrubber(output =>
                {
                    // for Linux Verify.NET replaces sub path /tmp/ to be {TempPath} wrongly
                    output.Replace("{TempPath}", "/tmp/");
                    output.ScrubByRegex(testExecDirPatternForVerify, "%Test Execution Direcotry%");
                });
                
        }

    }
}
