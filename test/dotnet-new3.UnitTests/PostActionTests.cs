// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using FluentAssertions;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;
using Xunit.Abstractions;

namespace dotnet_new3.UnitTests
{
    public class PostActionTests
    {
        private readonly ITestOutputHelper _log;

        public PostActionTests(ITestOutputHelper log)
        {
            _log = log;
        }

        [Theory]
        [InlineData("PostActions/RestoreNuGet/Basic", "TestAssets.PostActions.RestoreNuGet.Basic")]
        [InlineData("PostActions/RestoreNuGet/BasicWithFiles", "TestAssets.PostActions.RestoreNuGet.BasicWithFiles")]
        public void Restore_Basic(string templateLocation, string templateName)
        {
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            Helpers.InstallTestTemplate(templateLocation, _log, workingDirectory, home);

            new DotnetNewCommand(_log, templateName)
                .WithWorkingDirectory(workingDirectory)
                .WithEnvironmentVariable(TestUtils.HomeEnvironmentVariableName, home)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining($"The template \"{templateName}\" was created successfully.")
                .And.HaveStdOutContaining("Restore succeeded.")
                .And.NotHaveStdOutContaining("Manual instructions: Run 'dotnet restore'");

            new DotnetCommand(_log, "build", "--no-restore")
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr();
        }
    }
}
