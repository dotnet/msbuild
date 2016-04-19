// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Linq;
using Xunit;
using Moq;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.Extensions.PlatformAbstractions;
using System.Threading;
using FluentAssertions;
using NuGet.Frameworks;

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