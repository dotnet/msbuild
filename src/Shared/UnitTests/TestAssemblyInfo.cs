// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
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

        /// <summary>
        /// Marks the process as "running tests" at assembly-LOAD time, before test discovery, dynamic-data
        /// providers, or any test-class static constructor can run.
        /// </summary>
        /// <remarks>
        /// On .NET Framework, <c>ToolsetConfigurationReader</c> caches (process-wide, in a static
        /// <c>Lazy&lt;Configuration&gt;</c>) which application config it reads for toolset definitions. When
        /// <c>BuildEnvironmentHelper.Instance.RunningTests</c> is <see langword="false"/> at the moment of the
        /// first read, it reads <c>MSBuild.exe.config</c> — which defines a toolset property
        /// <c>MSBuildExtensionsPath = $([MSBuild]::GetMSBuildExtensionsPath())</c> that resolves to the test
        /// host's own output directory. That poisons <c>MSBuildExtensionsPath</c> resolution for the entire run.
        /// When <c>RunningTests</c> is <see langword="true"/> it instead reads the test host's own config, which
        /// does not define that property, so the tests behave correctly.
        ///
        /// <c>[AssemblyInitialize]</c> can run too late (after MTP discovery has already triggered the first config
        /// read and populated the cache). Setting the flag from a <c>[ModuleInitializer]</c> guarantees the very
        /// first read observes <c>RunningTests == true</c>, mirroring what the old xUnit
        /// <c>ITestPipelineStartup</c> did before discovery.
        /// </remarks>
        [ModuleInitializer]
        internal static void MarkRunningTestsBeforeConfigCacheIsPopulated()
        {
            var frameworkAssembly = typeof(ITask).Assembly;
            SetRunningTests(frameworkAssembly.GetType("Microsoft.Build.Framework.TestInfo"));
            SetRunningTests(frameworkAssembly.GetType("Microsoft.Build.Framework.BuildEnvironmentState"));
        }

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

            // The `dotnet` muxer always injects MSBuildSDKsPath / MSBuildExtensionsPath (pointing at the SDK it
            // resolved) into the environment of the MSBuild process it launches. In the bootstrapped CI build the
            // test hosts are spawned by the stage 1 bootstrap `dotnet`, so these leak in pointing at the stage 1
            // SDK. Any in-proc build a test performs (e.g. ObjectModelHelpers.BuildTempProjectFile*) would then
            // resolve SDKs and import the SDK's ImportAfter targets (e.g. Microsoft.NET.Build.Extensions) from that
            // stale SDK instead of the MSBuild layout under test, producing errors such as MSB4062 / MSB4216.
            // Unset them so every in-proc build uses the test host's own MSBuild layout (matching a clean local run).
            // Child processes that shell out to the bootstrap `dotnet` are unaffected: the muxer recomputes these.
            s_testEnvironment.SetEnvironmentVariable("MSBuildSDKsPath", null);
            s_testEnvironment.SetEnvironmentVariable("MSBuildExtensionsPath", null);
            s_testEnvironment.SetEnvironmentVariable("MSBuildExtensionsPath32", null);
            s_testEnvironment.SetEnvironmentVariable("MSBuildExtensionsPath64", null);

            // Use a project-specific temporary path
            //  This is so multiple test projects can be run in parallel without sharing the same temp directory
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
