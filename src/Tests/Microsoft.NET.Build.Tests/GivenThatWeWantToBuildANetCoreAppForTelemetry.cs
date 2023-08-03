// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildANetCoreAppAndPassingALogger : SdkTest
    {
        public GivenThatWeWantToBuildANetCoreAppAndPassingALogger(ITestOutputHelper log) : base(log)
        {
        }

        [CoreMSBuildOnlyFact]
        public void It_collects_TargetFramework_version_and_other_properties()
        {
            string targetFramework = ToolsetInfo.CurrentTargetFramework;
            var testProject = new TestProject()
            {
                Name = "FrameworkTargetTelemetryTest",
                TargetFrameworks = targetFramework,
            };
            Type loggerType = typeof(LogTelemetryToStdOutForTest);
            var TelemetryTestLogger = new[]
                {
                    $"/Logger:{loggerType.FullName},{loggerType.GetTypeInfo().Assembly.Location}"
                };
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute(TelemetryTestLogger)
                .StdOut.Should()
                .Contain($"{{\"EventName\":\"targetframeworkeval\",\"Properties\":{{\"TargetFrameworkVersion\":\".NETCoreApp,Version=v{ToolsetInfo.CurrentTargetFrameworkVersion}\",\"RuntimeIdentifier\":\"null\",\"SelfContained\":\"null\",\"UseApphost\":\"null\",\"OutputType\":\"Library\",\"UseArtifactsOutput\":\"null\",\"ArtifactsPathLocationType\":\"null\"}}");
        }

        [CoreMSBuildOnlyFact]
        public void It_collects_multi_TargetFramework_version_and_other_properties()
        {
            string targetFramework = $"net46;{ToolsetInfo.CurrentTargetFramework}";

            var testProject = new TestProject()
            {
                Name = "MultitargetTelemetry",
                TargetFrameworks = targetFramework,
            };
            Type loggerType = typeof(LogTelemetryToStdOutForTest);
            var TelemetryTestLogger = new[]
                {
                    $"/Logger:{loggerType.FullName},{loggerType.GetTypeInfo().Assembly.Location}"
                };
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);

            var result = buildCommand
                .Execute(TelemetryTestLogger);

            result
                .StdOut.Should()
                .Contain(
                    "{\"EventName\":\"targetframeworkeval\",\"Properties\":{\"TargetFrameworkVersion\":\".NETFramework,Version=v4.6\",\"RuntimeIdentifier\":\"null\",\"SelfContained\":\"null\",\"UseApphost\":\"null\",\"OutputType\":\"Library\",\"UseArtifactsOutput\":\"null\",\"ArtifactsPathLocationType\":\"null\"}")
                .And
                .Contain(
                    $"{{\"EventName\":\"targetframeworkeval\",\"Properties\":{{\"TargetFrameworkVersion\":\".NETCoreApp,Version=v{ToolsetInfo.CurrentTargetFrameworkVersion}\",\"RuntimeIdentifier\":\"null\",\"SelfContained\":\"null\",\"UseApphost\":\"null\",\"OutputType\":\"Library\",\"UseArtifactsOutput\":\"null\",\"ArtifactsPathLocationType\":\"null\"}}");
        }
    }
}
