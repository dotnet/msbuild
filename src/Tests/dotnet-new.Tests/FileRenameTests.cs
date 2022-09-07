// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Cli.New.IntegrationTests
{
    public class FileRenameTests : BaseIntegrationTest
    {
        private readonly ITestOutputHelper _log;

        public FileRenameTests(ITestOutputHelper log) : base(log)
        {
            _log = log;
        }

        [Fact]
        public void CanUseFileRenameWithNowGenerator()
        {
            string home = CreateTemporaryFolder(folderName: "Home");
            string workingDirectory = CreateTemporaryFolder();
            InstallTestTemplate("TemplateWithFileRenameDate", _log, home, workingDirectory);
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
