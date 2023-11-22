// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Logging;
using Microsoft.Build.UnitTests.Shared;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    public class UserConfigFile_Tests
    {
        [Fact]
        public void UserFileShouldImportInOuterBuild()
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                TransientTestFolder logFolder = env.CreateFolder(createFolder: true);
                string contentProjectFile = @"
<Project>

    <PropertyGroup>
        <TargetFramework>net6.0</TargetFramework>
    </PropertyGroup>

    <Target Name=""WriteValue"" AfterTargets=""Build"">
        <Message Text=""User value is: $(UserValue)"" Importance=""High"" />
    </Target>

</Project>";
                TransientTestFile projectFile = env.CreateFile(logFolder, "myProj.csproj", contentProjectFile);

                string contentUserConfigFile = @"
<?xml version=""1.0"" encoding=""utf-8""?>
<Project>
    <PropertyGroup>
        <UserValue>A User defined value</UserValue>
    </PropertyGroup>
</Project>
";
                TransientTestFile userFile = env.CreateFile(logFolder, "myProj.csproj.user", contentUserConfigFile);
                RunnerUtilities.ExecMSBuild($"{logFolder.Path} -flp:logfile={Path.Combine(logFolder.Path, "logFile.log")};verbosity=normal", out bool success);
                success.ShouldBeTrue();

                var logFilePath = Path.Combine(logFolder.Path, "logFile.log");
                string[] text = File.ReadAllLines(logFilePath);

                int checkForImport = Array.IndexOf(text, "User value is: A User defined value");
                checkForImport.ShouldBeGreaterThan(-1);

                int checkForWrongImport = Array.IndexOf(text, "User value is:");
                checkForWrongImport.ShouldBe(-1);
            }
        }
    }
}
