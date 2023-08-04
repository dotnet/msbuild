// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
                RuntimeIdentifier = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "linux-x64" : "win-x64"
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
