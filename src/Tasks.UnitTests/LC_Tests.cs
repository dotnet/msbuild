// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Unit tests for the LC task
    /// </summary>
    public class LC_Tests
    {
        /// <summary>
        /// Tests a simple case of valid arguments
        /// </summary>
        [Fact]
        public void SimpleValidArgumentsCommandLine()
        {
            string projectDir = GetProjectDir();
            LC task = CreateTask(projectDir);
            task.Sources = new TaskItem[] { new TaskItem("complist.licx"), new TaskItem("othersrc.txt") };
            task.LicenseTarget = new TaskItem("target.exe");
            task.OutputDirectory = "bin\\debug";
            task.ReferencedAssemblies = new TaskItem[] { new TaskItem("LicensedControl.dll"), new TaskItem("OtherControl.dll") };
            task.NoLogo = true;
            task.TargetFrameworkVersion = "2.0";

            CommandLine.ValidateHasParameter(task, "/complist:complist.licx", false /* don't use response file */);
            CommandLine.ValidateHasParameter(task, "/complist:othersrc.txt", false /* don't use response file */);
            CommandLine.ValidateHasParameter(task, "/target:target.exe", false /* don't use response file */);
            CommandLine.ValidateHasParameter(task, "/outdir:bin\\debug", false /* don't use response file */);
            CommandLine.ValidateHasParameter(task, "/i:LicensedControl.dll", false /* don't use response file */);
            CommandLine.ValidateHasParameter(task, "/i:OtherControl.dll", false /* don't use response file */);
            CommandLine.ValidateHasParameter(task, "/nologo", false /* don't use response file */);

            Assert.Equal(Path.Combine("bin\\debug", "target.exe.licenses"), task.OutputLicense.ItemSpec);
        }

        /// <summary>
        /// Tests a simple case of valid arguments
        /// </summary>
        [Fact]
        public void SimpleValidArgumentsResponseFile()
        {
            string projectDir = GetProjectDir();
            LC task = CreateTask(projectDir);
            task.Sources = new TaskItem[] { new TaskItem("complist.licx"), new TaskItem("othersrc.txt") };
            task.LicenseTarget = new TaskItem("target.exe");
            task.OutputDirectory = "bin\\debug";
            task.ReferencedAssemblies = new TaskItem[] { new TaskItem("LicensedControl.dll"), new TaskItem("OtherControl.dll") };
            task.NoLogo = true;
            task.TargetFrameworkVersion = "4.6";

            CommandLine.ValidateHasParameter(task, "/complist:complist.licx", true /* use response file */);
            CommandLine.ValidateHasParameter(task, "/complist:othersrc.txt", true /* use response file */);
            CommandLine.ValidateHasParameter(task, "/target:target.exe", true /* use response file */);
            CommandLine.ValidateHasParameter(task, "/outdir:bin\\debug", true /* use response file */);
            CommandLine.ValidateHasParameter(task, "/i:LicensedControl.dll", true /* use response file */);
            CommandLine.ValidateHasParameter(task, "/i:OtherControl.dll", true /* use response file */);
            CommandLine.ValidateHasParameter(task, "/nologo", true /* use response file */);

            Assert.Equal(Path.Combine("bin\\debug", "target.exe.licenses"), task.OutputLicense.ItemSpec);
        }

        [Fact]
        public void GenerateFullPathToToolResolvesRelativeSdkToolsPathAgainstTaskEnvironmentProjectDirectory()
        {
            string projectDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            string sdkToolsPath = "tools";
            string processorSpecificDirectory = ProcessorArchitecture.CurrentProcessArchitecture switch
            {
                ProcessorArchitecture.ARM => Path.Combine(projectDir, sdkToolsPath, "arm"),
                ProcessorArchitecture.AMD64 => Path.Combine(projectDir, sdkToolsPath, "x64"),
                ProcessorArchitecture.IA64 => Path.Combine(projectDir, sdkToolsPath, "ia64"),
                _ => Path.Combine(projectDir, sdkToolsPath),
            };
            string expectedToolPath = Path.Combine(processorSpecificDirectory, "lc.exe");

            try
            {
                Directory.CreateDirectory(processorSpecificDirectory);
                File.WriteAllText(expectedToolPath, string.Empty);

                TestableLC task = CreateTestableTask(projectDir);
                task.SdkToolsPath = sdkToolsPath;

                // The returned tool path must be absolute, independent of the process current
                // working directory, so the file is launchable in multithreaded execution mode.
                Assert.Equal(expectedToolPath, task.CallGenerateFullPathToTool());
            }
            finally
            {
                if (Directory.Exists(projectDir))
                {
                    Directory.Delete(projectDir, recursive: true);
                }
            }
        }

        private static string GetProjectDir()
        {
            return Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static LC CreateTask(string projectDir)
        {
            return new LC
            {
                BuildEngine = new MockEngine(),
                TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDir),
            };
        }

        private static TestableLC CreateTestableTask(string projectDir)
        {
            return new TestableLC
            {
                BuildEngine = new MockEngine(),
                TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(projectDir),
            };
        }

        private sealed class TestableLC : LC
        {
            public string CallGenerateFullPathToTool() => GenerateFullPathToTool();
        }
    }
}
