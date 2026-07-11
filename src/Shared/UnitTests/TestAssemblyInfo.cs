// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.Build.UnitTests;
using Microsoft.Build.UnitTests.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Constants = Microsoft.Build.Framework.Constants;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    [TestClass]
    public sealed class MSBuildTestAssemblyHooks
    {
        private static TestEnvironment s_testEnvironment;

        [AssemblyInitialize]
        public static void AssemblyInitialize(TestContext context)
        {
            var frameworkAssembly = typeof(ITask).Assembly;
            SetRunningTests(frameworkAssembly.GetType("Microsoft.Build.Framework.TestInfo"));
            SetRunningTests(frameworkAssembly.GetType("Microsoft.Build.Framework.BuildEnvironmentState"));

            var currentBuildEnvironment = BuildEnvironmentHelper.Instance;
            BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly(
                new BuildEnvironment(
                    currentBuildEnvironment.Mode,
                    currentBuildEnvironment.CurrentMSBuildExePath,
                    runningTests: true,
                    currentBuildEnvironment.RunningInMSBuildExe,
                    currentBuildEnvironment.RunningInVisualStudio,
                    currentBuildEnvironment.VisualStudioInstallRootDirectory));

            s_testEnvironment = TestEnvironment.Create(output: context, ignoreBuildErrorFiles: true);
            s_testEnvironment.DoNotLaunchDebugger();

            var bootstrapCorePath = Path.Combine(Path.Combine(RunnerUtilities.BootstrapRootPath, "core"), Constants.DotnetProcessName);
            s_testEnvironment.SetEnvironmentVariable(Constants.DotnetHostPathEnvVarName, bootstrapCorePath);
            s_testEnvironment.SetEnvironmentVariable("VisualStudioVersion", null);
            s_testEnvironment.SetEnvironmentVariable("DOTNET_PERFLOG_DIR", null);

            var subdirectory = Path.GetRandomFileName();
            string newTempPath = Path.Combine(Path.GetTempPath(), subdirectory);
            var assemblyTempFolder = s_testEnvironment.CreateFolder(newTempPath);
            s_testEnvironment.SetTempPath(assemblyTempFolder.Path);
            FileUtilities.ClearTempFileDirectory();

            s_testEnvironment.CreateFile(assemblyTempFolder, "MSBuild_Tests.txt", $"Temporary test folder for tests from {AppContext.BaseDirectory}");
            s_testEnvironment.CreateFile(assemblyTempFolder, "Directory.Build.rsp", string.Empty);
            s_testEnvironment.CreateFile(assemblyTempFolder, "Directory.Build.props", "<Project />");
            s_testEnvironment.CreateFile(assemblyTempFolder, "Directory.Build.targets", "<Project />");
        }

        [AssemblyCleanup]
        public static void AssemblyCleanup()
        {
            s_testEnvironment?.Dispose();
            s_testEnvironment = null;
        }

        private static void SetRunningTests(Type testInfoType)
        {
            var runningTestsField = testInfoType.GetField("s_runningTests", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            runningTestsField.SetValue(null, true);
        }
    }
}
