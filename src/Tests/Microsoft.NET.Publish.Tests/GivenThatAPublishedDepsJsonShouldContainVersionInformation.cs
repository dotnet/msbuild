using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
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

        [Fact]
        public void Inbox_version_of_assembly_is_loaded_over_applocal_version()
        {
            var testProject = GetTestProject();

            testProject.SourceFiles["Program.cs"] = @"
using System;

static class Program
{
    public static void Main()
    {
        Console.WriteLine(typeof(object).Assembly.Location);
        Console.WriteLine(typeof(System.Collections.Immutable.ImmutableList).Assembly.Location);
    }
}
";

            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .Restore(Log, testProject.Name);

            var publishCommand = new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            publishCommand.Execute()
                .Should()
                .Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(testProject.TargetFrameworks, runtimeIdentifier: testProject.RuntimeIdentifier);


            //  Assembly from package should be deployed, as it is newer than the in-box version for netcoreapp2.0,
            //  which is what the app targets
            publishDirectory.Should().HaveFile("System.Collections.Immutable.dll");

            var exePath = Path.Combine(publishDirectory.FullName, testProject.Name + ".dll");

            //  We want to test a .NET Core 2.0 app rolling forward to .NET Core 2.1.
            //  This wouldn't happen in our test environment as we also have the .NET Core 2.0 shared
            //  framework installed.  So we get the RuntimeFrameworkVersion of an app
            //  that targets .NET Core 2.1, and then use the --fx-version parameter to the host
            //  to force the .NET Core 2.0 app to run on that version
            string rollForwardVersion = GetRollForwardNetCoreAppVersion();

            var foo = TestContext.Current.ToolsetUnderTest.CliVersionForBundledVersions;
            var runAppCommand = Command.Create(TestContext.Current.ToolsetUnderTest.DotNetHostPath,
                new string[] { "exec", "--fx-version", rollForwardVersion, exePath });

            var runAppResult = runAppCommand
                .CaptureStdOut()
                .CaptureStdErr()
                .Execute();

            runAppResult
                .Should()
                .Pass();

            var stdOutLines = runAppResult.StdOut.Split(Environment.NewLine);

            string coreDir = Path.GetDirectoryName(stdOutLines[0]);
            string immutableDir = Path.GetDirectoryName(stdOutLines[1]);

            immutableDir.Should().BeEquivalentTo(coreDir, "immutable collections library from Framework should win");

        }

        string GetRollForwardNetCoreAppVersion()
        {
            var testProject = new TestProject()
            {
                Name = nameof(GetRollForwardNetCoreAppVersion),
                TargetFrameworks = "netcoreapp2.1",
                IsSdkProject = true,
                IsExe = true
            };
            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .Restore(Log, testProject.Name);
            var getValuesCommand = new GetValuesCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name),
                        testProject.TargetFrameworks, "RuntimeFrameworkVersion")
            {
                ShouldCompile = false
            };

            getValuesCommand.Execute().Should().Pass();

            return getValuesCommand.GetValues().Single();
        }
    }
}
