using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToCrossPublish : SdkTest
    {
        public GivenThatWeWantToCrossPublish(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void There_should_be_no_unresolved_conflicts()
        {
            var testProject = new TestProject()
            {
                Name = "CrossPublish",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
                RuntimeIdentifier = "centos.7-x64"
            };

            testProject.PackageReferences.Add(new TestPackageReference("System.Threading", "4.3.0"));

            var testProjectInstance = _testAssetsManager.CreateTestProject(testProject);

            var publishCommand = new PublishCommand(testProjectInstance);

            //  Shouldn't have messages like the following:
            //  Encountered conflict between 'CopyLocal:C:\git\dotnet-sdk\artifacts\.nuget\packages\runtime.any.system.runtime\4.3.0\lib\netstandard1.5\System.Runtime.dll'
            //  and 'CopyLocal:C:\git\dotnet-sdk\artifacts\.nuget\packages\runtime.linux-x64.microsoft.netcore.app\2.0.6\runtimes\linux-x64\lib\netcoreapp2.0\System.Runtime.dll'.
            //  Could not determine a winner because 'CopyLocal:C:\git\dotnet-sdk\artifacts\.nuget\packages\runtime.linux-x64.microsoft.netcore.app\2.0.6\runtimes\linux-x64\lib\netcoreapp2.0\System.Runtime.dll'
            //  is not an assembly.

            publishCommand
                .Execute("/v:normal")
                .Should()
                .Pass()
                .And.NotHaveStdOutContaining("Could not determine");
        }
    }
}
