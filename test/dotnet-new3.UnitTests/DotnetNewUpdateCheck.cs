// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;
using Xunit.Abstractions;

namespace dotnet_new3.UnitTests
{
    public class DotnetNewUpdateCheck
    {
        private readonly ITestOutputHelper _log;

        public DotnetNewUpdateCheck(ITestOutputHelper log)
        {
            _log = log;
        }

        [Fact]
        public void CanCheckForUpdate()
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
                .And.NotHaveStdOutContaining("Determining projects to restore...")
                .And.HaveStdOutContaining("console")
                .And.HaveStdOutContaining("classlib");

            new DotnetNewCommand(_log, "--update-check")
                .WithCustomHive(home)
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutContaining("An update for template package 'Microsoft.DotNet.Common.ProjectTemplates.5.0::5.0.0' is available.");
        }

        [Fact]
        public void DoesNotShowUpdatesWhenAllTemplatesAreUpToDate()
        {
            var home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            string templateLocation = Helpers.InstallTestTemplate("TemplateResolution/DifferentLanguagesGroup/BasicFSharp", _log, workingDirectory, home);
            new DotnetNewCommand(_log, "-i", "Microsoft.DotNet.Common.ProjectTemplates.5.0")
                .WithCustomHive(home)
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.NotHaveStdOutContaining("Determining projects to restore...")
                .And.HaveStdOutContaining("console")
                .And.HaveStdOutContaining("classlib");

            new DotnetNewCommand(_log, "--update-check")
                .WithCustomHive(home)
                .WithWorkingDirectory(TestUtils.CreateTemporaryFolder())
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOut("All template packages are up-to-date.");
        }
    }
}
