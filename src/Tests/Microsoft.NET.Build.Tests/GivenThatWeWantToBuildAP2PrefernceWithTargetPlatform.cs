// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;
using Microsoft.NET.TestFramework.ProjectConstruction;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildAP2PReferenceWithTargetPlatform : SdkTest
    {
        public GivenThatWeWantToBuildAP2PReferenceWithTargetPlatform(ITestOutputHelper log) : base(log)
        {
        }

        // https://github.com/dotnet/sdk/issues/13728
        [CoreMSBuildOnlyFact]
        public void It_builds_successfully()
        {
            var appProject = new TestProject()
            {
                Name = "P2PrefernceWithTargetPlatform_App",
                TargetFrameworks = "net5-windows",
                IsSdkProject = true,
                IsExe = true
            };

            var libraryProject = new TestProject()
            {
                Name = "P2PrefernceWithTargetPlatform_App_Library",
                TargetFrameworks = "net5-windows",
                IsSdkProject = true
            };

            appProject.ReferencedProjects.Add(libraryProject);

            var testAsset = _testAssetsManager.CreateTestProject(appProject);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand
                .Execute()
                .Should()
                .Pass();
        }
    }
}
