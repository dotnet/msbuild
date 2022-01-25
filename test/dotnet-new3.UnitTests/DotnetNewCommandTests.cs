// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using FluentAssertions;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;
using Xunit.Abstractions;

namespace Dotnet_new3.IntegrationTests
{
    public class DotnetNewCommandTests
    {
        private readonly ITestOutputHelper _log;

        public DotnetNewCommandTests(ITestOutputHelper log)
        {
            _log = log;
        }

        [Fact]
        public void CanShowBasicInfo()
        {
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();

            var commandResult = new DotnetNewCommand(_log)
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult.Should()
                .ExitWith(0).And.NotHaveStdErr();

            ApprovalTests.Approvals.Verify(commandResult.StdOut);
        }

        [Fact]
        public void CanShowFullCuratedList()
        {
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();

            Helpers.InstallNuGetTemplate("microsoft.dotnet.wpf.projecttemplates", _log, workingDirectory, home);
            Helpers.InstallNuGetTemplate("microsoft.dotnet.winforms.projecttemplates", _log, workingDirectory, home);
            Helpers.InstallNuGetTemplate("microsoft.dotnet.web.projecttemplates.6.0", _log, workingDirectory, home);

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
