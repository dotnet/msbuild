// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using Xunit.Abstractions;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.ProjectConstruction;
using System.IO;
using System;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildWithGlobalJson : SdkTest
    {
        public GivenThatWeWantToBuildWithGlobalJson(ITestOutputHelper log) : base(log)
        {}

        [FullMSBuildOnlyTheory]
        [InlineData(true)]
        [InlineData(false)]
        public void It_fails_build_on_failed_sdk_resolution(bool runningInVS)
        {
            var prevIncludeDefault = Environment.GetEnvironmentVariable("MSBUILDINCLUDEDEFAULTSDKRESOLVER");
            try
            {
                Environment.SetEnvironmentVariable("MSBUILDINCLUDEDEFAULTSDKRESOLVER", "false");
                TestProject testProject = new TestProject()
                {
                    Name = "FailedResolution",
                    TargetFrameworks = "net5.0"
                };

                var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: runningInVS.ToString());
                var globalJsonPath = Path.Combine(testAsset.Path, testProject.Name, "global.json");
                File.WriteAllText(globalJsonPath, @"{
    ""sdk"": {
    ""version"": ""9.9.999""
    }
    }");

                var buildCommand = new BuildCommand(testAsset);
                var result = buildCommand.Execute($"/p:BuildingInsideVisualStudio={runningInVS}", $"/bl:binlog{runningInVS}.binlog")
                    .Should()
                    .Fail();
                var warningString = runningInVS
                    ? "warning : Unable to locate the .NET SDK version '9.9.999' as specified by global.json, please check that the specified version is installed."
                    : "warning : Unable to locate the .NET SDK as specified by global.json, please check that the specified version is installed.";
                var errorString = "Unable to locate the .NET SDK. Check that it is installed, your PATH is configured for the correct architecture, and that the version specified in global.json (if any) matches the installed version.";
                if (runningInVS)
                {
                    result.And
                        .HaveStdOutContaining(warningString)
                        .And
                        .NotHaveStdOutContaining(errorString)
                        .And
                        .HaveStdOutContaining("NETSDK1141");
                }
                else
                {
                    result.And
                        .HaveStdOutContaining(errorString)
                        .And
                        .NotHaveStdOutContaining(warningString);
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable("MSBUILDINCLUDEDEFAULTSDKRESOLVER", prevIncludeDefault);
            }
        }
    }
}
