// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.ToolPack.Tests
{
    public class GivenThatWeWantToTestPackAToolProject : SdkTest
    {
        public GivenThatWeWantToTestPackAToolProject(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void When_app_project_reference_a_library_it_flows_to_test_project()
        {
            TestAsset testAsset = _testAssetsManager
                .CopyTestAsset("PortableToolWithTestProject")
                .WithSource();

            var appProjectDirectory = Path.Combine(testAsset.TestRoot, "Test");
            var testCommand = new DotnetCommand(Log, "test", appProjectDirectory);
            testCommand
                .Execute()
                .Should()
                .Pass()
                .And
                .NotHaveStdOutContaining("Could not load file or assembly");
        }
    }
}
