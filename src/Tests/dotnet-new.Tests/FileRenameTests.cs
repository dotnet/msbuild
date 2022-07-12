// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Linq;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.New.Tests
{
    public class FileRenameTests : SdkTest
    {
        private readonly ITestOutputHelper _log;

        public FileRenameTests(ITestOutputHelper log) : base(log)
        {
            _log = log;
        }

        [Fact]
        public void CanUseFileRenameWithNowGenerator()
        {
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            Helpers.InstallTestTemplate("TemplateWithFileRenameDate", _log, home, workingDirectory);
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
