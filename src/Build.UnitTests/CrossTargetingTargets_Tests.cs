// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.UnitTests;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    public class CrossTargetingTargetsTests
    {
        private readonly ITestOutputHelper _output;

        public CrossTargetingTargetsTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory]
        [InlineData("WarnAndContinue", true, true)]
        [InlineData("true", true, true)]
        [InlineData("ErrorAndContinue", false, true)]
        [InlineData("ErrorAndStop", false, false)]
        [InlineData("false", false, false)]
        [InlineData(null, false, false)]
        public void DispatchToInnerBuildsCanContinueOnError(string? continueOnError, bool expectSuccess, bool expectAfterDispatch)
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            TransientTestFile nugetTargets = env.CreateFile("NuGet.targets", "<Project />");

            TransientTestProjectWithFiles project = env.CreateTestProjectWithFiles("""
                <Project DefaultTargets="Test">
                <PropertyGroup>
                    <TargetFrameworks>failing;successful</TargetFrameworks>
                    <InnerTargets>InnerBuild</InnerTargets>
                    <BuildInParallel>false</BuildInParallel>
                </PropertyGroup>

                <Import Project="$(MSBuildToolsPath)\Microsoft.Common.CrossTargeting.targets" />

                <Target Name="InnerBuild">
                    <Error
                    Condition="'$(TargetFramework)' == 'failing'"
                    Text="INNER_FAILURE" />
                </Target>

                <Target Name="Test" DependsOnTargets="DispatchToInnerBuilds">
                    <Message Text="AFTER_DISPATCH" Importance="High" />
                </Target>
                </Project>
                """);

            var globalProperties = new Dictionary<string, string>
            {
                ["NuGetRestoreTargets"] = nugetTargets.Path,
            };

            if (continueOnError is not null)
            {
                globalProperties["DispatchToInnerBuildsContinueOnError"] = continueOnError;
            }

            MockLogger logger = expectSuccess
                ? project.BuildProjectExpectSuccess(globalProperties)
                : project.BuildProjectExpectFailure(globalProperties);

            logger.AssertLogContains("INNER_FAILURE");

            if (expectAfterDispatch)
            {
                logger.AssertLogContains("AFTER_DISPATCH");
            }
            else
            {
                logger.AssertLogDoesntContain("AFTER_DISPATCH");
            }
        }
    }
}