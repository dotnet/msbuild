// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.NET.TestFramework.Assertions;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;
using Xunit.Abstractions;

namespace Dotnet_new3.IntegrationTests
{
    public class FileRenameTests
    {
        private readonly ITestOutputHelper _log;

        public FileRenameTests(ITestOutputHelper log)
        {
            _log = log;
        }

        [Fact]
        public void CanUseFileRenameWithNowGenerator()
        {
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            Helpers.InstallTestTemplate("TemplateWithFileRenameDate", _log, workingDirectory, home);
            new DotnetNewCommand(_log, "TestAssets.TemplateWithFileRenameDate", "--migrationName", "MyTestName")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining("The template \"TestAssets.TemplateWithFileRenameDate\" was created successfully.");

            DirectoryInfo directoryInfo = new DirectoryInfo(workingDirectory);
            Assert.Matches("\\d{8}_mytestname.cs", directoryInfo.EnumerateFiles().Single().Name);
        }
    }
}
