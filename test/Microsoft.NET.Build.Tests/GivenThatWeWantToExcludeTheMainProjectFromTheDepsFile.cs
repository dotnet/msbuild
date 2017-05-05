// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Linq;
using Xunit;

using static Microsoft.NET.TestFramework.Commands.MSBuildTest;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToExcludeTheMainProjectFromTheDepsFile : SdkTest
    {
        [Fact]
        public void It_builds_successfully()
        {
            if (UsingFullFrameworkMSBuild)
            {
                //  Fullframework NuGet versioning on Jenkins infrastructure issue
                //        https://github.com/dotnet/sdk/issues/1041

                //  Disabled on full framework MSBuild until CI machines have VS with bundled .NET Core / .NET Standard versions
                //  See https://github.com/dotnet/sdk/issues/1077
                return;
            }

            TestProject testProject = new TestProject()
            {
                Name = "ExcludeMainProjectFromDeps",
                IsSdkProject = true,
                TargetFrameworks = "netcoreapp2.0",
                IsExe = true,
            };

            TestProject referencedProject = new TestProject()
            {
                Name = "ReferencedProject",
                IsSdkProject = true,
                TargetFrameworks = "netstandard2.0",
                IsExe = false
            };

            testProject.ReferencedProjects.Add(referencedProject);

            var testProjectInstance = _testAssetsManager.CreateTestProject(testProject, testProject.Name)
                .WithProjectChanges((path, project) =>
                {
                    if (Path.GetFileNameWithoutExtension(path) == testProject.Name)
                    {
                        var ns = project.Root.Name.Namespace;

                        var propertyGroup = new XElement(ns + "PropertyGroup");
                        project.Root.Add(propertyGroup);

                        propertyGroup.Add(new XElement(ns + "IncludeMainProjectInDepsFile", "false"));
                    }
                })
                .Restore(testProject.Name);

            var buildCommand = new BuildCommand(Stage0MSBuild, testProjectInstance.TestRoot, testProject.Name);

            buildCommand.Execute()
                .Should()
                .Pass();
        }
    }
}
