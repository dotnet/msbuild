// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.TemplateEngine.TestHelper;
using VerifyTests;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Dotnet_new3.IntegrationTests
{
    [UsesVerify]
    public class DotnetNewCommandTests : IClassFixture<VerifySettingsFixture>
    {
        private readonly VerifySettings _verifySettings;
        private readonly ITestOutputHelper _log;

        public DotnetNewCommandTests(VerifySettingsFixture verifySettings, ITestOutputHelper log)
        {
            _verifySettings = verifySettings.Settings;
            _log = log;
        }

        [Fact]
        public Task CanShowBasicInfo()
        {
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();

            var commandResult = new DotnetNewCommand(_log)
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult.Should()
                .ExitWith(0).And.NotHaveStdErr();

            return Verifier.Verify(commandResult.StdOut, _verifySettings);
        }

        [Fact]
        public void CanShowFullCuratedList()
        {
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();

            Helpers.InstallNuGetTemplate("microsoft.dotnet.wpf.projecttemplates", _log, home, workingDirectory);
            Helpers.InstallNuGetTemplate("microsoft.dotnet.winforms.projecttemplates", _log, home, workingDirectory);
            Helpers.InstallNuGetTemplate("microsoft.dotnet.web.projecttemplates.6.0", _log, home, workingDirectory);

            new DotnetNewCommand(_log)
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0).And.NotHaveStdErr()
                .And.HaveStdOutMatching("Template Name\\s+Short Name\\s+Language\\s+Tags")
                .And.HaveStdOutMatching("Console App\\s+console\\s+\\[C#\\],F#,VB\\s+Common/Console")
                .And.HaveStdOutMatching("Class Library\\s+classlib\\s+\\[C#\\],F#,VB\\s+Common/Library")
                .And.NotHaveStdOutMatching("dotnet gitignore file\\s+gitignore\\s+Config")
                .And.NotHaveStdOutContaining("webapi").And.NotHaveStdOutContaining("winformslib");
        }
    }
}
