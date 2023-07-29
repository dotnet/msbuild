// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.DependencyModel;

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
