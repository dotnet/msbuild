// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Text.RegularExpressions;
using ApprovalTests;
using FluentAssertions;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;

namespace Dotnet_new3.IntegrationTests
{
    public partial class DotnetNewInstantiate
    {
        [Fact]
        public void CannotInstantiateUnknownTemplate()
        {
            var commandResult = new DotnetNewCommand(_log, "webapp")
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute();

            commandResult
                .Should()
                .Fail()
                .And.NotHaveStdOut();

            Approvals.Verify(commandResult.StdErr);
        }

        [Fact]
        public void CannotInstantiateTemplateWithUnknownLanguage()
        {
            var commandResult = new DotnetNewCommand(_log, "console", "--language", "D#")
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute();

            commandResult
                .Should()
                .Fail()
                .And.NotHaveStdOut();

            Approvals.Verify(commandResult.StdErr);
        }

        [Fact]
        public void CannotInstantiateTemplate_WhenAmbiguousLanguageChoice()
        {
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            Helpers.InstallTestTemplate("TemplateResolution/DifferentLanguagesGroup/BasicFSharp", _log, workingDirectory, home);
            Helpers.InstallTestTemplate("TemplateResolution/DifferentLanguagesGroup/BasicVB", _log, workingDirectory, home);

            var commandResult = new DotnetNewCommand(_log, "basic")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult
                .Should()
                .Fail()
                .And.NotHaveStdOut();

            Approvals.Verify(commandResult.StdErr);
        }

        [Fact]
        public void CannotInstantiateTemplate_WhenAmbiguousShortNameChoice()
        {
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            string templateOneLocation = Helpers.InstallTestTemplate("TemplateResolution/SameShortName/BasicFSharp", _log, workingDirectory, home);
            string templateTwoLocation = Helpers.InstallTestTemplate("TemplateResolution/SameShortName/BasicVB", _log, workingDirectory, home);

            var commandResult = new DotnetNewCommand(_log, "basic")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult
                .Should()
                .Fail()
                .And.NotHaveStdOut();

            Approvals.Verify(commandResult.StdErr, (output) =>
            {
                //package locaions are machine specific so we cannot use them in approval tests
                //replace them with directory name
                var finalOutput = output.Replace(templateOneLocation, Path.GetFileName(templateOneLocation)).Replace(templateTwoLocation, Path.GetFileName(templateTwoLocation));
                //removes the delimiter line as we don't know the length of last columns containing paths above
                finalOutput = Regex.Replace(finalOutput, "-+[ -]*", "%delimiter%");
                //replace the "Package" column header as we don't know the amount of spaces after it (depends on the paths above)
                finalOutput = Regex.Replace(finalOutput, "Package *", "Package");
                return finalOutput;
            });
        }

        [Fact]
        public void CannotInstantiateTemplate_WhenFullNameIsUsed()
        {
            string workingDirectory = TestUtils.CreateTemporaryFolder();

            var commandResult = new DotnetNewCommand(_log, "Console App")
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult
                .Should().Fail()
                .And.NotHaveStdOut();

            Approvals.Verify(commandResult.StdErr);
        }

        [Fact]
        public void CannotInstantiateTemplate_WhenParameterIsInvalid()
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

            Approvals.Verify(commandResult.StdErr);
        }

        [Fact]
        public void CannotInstantiateTemplate_WhenChoiceParameterValueIsInvalid()
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

            Approvals.Verify(commandResult.StdErr);
        }

        [Fact]
        public void CannotInstantiateTemplate_WhenChoiceParameterValueIsNotComplete()
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

            Approvals.Verify(commandResult.StdErr);
        }

        [Fact]
        public void CannotInstantiateTemplate_OnMultipleParameterErrors()
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

            Approvals.Verify(commandResult.StdErr);
        }

        [Fact]
        public void CannotInstantiateTemplate_WhenPrecedenceIsSame()
        {
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            var templateOneLocation = Helpers.InstallTestTemplate("TemplateResolution/SamePrecedenceGroup/BasicTemplate1", _log, workingDirectory, home);
            var templateTwoLocation = Helpers.InstallTestTemplate("TemplateResolution/SamePrecedenceGroup/BasicTemplate2", _log, workingDirectory, home);

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

            Approvals.Verify(commandResult.StdErr, (output) =>
            {
                //package locaions are machine specific so we cannot use them in approval tests
                var finalOutput = output.Replace(templateOneLocation, "").Replace(templateTwoLocation, "");
                //removes the delimiter line as we don't know the length of last columns containing paths above
                finalOutput = Regex.Replace(finalOutput, "-+[ -]*", "");
                //removes the "Package" column header as we don't know the amount of spaces after it (depends on the paths above)
                finalOutput = Regex.Replace(finalOutput, "Package *", "");
                return finalOutput;
            });
        }

        [Fact]
        public void CannotOverwriteFilesWithoutForce()
        {
            string workingDirectory = TestUtils.CreateTemporaryFolder();

            new DotnetNewCommand(_log, "console", "--name", "overwrite-test")
                .WithCustomHive(_fixture.HomeDirectory)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("The template \"Console App\" was created successfully.");

            var commandResult = new DotnetNewCommand(_log, "console", "--name", "overwrite-test")
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

            Approvals.Verify(commandResult.StdErr, (output) =>
            {
                //unify directory separators
                output = output.Replace("\\", " / ");

                //order of files may vary, replace filename with placeholders
                //filenames are verified above
                foreach (var file in expectedFiles)
                {
                    output = output.Replace(file, "%FILENAME%");
                }
                return output;
            });
        }

        [Fact]
        public void CanShowWarning_WhenHostDataIsIncorrect()
        {
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            var templateLocation = Helpers.InstallTestTemplate("Invalid/InvalidHostData", _log, workingDirectory, home);

            var commandResult = new DotnetNewCommand(_log, "TestAssets.Invalid.InvalidHostData")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr();

            Approvals.Verify(commandResult.StdOut, (output) =>
            {
                //output contains path to host.json file - it is machine-specific.
                output = output.Replace($"{templateLocation}{Path.DirectorySeparatorChar}", "<%TEMPLATE ROOT%>");
                //details varies based on OS
                output = Regex.Replace(output, "(Details: )([^\\r\\n]*)", $"$1<%DETAILS%>");
                return output;
            });
        }

    }
}
