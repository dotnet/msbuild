// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.DotNet.Tools.Test.Utilities;
using FluentAssertions;

namespace Microsoft.DotNet.Cli.Utils.Tests
{
    public class GivenADesktopAppWhichUsesCommandCreateDotnet : TestBase
    {
        [WindowsOnlyFact]
        public void It_calls_dotnet_build_on_a_project_successfully()
        {
            var testAssetsManager = GetTestGroupTestAssetsManager("DesktopTestProjects");
            var testInstance = testAssetsManager
                .CreateTestInstance("DesktopAppWhichCallsDotnet")
                .WithLockFiles()
                .WithBuildArtifacts();
            // project was moved to another location and needs it's relative path to Utils project restored
            new RestoreCommand().Execute(testInstance.TestRoot).Should().Pass();

            var testProjectAssetManager = GetTestGroupTestAssetsManager("TestProjects");
            var testInstanceToBuild = testProjectAssetManager
                .CreateTestInstance("TestAppSimple")
                .WithLockFiles();

            var testProject = Path.Combine(testInstance.TestRoot, "project.json");
            var testProjectToBuild = Path.Combine(testInstanceToBuild.TestRoot, "project.json");

            new RunCommand(testProject).Execute(testProjectToBuild).Should().Pass();
        }
    }
}
