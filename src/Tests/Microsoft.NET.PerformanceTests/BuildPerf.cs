// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Microsoft.Xunit.Performance.Api;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Perf.Tests
{
    public class BuildPerf : SdkTest
    {
        public BuildPerf(ITestOutputHelper log) : base(log)
        {
        }

        //  These tests are currently disabled for full framework MSBuild because the CI machines don't
        //  have an MSBuild that supports the /restore command-line argument
        [CoreMSBuildOnlyTheory]
        [InlineData(ProjectPerfOperation.CleanBuild)]
        [InlineData(ProjectPerfOperation.BuildWithNoChanges)]
        public void BuildNetCore2App(ProjectPerfOperation operation)
        {
            var testProject = new TestProject()
            {
                Name = "NetCoreApp",
                TargetFrameworks = "netcoreapp2.0",
                IsSdkProject = true,
                IsExe = true
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: operation.ToString());

            TestProject(testAsset.Path, ".NET Core 2 Console App", operation);
        }

        [CoreMSBuildOnlyTheory]
        [InlineData(ProjectPerfOperation.CleanBuild)]
        [InlineData(ProjectPerfOperation.BuildWithNoChanges)]
        public void BuildNetStandard2Library(ProjectPerfOperation operation)
        {
            var testProject = new TestProject()
            {
                Name = "NetCoreApp",
                TargetFrameworks = "netstandard2.0",
                IsSdkProject = true
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: operation.ToString());

            TestProject(testAsset.Path, ".NET Standard 2.0 Library", operation);
        }

        [CoreMSBuildOnlyTheory]
        [InlineData(ProjectPerfOperation.CleanBuild)]
        [InlineData(ProjectPerfOperation.BuildWithNoChanges)]
        public void BuildWebLarge(ProjectPerfOperation operation)
        {
            string sourceProject = Path.Combine(TestContext.GetRepoRoot(), ".perftestsource/PerformanceTestProjects/WebLarge");
            var testDir = _testAssetsManager.CreateTestDirectory("WebLarge", identifier: operation.ToString());
            Console.WriteLine($"Mirroring {sourceProject} to {testDir}...");
            FolderSnapshot.MirrorFiles(sourceProject, testDir.Path);
            TestContext.Current.WriteGlobalJson(testDir.Path);
            Console.WriteLine("Done");

            TestProject(Path.Combine(testDir.Path, "mvc"), "Build Web Large", operation);
        }
   
        [CoreMSBuildOnlyTheory]
        [InlineData(ProjectPerfOperation.CleanBuild)]
        [InlineData(ProjectPerfOperation.BuildWithNoChanges)]
        public void BuildWebLarge30(ProjectPerfOperation operation)
        {
            string sourceProject = Path.Combine(TestContext.GetRepoRoot(), ".perftestsource/PerformanceTestProjects/WebLarge30");
            var testDir = _testAssetsManager.CreateTestDirectory("WebLarge30", identifier: operation.ToString());
            Console.WriteLine($"Mirroring {sourceProject} to {testDir}...");
            FolderSnapshot.MirrorFiles(sourceProject, testDir.Path);
            TestContext.Current.WriteGlobalJson(testDir.Path);
            Console.WriteLine("Done");

            TestProject(Path.Combine(testDir.Path, "mvc"), "Build Web Large 3.0", operation);
        }
   
        [CoreMSBuildOnlyTheory]
        [InlineData(ProjectPerfOperation.CleanBuild)]
        [InlineData(ProjectPerfOperation.BuildWithNoChanges)]
        public void BuildMVCApp(ProjectPerfOperation operation)
        {
            var testDir = _testAssetsManager.CreateTestDirectory(identifier: operation.ToString());

            NuGetConfigWriter.Write(testDir.Path, NuGetConfigWriter.AspNetCoreDevFeed, NuGetConfigWriter.DotnetCoreBlobFeed);

            var newCommand = new DotnetCommand(Log);
            newCommand.WorkingDirectory = testDir.Path;

            newCommand.Execute("new", "mvc", "--no-restore").Should().Pass();

            TestProject(testDir.Path, "ASP.NET Core MVC app", operation);
        }

        [CoreMSBuildOnlyTheory(Skip = "The code for these scenarios needs to be acquired during the test run (instead of relying on hard-coded local path)")]
        [InlineData("SmallP2POldCsproj", ProjectPerfOperation.CleanBuild)]
        [InlineData("SmallP2POldCsproj", ProjectPerfOperation.BuildWithNoChanges)]
        [InlineData("SmallP2PNewCsproj", ProjectPerfOperation.CleanBuild)]
        [InlineData("SmallP2PNewCsproj", ProjectPerfOperation.BuildWithNoChanges)]
        [InlineData("LargeP2POldCsproj", ProjectPerfOperation.CleanBuild)]
        [InlineData("LargeP2POldCsproj", ProjectPerfOperation.BuildWithNoChanges)]
        public void BuildProjectFromPerfSuite(string name, ProjectPerfOperation operation)
        {
            string sourceProject = Path.Combine(@"C:\MSBPerf\3", name);
            var testDir = _testAssetsManager.CreateTestDirectory("Perf_" + name, identifier: operation.ToString());
            FolderSnapshot.MirrorFiles(sourceProject, testDir.Path);
            TestContext.Current.WriteGlobalJson(testDir.Path);

            //  The generated projects target .NET Core 2.1, retarget them to .NET Core 2.0
            foreach (var projFile in Directory.GetFiles(testDir.Path, "*.csproj", SearchOption.AllDirectories))
            {
                var project = XDocument.Load(projFile);
                var ns = project.Root.Name.Namespace;

                //  Find both TargetFramework and TargetFrameworks elements
                var targetFrameworkElements = project.Root.Elements(ns + "PropertyGroup").Elements("TargetFramework");
                targetFrameworkElements = targetFrameworkElements.Concat(project.Root.Elements(ns + "PropertyGroup").Elements("TargetFrameworks"));

                foreach (var tfElement in targetFrameworkElements)
                {
                    tfElement.Value = tfElement.Value.Replace("netcoreapp2.1", "netcoreapp2.0");
                }

                project.Save(projFile);
            }

            TestProject(testDir.Path, name, operation);
        }

        [CoreMSBuildOnlyTheory(Skip = "This test needs to clone the Roslyn repo and checkout a given commit instead of relying on a local copy of the repo")]
        [InlineData(ProjectPerfOperation.CleanBuild)]
        [InlineData(ProjectPerfOperation.BuildWithNoChanges)]
        public void BuildRoslynCompilers(ProjectPerfOperation operation)
        {

            string sourceProject = @"C:\git\roslyn";
            var testDir = _testAssetsManager.CreateTestDirectory("Perf_Roslyn", identifier: operation.ToString());
            Console.WriteLine($"Mirroring {sourceProject} to {testDir.Path}...");
            FolderSnapshot.MirrorFiles(sourceProject, testDir.Path);
            TestContext.Current.WriteGlobalJson(testDir.Path);
            Console.WriteLine("Done");

            //  Override global.json from repo
            File.Delete(Path.Combine(testDir.Path, "global.json"));

            //  Run Roslyn's restore script
            var restoreCmd = new SdkCommandSpec()
            {
                FileName = Path.Combine(testDir.Path, "Restore.cmd"),
                WorkingDirectory = testDir.Path
            };
            TestContext.Current.AddTestEnvironmentVariables(restoreCmd);
            restoreCmd.ToCommand().Execute().Should().Pass();

            TestProject(Path.Combine(testDir.Path, "Compilers.sln"), "Roslyn", operation);
        }

        public enum ProjectPerfOperation
        {
            CleanBuild,
            BuildWithNoChanges,
            NoOpRestore
        }

        private void TestProject(string projectFolderOrFile, string testName, ProjectPerfOperation perfOperation, string restoreSources = null)
        {
            string testProjectPath;
            string testProjectDirectory;
            bool projectFileSpecified;

            if (File.Exists(projectFolderOrFile))
            {
                projectFileSpecified = true;
                testProjectPath = projectFolderOrFile;
                testProjectDirectory = Path.GetDirectoryName(projectFolderOrFile);
            }
            else
            {
                projectFileSpecified = false;
                testProjectPath = Directory.GetFiles(projectFolderOrFile, "*.sln", SearchOption.AllDirectories).SingleOrDefault();
                if (testProjectPath == null)
                {
                    testProjectPath = Directory.GetFiles(projectFolderOrFile, "*.csproj", SearchOption.AllDirectories).SingleOrDefault();
                    if (testProjectPath == null)
                    {
                        throw new ArgumentException("Could not find project file to test in folder: " + projectFolderOrFile);
                    }
                }
                testProjectDirectory = Path.GetDirectoryName(testProjectPath);
            }

            TestCommand commandToTest;
            var perfTest = new PerfTest();
            perfTest.ScenarioName = testName;

            if (perfOperation == ProjectPerfOperation.NoOpRestore)
            {
                TestCommand restoreCommand;

                if (TestContext.Current.ToolsetUnderTest.ShouldUseFullFrameworkMSBuild)
                {
                    restoreCommand = new RestoreCommand(Log, testProjectPath);
                }
                else
                {
                    restoreCommand = new RestoreCommand(Log, testProjectPath);
                    restoreCommand = new DotnetCommand(Log, "restore");
                    if (projectFileSpecified)
                    {
                        restoreCommand.Arguments.Add(testProjectPath);
                    }
                }
                if (!string.IsNullOrEmpty(restoreSources))
                {
                    restoreCommand.Arguments.Add($"/p:RestoreSources={restoreSources}");
                }
                restoreCommand.WorkingDirectory = testProjectDirectory;

                restoreCommand.Execute().Should().Pass();

                commandToTest = restoreCommand;
                perfTest.TestName = "Restore (No-op)";
            }
            else
            {
                if (TestContext.Current.ToolsetUnderTest.ShouldUseFullFrameworkMSBuild)
                {
                    commandToTest = new BuildCommand(Log, projectFileSpecified ? testProjectPath : testProjectDirectory);
                    commandToTest.Arguments.Add("/restore");
                }
                else
                {
                    commandToTest = new DotnetCommand(Log, "build");
                    if (projectFileSpecified)
                    {
                        commandToTest.Arguments.Add(testProjectPath);
                    }
                }
                if (!string.IsNullOrEmpty(restoreSources))
                {
                    commandToTest.Arguments.Add($"/p:RestoreSources={restoreSources}");
                }
                commandToTest.WorkingDirectory = testProjectDirectory;

                if (perfOperation == ProjectPerfOperation.CleanBuild)
                {
                    perfTest.TestName = "Build";
                }
                else if (perfOperation == ProjectPerfOperation.BuildWithNoChanges)
                {
                    //  Build once before taking folder snapshot
                    commandToTest.Execute().Should().Pass();

                    perfTest.TestName = "Build (no changes)";
                }
                else
                {
                    throw new ArgumentException("Unexpected perf operation: " + perfOperation);
                }
            }

            perfTest.ProcessToMeasure = commandToTest.GetProcessStartInfo();
            perfTest.TestFolder = testProjectDirectory;

            perfTest.Run();
        }
    }
}
