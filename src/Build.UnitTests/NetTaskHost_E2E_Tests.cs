// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.UnitTests;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.Engine.UnitTests
{
    public class NetTaskHost_E2E_Tests : IDisposable
    {
        private readonly TestEnvironment _env;

        public NetTaskHost_E2E_Tests(ITestOutputHelper output)
        {
            _env = TestEnvironment.Create(output);
        }

        private static string AssemblyLocation { get; } = Path.Combine(Path.GetDirectoryName(typeof(NetTaskHost_E2E_Tests).Assembly.Location) ?? AppContext.BaseDirectory);

        private static string TestAssetsRootPath { get; } = Path.Combine(AssemblyLocation, "TestAssets");

        public void Dispose() => _env.Dispose();

        [WindowsFullFrameworkOnlyFact]
        public void NetTaskHostTest()
        {
            _ = _env.SetEnvironmentVariable("MSBuildToolsDirectoryNET", Path.Combine(RunnerUtilities.BootstrapRootPath, "core"));
            _ = _env.SetEnvironmentVariable("MSBuildAssemblyDirectory", Path.Combine(RunnerUtilities.BootstrapRootPath, "core", "sdk", RunnerUtilities.BootstrapSdkVersion));

            TransientTestFolder workFolder = _env.CreateFolder(createFolder: true);
            var testDirectory = workFolder.CreateDirectory(nameof(NetTaskHostTest));
            var assets = Path.Combine(TestAssetsRootPath, "ExampleNetTask");
            CopyDirectory(assets, testDirectory.Path);

            var taskProject = Path.Combine(testDirectory.Path, "ExampleTask", "ExampleTask.csproj");
            string testTaskOutput = RunnerUtilities.ExecBootstrapedMSBuild($"{taskProject} -restore", out bool successTestTask);
            successTestTask.ShouldBeTrue();

            var testTaskProject = Path.Combine(testDirectory.Path, "TestNetTask", "TestNetTask.csproj");

            RunnerUtilities.ExecBootstrapedMSBuild($"{testTaskProject} -restore", out bool successTaskTestProject);

            successTaskTestProject.ShouldBeTrue();
        }

        private static void CopyDirectory(string sourceDir, string destinationDir)
        {
            var dir = new DirectoryInfo(sourceDir);
            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");
            }

            DirectoryInfo[] dirs = dir.GetDirectories();
            Directory.CreateDirectory(destinationDir);

            // Get the files in the source directory and copy to the destination directory
            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath, true);
            }

            // copy subdirectories and their contents to the new location
            foreach (DirectoryInfo subDir in dirs)
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                CopyDirectory(subDir.FullName, newDestinationDir);
            }
        }
    }
}
