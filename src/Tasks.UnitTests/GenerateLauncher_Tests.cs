// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.UnitTests.Shared;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    public sealed class GenerateLauncher_Tests
    {
        // MSB3964 is the unique code for the "GenerateLauncher.MissingLauncherExe" resource
        // ("Could not find required file '{0}'"). Asserting on the code lets the test be
        // independent of localized resource text.
        private const string MissingLauncherExeCode = "MSB3964";

        private readonly ITestOutputHelper _output;

        public GenerateLauncher_Tests(ITestOutputHelper output)
        {
            _output = output;
        }

        /// <summary>
        /// Proves that <see cref="GenerateLauncher"/> resolves a relative <c>LauncherPath</c>
        /// against <see cref="TaskEnvironment.ProjectDirectory"/> rather than against the
        /// process-wide current directory. We place a file with a unique relative name inside
        /// the project directory; if resolution went through CWD instead, the launcher would
        /// not be found and the task would log MSB3964. The test does not assert on the final
        /// outcome of the task (the placed file is not a real PE binary, so the subsequent
        /// resource-update step will fail) — it only asserts on where the file was *looked
        /// for*, which is exactly what the MT-safe contract requires.
        /// </summary>
        [WindowsOnlyFact]
        public void RelativeLauncherPath_ResolvesAgainstProjectDirectory_NotCurrentDirectory()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            TransientTestFolder projectDir = env.CreateFolder();
            TransientTestFolder outputDir = env.CreateFolder();

            // Use a relative name that is extremely unlikely to exist anywhere on disk
            // outside of projectDir, so a CWD-based resolution would fail.
            const string relativeLauncherName = "GenerateLauncher_Tests_Probe.exe";
            File.WriteAllBytes(Path.Combine(projectDir.Path, relativeLauncherName), [0]);

            var engine = new MockEngine(_output);
            var task = new GenerateLauncher
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDir.Path),
                LauncherPath = relativeLauncherName,
                OutputPath = outputDir.Path,
                EntryPoint = new TaskItem("App.exe"),
            };

            task.Execute();

            // Resolution went through ProjectDirectory → the file was found → MSB3964 must NOT appear.
            // A later step (resource update) will fail with a different code, which is fine for this test.
            engine.AssertLogDoesntContain(MissingLauncherExeCode);
        }

        /// <summary>
        /// Proves that user-facing diagnostic messages quote the path the caller supplied,
        /// not the absolutized form produced internally. The test deliberately points
        /// <c>LauncherPath</c> at a non-existent relative file so <see cref="GenerateLauncher"/>
        /// logs MSB3964 ("Could not find required file '{0}'"), then asserts the message
        /// contains the literal relative input but not the absolutized project-directory-
        /// qualified path.
        /// </summary>
        [WindowsOnlyFact]
        public void MissingLauncher_ErrorMessage_PreservesOriginalRelativePath()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            TransientTestFolder projectDir = env.CreateFolder();
            TransientTestFolder outputDir = env.CreateFolder();

            const string relativeLauncherName = "GenerateLauncher_Tests_DoesNotExist.exe";
            string absolutizedLauncherPath = Path.Combine(projectDir.Path, relativeLauncherName);

            var engine = new MockEngine(_output);
            var task = new GenerateLauncher
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDir.Path),
                LauncherPath = relativeLauncherName,
                OutputPath = outputDir.Path,
                EntryPoint = new TaskItem("App.exe"),
            };

            bool result = task.Execute();

            result.ShouldBeFalse();
            engine.AssertLogContains(MissingLauncherExeCode);
            // The message must quote the original input, not the absolutized form.
            engine.AssertLogContains($"'{relativeLauncherName}'");
            engine.AssertLogDoesntContain($"'{absolutizedLauncherPath}'");
        }
    }
}
