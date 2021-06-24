using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using System;
using System.IO;
using Xunit.Abstractions;
using System.Reflection;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToCollectExceptionTelemetry : SdkTest
    {
        public GivenThatWeWantToCollectExceptionTelemetry(ITestOutputHelper log) : base(log)
        {
        }

        [CoreMSBuildAndWindowsOnlyFact]
        public void It_collects_Exception()
        {
            Type loggerType = typeof(LogTelemetryToStdOutForTest);
            string telemetryTestLogger = $"/Logger:{loggerType.FullName},{loggerType.GetTypeInfo().Assembly.Location}";

            var testAsset = _testAssetsManager.CopyTestAsset("HelloWorld").WithSource();

            var mSBuildCommand = new MSBuildCommand(Log, "GenerateToolsSettingsFileFromBuildProperty", Path.Combine(testAsset.TestRoot));

            string invalidPath = @"\\.\COM56";
            string causeTaskToFail = $"/p:_ToolsSettingsFilePath={invalidPath}";

            mSBuildCommand
                .Execute(telemetryTestLogger, causeTaskToFail)
                .StdOut.Should()
                .Contain("\"EventName\":\"taskBaseCatchException\",\"Properties\":{\"exceptionType\":\"System.IO.FileNotFoundException\"")
                .And.Contain("detail");
        }
    }
}
