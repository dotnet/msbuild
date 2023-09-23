// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantACustomTFM : SdkTest
    {
        public GivenThatWeWantACustomTFM(ITestOutputHelper log) : base(log)
        { }

        [Fact]
        public void It_imports_custom_parsing_targets()
        {
            var targetFramework = ToolsetInfo.CurrentTargetFramework;
            var runtimeIdentifier = $"{ToolsetInfo.LatestMacRuntimeIdentifier}-x64";
            TestProject testProject = new()
            {
                Name = "CustomTFMProject",
                IsExe = true,
                TargetFrameworks = $"{targetFramework}-{runtimeIdentifier}"
            };

            testProject.AdditionalProperties["BeforeTargetFrameworkInferenceTargets"] = @"$(MSBuildProjectDirectory)\CustomTargetFramework.targets";

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            File.WriteAllText(Path.Combine(testAsset.TestRoot, testProject.Name, "CustomTargetFramework.targets"), $@"
<Project>
  <PropertyGroup>
    <RuntimeIdentifier>$(TargetFramework.Split('-')[1])-$(TargetFramework.Split('-')[2])</RuntimeIdentifier>
    <TargetFramework>$(TargetFramework.Split('-')[0])</TargetFramework>
  </PropertyGroup>
</Project>
".Replace('`', '"'));

            var expectedValues = new Dictionary<string, string>
            {
                { "TargetFramework", targetFramework },
                { "TargetFrameworkIdentifier", ".NETCoreApp" },
                { "TargetFrameworkVersion", $"v{ToolsetInfo.CurrentTargetFrameworkVersion}" },
                { "RuntimeIdentifier", runtimeIdentifier }
            };

            foreach (var property in expectedValues.Keys)
            {
                var getValuesCommand = new GetValuesCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name),
                    targetFramework, property, GetValuesCommand.ValueType.Property)
                {
                    Configuration = "Debug"
                };
                getValuesCommand
                    .Execute()
                    .Should()
                    .Pass();

                var values = getValuesCommand.GetValues();
                values.Count.Should().Be(1);
                values[0].Trim().Should().Be(expectedValues[property]);
            }
        }
    }
}
