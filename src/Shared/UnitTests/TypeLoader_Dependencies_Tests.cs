// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


using System.IO;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
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
                successfulExit.ShouldBeTrue(output);

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
                successfulExit.ShouldBeTrue(output);

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

            File.Exists(originalTaskDllPath).ShouldBeTrue();
            File.Exists(originalDependencyDllPath).ShouldBeTrue();

            if (copy)
            {
                File.Copy(originalTaskDllPath, newTaskDllPath);
                File.Copy(originalDependencyDllPath, newDependencyDllPath);

                File.Exists(newTaskDllPath).ShouldBeTrue();
                File.Exists(newDependencyDllPath).ShouldBeTrue();
            }
            else
            {
                File.Move(originalTaskDllPath, newTaskDllPath);
                File.Move(originalDependencyDllPath, newDependencyDllPath);


                File.Exists(newTaskDllPath).ShouldBeTrue();
                File.Exists(newDependencyDllPath).ShouldBeTrue();
                File.Exists(originalTaskDllPath).ShouldBeFalse();
                File.Exists(originalDependencyDllPath).ShouldBeFalse();
            }

            return temporaryDirectory;
        }

        private void CheckIfCorrectAssemblyLoaded(string scriptOutput, string expectedAssemblyPath, bool expectedSuccess = true)
        {
            var successfulMessage = @"Using ""LogStringFromDependency"" task from assembly """ + expectedAssemblyPath + @""".";

            if (expectedSuccess)
            {
                scriptOutput.ShouldContain(successfulMessage, Case.Insensitive);
            }
            else
            {
                scriptOutput.ShouldNotContain(successfulMessage, Case.Insensitive);
            }
        }
    }
}

