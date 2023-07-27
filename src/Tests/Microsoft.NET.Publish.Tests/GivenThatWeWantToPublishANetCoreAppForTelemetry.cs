// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Microsoft.NET.Build.Tests;

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToPublishANetCoreAppForTelemetry : SdkTest
    {
        public GivenThatWeWantToPublishANetCoreAppForTelemetry(ITestOutputHelper log) : base(log)
        {
        }

        [CoreMSBuildOnlyTheory]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void It_collects_empty_Trimmer_SingleFile_ReadyToRun_Aot_publishing_properties(string targetFramework)
        {
            Type loggerType = typeof(LogTelemetryToStdOutForTest);
            var TelemetryTestLogger = new[]
                {
                    "--property:SelfContained=true",
                    $"/Logger:{loggerType.FullName},{loggerType.GetTypeInfo().Assembly.Location}"
                };

            var testProject = CreateTestProject(targetFramework, "PlainProject");
            var testProjectInstance = _testAssetsManager.CreateTestProject(testProject);
            var publishCommand = new PublishCommand(testProjectInstance);
            publishCommand.Execute(TelemetryTestLogger).StdOut.Should().Contain(
                "{\"EventName\":\"PublishProperties\",\"Properties\":{\"PublishReadyToRun\":\"null\",\"PublishTrimmed\":\"null\",\"PublishSingleFile\":\"null\",\"PublishAot\":\"null\",\"PublishProtocol\":\"null\"}");
        }

        [CoreMSBuildOnlyTheory]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void It_collects_Trimmer_SingleFile_ReadyToRun_publishing_properties(string targetFramework)
        {
            Type loggerType = typeof(LogTelemetryToStdOutForTest);
            var TelemetryTestLogger = new[]
                {
                    "--property:SelfContained=true",
                    $"/Logger:{loggerType.FullName},{loggerType.GetTypeInfo().Assembly.Location}"
                };

            var testProject = CreateTestProject(targetFramework, "TrimmedR2RSingleFileProject", true, true, true);
            var testProjectInstance = _testAssetsManager.CreateTestProject(testProject);
            var publishCommand = new PublishCommand(testProjectInstance);
            string s = publishCommand.Execute(TelemetryTestLogger).StdOut;//.Should()
            s.Should().Contain(
                "{\"EventName\":\"PublishProperties\",\"Properties\":{\"PublishReadyToRun\":\"True\",\"PublishTrimmed\":\"True\",\"PublishSingleFile\":\"True\",\"PublishAot\":\"null\",\"PublishProtocol\":\"null\"}");
            s.Should().Contain(
                "{\"EventName\":\"ReadyToRun\",\"Properties\":{\"PublishReadyToRunUseCrossgen2\":\"true\",")
                .And.MatchRegex(
                    "\"Crossgen2PackVersion\":\"[5-9]\\..+\"");
            s.Should().Contain(
                "\"FailedCount\":\"0\"");
            s.Should().MatchRegex(
                "\"CompileListCount\":\"[1-9]\\d?\"");  // Do not hardcode number of assemblies being compiled here, due to ILTrimmer
        }

        [CoreMSBuildOnlyTheory]
        [InlineData(ToolsetInfo.CurrentTargetFramework)] 
        void It_collects_crossgen2_publishing_properties(string targetFramework)
        {
            // Crossgen2 only supported for Linux/Windows x64 scenarios for now
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || RuntimeInformation.OSArchitecture != Architecture.X64)
                return;

            Type loggerType = typeof(LogTelemetryToStdOutForTest);
            var TelemetryTestLogger = new[]
                {
                    $"/Logger:{loggerType.FullName},{loggerType.GetTypeInfo().Assembly.Location}"
                };

            var testProject = CreateTestProject(targetFramework, "TrimmedR2RSingleFileProject", r2r: true);
            testProject.AdditionalProperties["PublishReadyToRunUseCrossgen2"] = "True";

            var testProjectInstance = _testAssetsManager.CreateTestProject(testProject);
            var publishCommand = new PublishCommand(testProjectInstance);
            publishCommand.Execute(TelemetryTestLogger).StdOut.Should()
                .Contain(
                    "{\"EventName\":\"PublishProperties\",\"Properties\":{\"PublishReadyToRun\":\"True\",\"PublishTrimmed\":\"null\",\"PublishSingleFile\":\"null\",\"PublishAot\":\"null\",\"PublishProtocol\":\"null\"}")
                .And.Contain(
                    "{\"EventName\":\"ReadyToRun\",\"Properties\":{\"PublishReadyToRunUseCrossgen2\":\"true\",")
                .And.MatchRegex(
                    "\"Crossgen2PackVersion\":\"[5-9]\\..+\"")
                .And.Contain(
                    "\"CompileListCount\":\"1\",\"FailedCount\":\"0\"");
        }

        [CoreMSBuildOnlyTheory]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void It_collects_Aot_publishing_properties(string targetFramework)
        {
            // NativeAOT is only supported on Linux/Windows x64 scenarios for now
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || RuntimeInformation.OSArchitecture != Architecture.X64)
                return;

            Type loggerType = typeof(LogTelemetryToStdOutForTest);
            var TelemetryTestLogger = new[]
                {
                    $"/Logger:{loggerType.FullName},{loggerType.GetTypeInfo().Assembly.Location}"
                };

            // NativeAOT compilation requires PublishTrimmed and will be set to true if not set by the user
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);
            var testProject = CreateTestProject(targetFramework, "AotProject", aot: true);
            testProject.AdditionalProperties["UseCurrentRuntimeIdentifier"] = "true";

            var testProjectInstance = _testAssetsManager.CreateTestProject(testProject);
            var publishCommand = new PublishCommand(testProjectInstance);
            publishCommand.Execute(TelemetryTestLogger).StdOut.Should().Contain(
                "{\"EventName\":\"PublishProperties\",\"Properties\":{\"PublishReadyToRun\":\"null\",\"PublishTrimmed\":\"true\",\"PublishSingleFile\":\"null\",\"PublishAot\":\"True\",\"PublishProtocol\":\"null\"}");
        }


        private TestProject CreateTestProject(string targetFramework, string projectName, bool trimmer = false, bool r2r = false, bool singleFile = false, bool aot = false)
        {
            var testProject = new TestProject()
            {
                Name = projectName,
                TargetFrameworks = targetFramework,
                IsExe = true,
                RuntimeIdentifier = EnvironmentInfo.GetCompatibleRid(targetFramework)
            };
            if (r2r)
            {
                testProject.AdditionalProperties["PublishReadyToRun"] = "True";
            }
            if (trimmer)
            {
                testProject.AdditionalProperties["PublishTrimmed"] = "True";
            }
            if (singleFile)
            {
                testProject.AdditionalProperties["PublishSingleFile"] = "True";
            }
            if (aot)
            {
                testProject.AdditionalProperties["PublishAot"] = "True";
            }

            testProject.SourceFiles[$"{projectName}.cs"] = @"
using System;
public class Program
{
    public static void Main()
    {
        Console.WriteLine(""Hello world"");
    }
}";

            return testProject;
        }
    }
}
