// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


using System;
using System.IO;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests.Shared;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    public class TypeLoader_Dependencies_Tests
    {
        private static readonly string ProjectFileFolder = Path.Combine(BuildEnvironmentHelper.Instance.CurrentMSBuildToolsDirectory, "TaskWithDependency");
        private static readonly string ProjectFileName = "TaskWithDependencyTest.proj";
        private static readonly string TaskDllFileName = "TaskWithDependency.dll";
        private static readonly string DependencyDllFileName = "Dependency.dll";

        [Fact]
        public void LoadAssemblyAndDependency_InsideProjectFolder()
        {
            using (var dir = new FileUtilities.TempWorkingDirectory(ProjectFileFolder))
            {
                string projectFilePath = Path.Combine(dir.Path, ProjectFileName);

                bool successfulExit;
                string output = RunnerUtilities.ExecMSBuild(projectFilePath + " /v:diag", out successfulExit);
                Assert.True(successfulExit);

                string dllPath = Path.Combine(dir.Path, TaskDllFileName);

                CheckIfCorrectAssemblyLoaded(output, dllPath);
            }
        }

        [Fact]
        public void LoadAssemblyAndDependency_OutsideProjectFolder()
        {
            using (var dir = new FileUtilities.TempWorkingDirectory(ProjectFileFolder))
            {
                string projectFilePath = Path.Combine(dir.Path, ProjectFileName);

                string tempDir = MoveOrCopyDllsToTempDir(dir.Path, copy: false);
                var newTaskDllPath = Path.Combine(tempDir, TaskDllFileName);

                bool successfulExit;
                string output = RunnerUtilities.ExecMSBuild(projectFilePath + " /v:diag /p:AssemblyPath=" + newTaskDllPath, out successfulExit);
                Assert.True(successfulExit);

                CheckIfCorrectAssemblyLoaded(output, newTaskDllPath);
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="copy"></param>
        /// <returns>Path to temporary directory</returns>
        private string MoveOrCopyDllsToTempDir(string originalDirectory, bool copy)
        {
            var originalTaskDllPath = Path.Combine(originalDirectory, TaskDllFileName);
            var originalDependencyDllPath = Path.Combine(originalDirectory, DependencyDllFileName);

            var temporaryDirectory = FileUtilities.GetTemporaryDirectory();

            var newTaskDllPath = Path.Combine(temporaryDirectory, TaskDllFileName);
            var newDependencyDllPath = Path.Combine(temporaryDirectory, DependencyDllFileName);

            Assert.True(File.Exists(originalTaskDllPath));
            Assert.True(File.Exists(originalDependencyDllPath));

            if (copy)
            {
                File.Copy(originalTaskDllPath, newTaskDllPath);
                File.Copy(originalDependencyDllPath, newDependencyDllPath);

                Assert.True(File.Exists(newTaskDllPath));
                Assert.True(File.Exists(newDependencyDllPath));
            }
            else
            {
                File.Move(originalTaskDllPath, newTaskDllPath);
                File.Move(originalDependencyDllPath, newDependencyDllPath);


                Assert.True(File.Exists(newTaskDllPath));
                Assert.True(File.Exists(newDependencyDllPath));
                Assert.False(File.Exists(originalTaskDllPath));
                Assert.False(File.Exists(originalDependencyDllPath));
            }

            return temporaryDirectory;
        }

        private void CheckIfCorrectAssemblyLoaded(string scriptOutput, string expectedAssemblyPath, bool expectedSuccess = true)
        {
            var successfulMessage = @"Using ""LogStringFromDependency"" task from assembly """ + expectedAssemblyPath + @""".";

            if (expectedSuccess)
            {
                Assert.Contains(successfulMessage, scriptOutput, StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                Assert.DoesNotContain(successfulMessage, scriptOutput, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}

