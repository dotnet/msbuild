using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyModel;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatAPublishedDepsJsonShouldContainVersionInformation : SdkTest
    {
        public GivenThatAPublishedDepsJsonShouldContainVersionInformation(ITestOutputHelper log) : base(log)
        {
        }

        private TestProject GetTestProject()
        {
            var testProject = new TestProject()
            {
                Name = "DepsJsonVersions",
                TargetFrameworks = "netcoreapp2.0",
                IsSdkProject = true,
                IsExe = true,
            };
            testProject.PackageReferences.Add(new TestPackageReference("System.Collections.Immutable", "1.5.0-preview1-26216-02"));
            testProject.PackageReferences.Add(new TestPackageReference("Libuv", "1.10.0"));

            return testProject;
        }

        [Fact]
        public void Versions_are_included_in_deps_json()
        {
            var testProject = GetTestProject();

            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .Restore(Log, testProject.Name);

            var publishCommand = new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            publishCommand.Execute()
                .Should()
                .Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(testProject.TargetFrameworks, runtimeIdentifier: testProject.RuntimeIdentifier);
            publishDirectory.Should().HaveFile(testProject.Name + ".deps.json");

            DependencyContext dependencyContext;
            using (var depsJsonFileStream = File.OpenRead(Path.Combine(publishDirectory.FullName, $"{testProject.Name}.deps.json")))
            {
                dependencyContext = new DependencyContextJsonReader().Read(depsJsonFileStream);
            }

            var libuvRuntimeLibrary = dependencyContext.RuntimeLibraries.Single(l => l.Name == "Libuv");
            var libuvRuntimeFiles = libuvRuntimeLibrary.NativeLibraryGroups.SelectMany(rag => rag.RuntimeFiles).ToList();
            libuvRuntimeFiles.Should().NotBeEmpty();
            foreach (var runtimeFile in libuvRuntimeFiles)
            {
                runtimeFile.AssemblyVersion.Should().BeNull();
                runtimeFile.FileVersion.Should().Be("0.0.0.0");
            }

            var immutableRuntimeLibrary = dependencyContext.RuntimeLibraries.Single(l => l.Name == "System.Collections.Immutable");
            var immutableRuntimeFiles = immutableRuntimeLibrary.RuntimeAssemblyGroups.SelectMany(rag => rag.RuntimeFiles).ToList();
            immutableRuntimeFiles.Should().NotBeEmpty();
            foreach (var runtimeFile in immutableRuntimeFiles)
            {
                runtimeFile.AssemblyVersion.Should().Be("1.2.3.0");
                runtimeFile.FileVersion.Should().Be("4.6.26216.2");
            }
        }

        [Fact]
        public void Versions_are_not_included_for_self_contained_apps()
        {
            Versions_are_not_included(false);
        }

        [Fact]
        public void Versions_are_not_included_for_build()
        {
            Versions_are_not_included(true);
        }

        private void Versions_are_not_included(bool build, [CallerMemberName] string callingMethod = "")
        {
            var testProject = GetTestProject();
            testProject.RuntimeIdentifier = EnvironmentInfo.GetCompatibleRid(testProject.TargetFrameworks);

            var testAsset = _testAssetsManager.CreateTestProject(testProject, callingMethod)
                .Restore(Log, testProject.Name);

            MSBuildCommand command;
            if (build)
            {
                command = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));
            }
            else
            {
                command = new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));
            }

            command.Execute()
                .Should()
                .Pass();

            var outputDirectory = command.GetOutputDirectory(testProject.TargetFrameworks, runtimeIdentifier: testProject.RuntimeIdentifier);
            outputDirectory.Should().HaveFile(testProject.Name + ".deps.json");

            DependencyContext dependencyContext;
            using (var depsJsonFileStream = File.OpenRead(Path.Combine(outputDirectory.FullName, $"{testProject.Name}.deps.json")))
            {
                dependencyContext = new DependencyContextJsonReader().Read(depsJsonFileStream);
            }

            var allRuntimeFiles = dependencyContext.RuntimeLibraries.SelectMany(rl => rl.NativeLibraryGroups.Concat(rl.RuntimeAssemblyGroups))
                .SelectMany(rag => rag.RuntimeFiles);

            allRuntimeFiles.Should().NotBeEmpty();

            foreach (var runtimeFile in allRuntimeFiles)
            {
                runtimeFile.AssemblyVersion.Should().BeNull();
                runtimeFile.FileVersion.Should().BeNull();
            }
        }
    }
}
