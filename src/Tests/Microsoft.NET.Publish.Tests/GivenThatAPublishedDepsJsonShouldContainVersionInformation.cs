// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.ProjectModel;
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

            var depsFilePath = Path.Combine(publishDirectory.FullName, $"{testProject.Name}.deps.json");
            CheckVersionsInDepsFile(depsFilePath);
        }

        void CheckVersionsInDepsFile(string depsFilePath)
        {
            DependencyContext dependencyContext;
            using (var depsJsonFileStream = File.OpenRead(depsFilePath))
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
        public void Versions_are_included_for_self_contained_apps()
        {
            Versions_are_included(build: false);
        }

        [Fact]
        public void Versions_are_included_for_build()
        {
            Versions_are_included(build: true);
        }

        private void Versions_are_included(bool build, [CallerMemberName] string callingMethod = "")
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

            var depsFilePath = Path.Combine(outputDirectory.FullName, $"{testProject.Name}.deps.json");
            CheckVersionsInDepsFile(depsFilePath);
        }

        [Fact(Skip = "Host deps.json doesn't have runtime file version info yet: https://github.com/dotnet/sdk/issues/2124")]
        public void Inbox_version_of_assembly_is_loaded_over_applocal_version()
        {
            var (coreDir, publishDir, immutableDir) = TestConflictResult();
            immutableDir.Should().BeEquivalentTo(coreDir, "immutable collections library from Framework should win");
        }

        [Fact]
        public void Inbox_version_is_loaded_if_runtime_file_versions_arent_in_deps()
        {
            void testProjectChanges(TestProject testProject)
            {
                testProject.AdditionalProperties["IncludeFileVersionsInDependencyFile"] = "false";
            }

            var (coreDir, publishDir, immutableDir) = TestConflictResult(testProjectChanges);
            immutableDir.Should().BeEquivalentTo(coreDir, "inbox immutable collections library from should win");
        }

        [Fact]
        public void Local_version_of_assembly_with_higher_version_is_loaded_over_inbox_version()
        {
            void publishFolderChanges(string publishFolder)
            {
                var depsJsonPath = Path.Combine(publishFolder, "DepsJsonVersions.deps.json");
                var depsJson = JObject.Parse(File.ReadAllText(depsJsonPath));
                var target = ((JProperty)depsJson["targets"].First).Value;
                var file = target["System.Collections.Immutable/1.5.0-preview1-26216-02"]["runtime"]["lib/netstandard2.0/System.Collections.Immutable.dll"];
                //  Set fileVersion in deps.json to 4.7.0.0, which should be bigger than in box 4.6.x version
                file["fileVersion"] = "4.7.0.0";
                File.WriteAllText(depsJsonPath, depsJson.ToString());
            }

            var (coreDir, publishDir, immutableDir) = TestConflictResult(publishFolderChanges: publishFolderChanges);
            immutableDir.Should().BeEquivalentTo(publishDir, "published immutable collections library from should win");
        }

        private (string coreDir, string publishDir, string immutableDir) TestConflictResult(
            Action<TestProject> testProjectChanges = null,
            Action<string> publishFolderChanges = null,
            [CallerMemberName] string callingMethod = "")
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
            if (testProjectChanges != null)
            {
                testProjectChanges(testProject);
            }


            var testAsset = _testAssetsManager.CreateTestProject(testProject, callingMethod: callingMethod)
                .Restore(Log, testProject.Name);

            var publishCommand = new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            publishCommand.Execute()
                .Should()
                .Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(testProject.TargetFrameworks, runtimeIdentifier: testProject.RuntimeIdentifier);

            if (publishFolderChanges != null)
            {
                publishFolderChanges(publishDirectory.FullName);
            }

            //  Assembly from package should be deployed, as it is newer than the in-box version for netcoreapp2.0,
            //  which is what the app targets
            publishDirectory.Should().HaveFile("System.Collections.Immutable.dll");

            var exePath = Path.Combine(publishDirectory.FullName, testProject.Name + ".dll");

            //  We want to test a .NET Core 2.0 app rolling forward to .NET Core 2.2.
            //  This wouldn't happen in our test environment as we also have the .NET Core 2.0 shared
            //  framework installed.  So we get the RuntimeFrameworkVersion of an app
            //  that targets .NET Core 2.1, and then use the --fx-version parameter to the host
            //  to force the .NET Core 2.0 app to run on that version
            string rollForwardVersion = GetRollForwardNetCoreAppVersion();

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

            return (coreDir, publishDirectory.FullName, immutableDir);

        }

        string GetRollForwardNetCoreAppVersion()
        {
            var testProject = new TestProject()
            {
                Name = nameof(GetRollForwardNetCoreAppVersion),
                TargetFrameworks = "netcoreapp2.2",
                IsSdkProject = true,
                IsExe = true
            };
            testProject.AdditionalProperties.Add("TargetLatestRuntimePatch", "true");

            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .Restore(Log, testProject.Name);

            LockFile lockFile = LockFileUtilities.GetLockFile(Path.Combine(testAsset.TestRoot, testProject.Name,
                                            "obj", "project.assets.json"), NullLogger.Instance);

            var target = lockFile.GetTarget(NuGetFramework.Parse(testProject.TargetFrameworks), null);
            var netCoreAppLibrary = target.Libraries.Single(l => l.Name == "Microsoft.NETCore.App");

            return netCoreAppLibrary.Version.ToString();
        }
    }
}
