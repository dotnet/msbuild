// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


using System;
using System.IO;
using Microsoft.Build.Shared;
using Microsoft.Build.SharedUtilities;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    public class TypeLoader_Dependencies_Tests
    {
        private static readonly string ProjectFilePath = Path.Combine(BuildEnvironmentHelper.Instance.CurrentMSBuildToolsDirectory, "TaskWithDependencyTest.proj");
        private static readonly string TaskDllFileName = "TaskWithDependency.dll";
        private static readonly string OriginalTaskDllPath = Path.Combine(BuildEnvironmentHelper.Instance.CurrentMSBuildToolsDirectory, TaskDllFileName);
        private static readonly string DependencyDllFileName = "Dependency.dll";
        private static readonly string OriginalDependencyDllPath = Path.Combine(BuildEnvironmentHelper.Instance.CurrentMSBuildToolsDirectory, DependencyDllFileName);

        [Fact]
        public void LoadAssemblyAndDependency_InsideProjectFolder()
        {
            bool successfulExit;
            string output = RunnerUtilities.ExecMSBuild(ProjectFilePath + " /v:diag", out successfulExit);
            Assert.True(successfulExit);

            CheckIfCorrectAssemblyLoaded(output, OriginalTaskDllPath);
        }

        [Fact]
        public void LoadAssemblyAndDependency_OutsideProjectFolder()
        {
            string tempDir = MoveOrCopyDllsToTempDir(copy: false);
            var newTaskDllPath = Path.Combine(tempDir, TaskDllFileName);

            try
            {
                bool successfulExit;
                string output = RunnerUtilities.ExecMSBuild(ProjectFilePath + " /v:diag /p:AssemblyPath=" + newTaskDllPath, out successfulExit);
                Assert.True(successfulExit);

                CheckIfCorrectAssemblyLoaded(output, newTaskDllPath);
            }
            finally
            {
                UndoDLLOperations(tempDir, moveBack: true);
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="copy"></param>
        /// <returns>Path to temporary directory</returns>
        private string MoveOrCopyDllsToTempDir(bool copy)
        {
            var temporaryDirectory = FileUtilities.GetTemporaryDirectory();

            var newTaskDllPath = Path.Combine(temporaryDirectory, TaskDllFileName);
            var newDependencyDllPath = Path.Combine(temporaryDirectory, DependencyDllFileName);

            Assert.True(File.Exists(OriginalTaskDllPath));
            Assert.True(File.Exists(OriginalDependencyDllPath));

            if (copy)
            {
                File.Copy(OriginalTaskDllPath, newTaskDllPath);
                File.Copy(OriginalDependencyDllPath, newDependencyDllPath);

                Assert.True(File.Exists(newTaskDllPath));
                Assert.True(File.Exists(newDependencyDllPath));
            }
            else
            {
                File.Move(OriginalTaskDllPath, newTaskDllPath);
                File.Move(OriginalDependencyDllPath, newDependencyDllPath);


                Assert.True(File.Exists(newTaskDllPath));
                Assert.True(File.Exists(newDependencyDllPath));
                Assert.False(File.Exists(OriginalTaskDllPath));
                Assert.False(File.Exists(OriginalDependencyDllPath));
            }

            return temporaryDirectory;
        }

        /// <summary>
        /// Move / Delete newDllPath and delete temp directory
        /// </summary>
        /// <param name="newDllPath"></param>
        /// <param name="moveBack">If true, move newDllPath back to bin. If false, delete it</param>
        private void UndoDLLOperations(string tempDirectoryPath, bool moveBack)
        {
            var currentTaskDllPath = Path.Combine(tempDirectoryPath, TaskDllFileName);
            var currentDependencyDllPath = Path.Combine(tempDirectoryPath, DependencyDllFileName);

            if (moveBack)
            {
                File.Move(currentTaskDllPath, OriginalTaskDllPath);
                File.Move(currentDependencyDllPath, OriginalDependencyDllPath);
            }
            else
            {
                File.Delete(currentTaskDllPath);
                File.Delete(currentDependencyDllPath);

            }

            Assert.True(File.Exists(OriginalTaskDllPath));
            Assert.True(File.Exists(OriginalDependencyDllPath));


            Assert.False(File.Exists(currentTaskDllPath));
            Assert.False(File.Exists(currentDependencyDllPath));

            Assert.Empty(Directory.EnumerateFiles(tempDirectoryPath));

            Directory.Delete(tempDirectoryPath);
            Assert.False(Directory.Exists(tempDirectoryPath));
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

