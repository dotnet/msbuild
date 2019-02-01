using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using System;
using System.IO;
using Xunit;
using Xunit.Abstractions;
using System.Reflection;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildANetCoreAppAndPassingALogger : SdkTest
    {
        public GivenThatWeWantToBuildANetCoreAppAndPassingALogger(ITestOutputHelper log) : base(log)
        {
        }

        [CoreMSBuildOnlyFact]
        public void It_collects_TargetFramework_version()
        {
            string targetFramework = "netcoreapp1.0";
            var testProject = new TestProject()
            {
                Name = "FrameworkTargetTelemetryTest",
                TargetFrameworks = targetFramework,
                IsSdkProject = true,
            };
            Type loggerType = typeof(LogTelemetryToStdOutForTest);
            var TelemetryTestLogger = new[]
                {
                    $"/Logger:{loggerType.FullName},{loggerType.GetTypeInfo().Assembly.Location}"
                };
            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .Restore(Log, testProject.Name, TelemetryTestLogger);

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            buildCommand
                .Execute(TelemetryTestLogger)
                .StdOut.Should()
                .Contain("{\"EventName\":\"targetframeworkeval\",\"Properties\":{\"TargetFrameworkVersion\":\".NETCoreApp,Version=v1.0\",\"UseWindowsForms\":\"null\",\"UseWPF\":\"null\"}");
        }

        [CoreMSBuildOnlyFact]
        public void It_collects_multi_TargetFramework_version()
        {
            string targetFramework = "net46;netcoreapp1.1";

            var testProject = new TestProject()
            {
                Name = "MultitargetTelemetry",
                TargetFrameworks = targetFramework,
                IsSdkProject = true,
            };
            Type loggerType = typeof(LogTelemetryToStdOutForTest);
            var TelemetryTestLogger = new[]
                {
                    $"/Logger:{loggerType.FullName},{loggerType.GetTypeInfo().Assembly.Location}"
                };
            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .Restore(Log, testProject.Name, TelemetryTestLogger);

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            buildCommand
                .Execute(TelemetryTestLogger)
                .StdOut.Should()
                .Contain("{\"EventName\":\"targetframeworkeval\",\"Properties\":{\"TargetFrameworkVersion\":\".NETFramework,Version=v4.6\",\"UseWindowsForms\":\"null\",\"UseWPF\":\"null\"}")
                .And
                .Contain("{\"EventName\":\"targetframeworkeval\",\"Properties\":{\"TargetFrameworkVersion\":\".NETCoreApp,Version=v1.1\",\"UseWindowsForms\":\"null\",\"UseWPF\":\"null\"}");
        }
    }
}
