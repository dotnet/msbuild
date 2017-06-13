using FluentAssertions;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Test.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace EndToEnd
{
    public class GivenNetFrameworkSupportsNetStandard2 : TestBase
    {
        [WindowsOnlyFact]
        public void ANET461ProjectCanReferenceANETStandardProject()
        {
            var _testInstance = TestAssets.Get(TestAssetKinds.DesktopTestProjects, "NETFrameworkReferenceNETStandard20")
                .CreateInstance()
                .WithSourceFiles();

            string projectDirectory = Path.Combine(_testInstance.Root.FullName, "TestApp");

            new RestoreCommand()
                .WithWorkingDirectory(projectDirectory)
                .Execute()
                .Should().Pass();

            new BuildCommand()
                .WithWorkingDirectory(projectDirectory)
                .Execute()
                .Should().Pass();

            new RunCommand()
                    .WithWorkingDirectory(projectDirectory)
                    .ExecuteWithCapturedOutput()
                    .Should().Pass()
                         .And.HaveStdOutContaining("This string came from the test library!");

        }
    }
}
