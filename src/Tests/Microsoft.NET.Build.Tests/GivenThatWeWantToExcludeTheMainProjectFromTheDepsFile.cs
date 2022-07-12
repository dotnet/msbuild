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

using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToExcludeTheMainProjectFromTheDepsFile : SdkTest
    {
        public GivenThatWeWantToExcludeTheMainProjectFromTheDepsFile(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_builds_successfully()
        {
            TestProject testProject = new TestProject()
            {
                Name = "ExcludeMainProjectFromDeps",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
            };

            TestProject referencedProject = new TestProject()
            {
                Name = "ReferencedProject",
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
                });

            var buildCommand = new BuildCommand(testProjectInstance);

            buildCommand.Execute()
                .Should()
                .Pass();
        }
    }
}
