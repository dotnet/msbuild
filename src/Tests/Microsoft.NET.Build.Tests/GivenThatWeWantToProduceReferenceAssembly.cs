// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToProduceReferenceAssembly : SdkTest
    {
        public GivenThatWeWantToProduceReferenceAssembly(ITestOutputHelper log) : base(log)
        { }

        [RequiresMSBuildVersionTheory("16.8.0")]
        [InlineData("netcoreapp3.1", false)]
        [InlineData(ToolsetInfo.CurrentTargetFramework, true)]
        public void It_produces_ref_assembly_for_appropriate_frameworks(string targetFramework, bool expectedExists)
        {
            TestProject testProject = new()
            {
                Name = "ProduceRefAssembly",
                IsExe = true,
                TargetFrameworks = targetFramework,
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute()
                .Should()
                .Pass();
            var filePath = Path.Combine(testAsset.Path, testProject.Name, "obj", "Debug", targetFramework, "ref", $"{testProject.Name}.dll");
            File.Exists(filePath).Should().Be(expectedExists);
        }
    }
}
