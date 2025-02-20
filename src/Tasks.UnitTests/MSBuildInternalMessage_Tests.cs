﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Evaluation;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.Tasks.UnitTests
{
    public class MSBuildInternalMessage_Tests
    {
        private readonly ITestOutputHelper _testOutput;

        public MSBuildInternalMessage_Tests(ITestOutputHelper testOutput) => _testOutput = testOutput;

        [Theory]
        [InlineData(true, true, "CommonTarget.Prefer32BitAndPreferNativeArm64Enabled", false)]
        [InlineData(false, false, "CommonTarget.PlatformIsAnyCPUAndPreferNativeArm64Enabled", true, new[] { "Release" })]
        public void E2EScenarioTests(bool prefer32, bool isPlatformAnyCpu, string expectedResourceName, bool isNetWarningExpected, string[]? formatArgs = null)
        {
            using (TestEnvironment env = TestEnvironment.Create())
            {
                var outputPath = env.CreateFolder().Path;
                string projectContent = @$"
                <Project DefaultTargets=""Build"">
                    <Import Project=""$(MSBuildBinPath)\Microsoft.Common.props"" />

                    <PropertyGroup>
                        <Platform>{(isPlatformAnyCpu ? "AnyCPU" : "Release")}</Platform>
                        <OutputType>Library</OutputType>
                        <PreferNativeArm64>true</PreferNativeArm64>
                        <Prefer32Bit>{(prefer32 ? "true" : "false")}</Prefer32Bit>
                    </PropertyGroup>

                    <Target Name=""Build""/>
                    <Import Project=""$(MSBuildBinPath)\Microsoft.CSharp.targets"" />

                </Project>
                ";

                var projectFile = env.CreateFile(env.CreateFolder(), "test.csproj", projectContent).Path;
                Project project = ObjectModelHelpers.LoadProjectFileInTempProjectDirectory(projectFile, touchProject: false);

                string expectedBuildMessage = ResourceUtilities.FormatResourceStringStripCodeAndKeyword(expectedResourceName, formatArgs);
                MockLogger logger = new MockLogger(_testOutput);

                project.Build(logger);

                if (isNetWarningExpected)
                {
                    logger.Warnings[0].RawMessage.ShouldBe(expectedBuildMessage);
                }
                else
                {
                    logger.Errors[0].RawMessage.ShouldBe(expectedBuildMessage);
                }
            }
        }
    }
}
