// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using Xunit.Abstractions;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.ProjectConstruction;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildWithGlobalJson : SdkTest
    {
        public GivenThatWeWantToBuildWithGlobalJson(ITestOutputHelper log) : base(log)
        {}

        [Fact]
        public void It_fails_build_on_failed_sdk_resolution()
        {
            var fakePath = "fakePath";
            TestProject testProject = new TestProject()
            {
                Name = "FailedResolution",
                IsSdkProject = true,
                TargetFrameworks = "net5.0"
            };
            testProject.AdditionalProperties["SdkResolverHonoredGlobalJson"] = "false";
            testProject.AdditionalProperties["SdkResolverGlobalJsonPath"] = fakePath;

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1141")
                .And
                .HaveStdOutContaining(fakePath);
        }
    }
}
