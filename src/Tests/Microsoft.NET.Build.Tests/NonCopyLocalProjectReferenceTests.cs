// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Linq;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.Extensions.DependencyModel;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class NonCopyLocalProjectReferenceTests : SdkTest
    {
        public NonCopyLocalProjectReferenceTests(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void NonCopyLocalProjectReferenceDoesNotGoToDeps()
        {
            var targetFramework = ToolsetInfo.CurrentTargetFramework;

            var referencedProject = new TestProject
            {
                Name = "ReferencedProject",
                TargetFrameworks = targetFramework,
                IsExe = false,
            };

            var testProject = new TestProject
            {
                Name = "MainProject",
                TargetFrameworks = targetFramework,
                IsExe = true,
                ReferencedProjects = { referencedProject },
            };

            var testAsset = _testAssetsManager
                .CreateTestProject(testProject)
                .WithProjectChanges(doc =>
                    doc.Root
                       .DescendantNodes()
                       .OfType<XElement>()
                       .Where(e => e.Name.LocalName == "ProjectReference")
                       .SingleOrDefault()
                       ?.Add(new XAttribute("Private", "False")));

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute().Should().Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework);

            using var stream = File.OpenRead(outputDirectory.File("MainProject.deps.json").FullName);
            using var reader = new DependencyContextJsonReader();

            reader
                .Read(stream)
                .GetRuntimeAssemblyNames("any")
                .Select(n => n.Name)
                .Should()
                .NotContain("ReferencedProject");
        }
    }
}
