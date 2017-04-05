// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Microsoft.Build.Shared
{
    internal class BuildEnvironmentHelper
    {
        // Since this class is added as 'link' to shared source in multiple projects,
        // MSBuildConstants.CurrentVisualStudioVersion is not available in all of them.
        private const string CurrentVisualStudioVersion = "15.0";

        // MSBuildConstants.CurrentToolsVersion
        private const string CurrentToolsVersion = "15.0";

        // Duplicated in InternalErrorException.cs. Update both when changing.
        private static readonly string[] s_testRunners =
        {
            "XUNIT", "NUNIT", "MSTEST", "VSTEST", "TASKRUNNER",
            "VSTESTHOST", "QTAGENT32", "CONCURRENT", "RESHARPER", "MDHOST", "TE.PROCESSHOST",
            "JetBrains.ReSharper.TaskRunner.CLR4.x64", "JetBrains.ReSharper.TaskRunner.CLR45"
        };

        private static readonly string[] s_testAssemblies =
        {
            "Microsoft.Build.Tasks.UnitTests", "Microsoft.Build.Engine.UnitTests", "Microsoft.Build.Utilities.UnitTests",
            "Microsoft.Build.CommandLine.UnitTests", "Microsoft.Build.Engine.OM.UnitTests",
            "Microsoft.Build.Framework.UnitTests"
        };

        /// <summary>
        /// Name of the Visual Studio (and Blend) process.
        /// </summary>
        private static readonly string[] s_visualStudioProcess = {"DEVENV", "BLEND"};

        /// <summary>
        /// Name of the MSBuild process(es)
        /// </summary>
        private static readonly string[] s_msBuildProcess = {"MSBUILD"};

        /// <summary>
        /// Name of MSBuild executable files.
        /// </summary>
        private static readonly string[] s_msBuildExeNames = { "MSBuild.exe", "MSBuild.dll" };

        /// <summary>
        /// Gets the cached Build Environment instance.
        /// </summary>
        public static BuildEnvironment Instance
        {
            get
            {
                try
                {
                    return BuildEnvironmentHelperSingleton.s_instance;
                }
                catch (TypeInitializationException e)
                {
                    if (e.InnerException != null)
                    {
                        // Throw the error that caused the TypeInitializationException.
                        // (likely InvalidOperationException)
                        throw e.InnerException;
                    }

                    throw;
                }
            }
        }

        /// <summary>
        /// Find the location of MSBuild.exe based on the current environment.
        /// </summary>
        /// <remarks>
        /// This defines the order and precedence for various methods of discovering MSBuild and associated toolsets.
        /// At a high level, an install under Visual Studio is preferred as the user may have SDKs installed to a
        /// specific instance of Visual Studio and build will only succeed if we can discover those. See
        /// https://github.com/Microsoft/msbuild/issues/1461 for details.
        /// </remarks>
        /// <returns>Build environment.</returns>
        private static BuildEnvironment Initialize()
        {
            // See https://github.com/Microsoft/msbuild/issues/1461 for specification of ordering and details.
            var possibleLocations = new Func<BuildEnvironment>[]
            {
                TryFromEnvironmentVariable,
                TryFromVisualStudioProcess,
                TryFromMSBuildProcess,
                TryFromMSBuildAssembly,
                TryFromDevConsole,
                TryFromSetupApi,
                TryFromAppContextBaseDirectory
            };

            foreach (var location in possibleLocations)
            {
                var env = location();
                if (env != null)
                    return env;
            }

            // If we can't find a suitable environment, continue in the 'None' mode. For the CurrentMSBuildExePath
            // value we will use the current running process. This is likely wrong, but many things use the
            // CurrentMSBuildToolsDirectory value which must be set for basic functionality to work.
            return new BuildEnvironment(
                BuildEnvironmentMode.None,
                s_getProcessFromRunningProcess(),
                runningTests: CheckIfRunningTests(),
                runningInVisualStudio: false,
                visualStudioPath: null);
        }

        private static BuildEnvironment TryFromEnvironmentVariable()
        {
            var msBuildExePath = s_getEnvironmentVariable("MSBUILD_EXE_PATH");

            return TryFromStandaloneMSBuildExe(msBuildExePath);
        }

        private static BuildEnvironment TryFromVisualStudioProcess()
        {
            var vsProcess = s_getProcessFromRunningProcess();
            if (!IsProcessInList(vsProcess, s_visualStudioProcess)) return null;

            var vsRoot = FileUtilities.GetFolderAbove(vsProcess, 3);
            string msBuildExe = GetMSBuildExeFromVsRoot(vsRoot);

            return new BuildEnvironment(
                BuildEnvironmentMode.VisualStudio,
                msBuildExe,
                runningTests: false,
                runningInVisualStudio: true,
                visualStudioPath: vsRoot);
        }

        private static BuildEnvironment TryFromMSBuildProcess()
        {
            var msBuildExe = s_getProcessFromRunningProcess();
            if (!IsProcessInList(msBuildExe, s_msBuildProcess)) return null;

            // First check if we're in a VS installation
            if (NativeMethodsShared.IsWindows &&
                Regex.IsMatch(msBuildExe, $@".*\\MSBuild\\{CurrentToolsVersion}\\Bin\\.*MSBuild\.exe", RegexOptions.IgnoreCase))
            {
                return new BuildEnvironment(
                    BuildEnvironmentMode.VisualStudio,
                    msBuildExe,
                    runningTests: false,
                    runningInVisualStudio: false,
                    visualStudioPath: GetVsRootFromMSBuildAssembly(msBuildExe));
            }

            // Standalone mode running in MSBuild.exe
            return new BuildEnvironment(
                BuildEnvironmentMode.Standalone,
                msBuildExe,
                runningTests: false,
                runningInVisualStudio: false,
                visualStudioPath: null);
        }

        private static BuildEnvironment TryFromMSBuildAssembly()
        {
            var buildAssembly = s_getExecutingAssemblyPath();
            if (buildAssembly == null) return null;

            // Check for MSBuild.[exe|dll] next to the current assembly
            var msBuildExe = Path.Combine(FileUtilities.GetFolderAbove(buildAssembly), "MSBuild.exe");
            var msBuildDll = Path.Combine(FileUtilities.GetFolderAbove(buildAssembly), "MSBuild.dll");

            // First check if we're in a VS installation
            if (NativeMethodsShared.IsWindows &&
                Regex.IsMatch(buildAssembly, $@".*\\MSBuild\\{CurrentToolsVersion}\\Bin\\.*", RegexOptions.IgnoreCase))
            {
                // In a Visual Studio path we must have MSBuild.exe
                if (File.Exists(msBuildExe))
                {
                    return new BuildEnvironment(
                        BuildEnvironmentMode.VisualStudio,
                        msBuildExe,
                        runningTests: CheckIfRunningTests(),
                        runningInVisualStudio: false,
                        visualStudioPath: GetVsRootFromMSBuildAssembly(buildAssembly));
                }
            }

            // We're not in VS, check for MSBuild.exe / dll to consider this a standalone environment.
            string msBuildPath = null;
            if (File.Exists(msBuildExe)) msBuildPath = msBuildExe;
            else if (File.Exists(msBuildDll)) msBuildPath = msBuildDll;

            if (!string.IsNullOrEmpty(msBuildPath))
            {
                // Standalone mode with toolset
                return new BuildEnvironment(
                    BuildEnvironmentMode.Standalone,
                    msBuildPath,
                    runningTests: CheckIfRunningTests(),
                    runningInVisualStudio: false,
                    visualStudioPath: null);
            }

            return null;

        }

        private static BuildEnvironment TryFromDevConsole()
        {
            // VSINSTALLDIR and VisualStudioVersion are set from the Developer Command Prompt.
            var vsInstallDir = s_getEnvironmentVariable("VSINSTALLDIR");
            var vsVersion = s_getEnvironmentVariable("VisualStudioVersion");

            if (string.IsNullOrEmpty(vsInstallDir) || string.IsNullOrEmpty(vsVersion) ||
                vsVersion != CurrentVisualStudioVersion || !Directory.Exists(vsInstallDir)) return null;

            return new BuildEnvironment(
                BuildEnvironmentMode.VisualStudio,
                GetMSBuildExeFromVsRoot(vsInstallDir),
                runningTests: false,
                runningInVisualStudio: false,
                visualStudioPath: vsInstallDir);
        }

        private static BuildEnvironment TryFromSetupApi()
        {
            Version v = new Version(CurrentVisualStudioVersion);
            var instances = s_getVisualStudioInstances()
                .Where(i => i.Version.Major == v.Major && i.Version.Minor == v.Minor && Directory.Exists(i.Path))
                .ToList();

            if (instances.Count == 0) return null;

            if (instances.Count > 1)
            {
                // TODO: Warn user somehow. We may have picked the wrong one.
            }

            return new BuildEnvironment(
                BuildEnvironmentMode.VisualStudio,
                GetMSBuildExeFromVsRoot(instances[0].Path),
                runningTests: false,
                runningInVisualStudio: false,
                visualStudioPath: instances[0].Path);
        }

        private static BuildEnvironment TryFromAppContextBaseDirectory()
        {
            // Assemblies compiled against anything older than .NET 4.0 won't have a System.AppContext
            // Try the base directory that the assembly resolver uses to probe for assemblies.
            // Under certain scenarios the assemblies are loaded from spurious locations like the NuGet package cache
            // but the toolset files are copied to the app's directory via "contentFiles".

            var appContextBaseDirectory = s_getAppContextBaseDirectory();
            if (string.IsNullOrEmpty(appContextBaseDirectory)) return null;

            // Look for possible MSBuild exe names in the AppContextBaseDirectory
            return s_msBuildExeNames
                .Select((name) => TryFromStandaloneMSBuildExe(Path.Combine(appContextBaseDirectory, name)))
                .FirstOrDefault(env => env != null);
        }

        private static BuildEnvironment TryFromStandaloneMSBuildExe(string msBuildExePath)
        {
            if (!string.IsNullOrEmpty(msBuildExePath) && File.Exists(msBuildExePath))
            {
                // MSBuild.exe was found outside of Visual Studio. Assume Standalone mode.
                return new BuildEnvironment(
                    BuildEnvironmentMode.Standalone,
                    msBuildExePath,
                    runningTests: CheckIfRunningTests(),
                    runningInVisualStudio: false,
                    visualStudioPath: null);
            }

            return null;
        }

        private static string GetVsRootFromMSBuildAssembly(string msBuildAssembly)
        {
            return FileUtilities.GetFolderAbove(msBuildAssembly,
                Regex.IsMatch(msBuildAssembly, $@".\\MSBuild\\{CurrentToolsVersion}\\Bin\\Amd64\\MSBuild\.exe", RegexOptions.IgnoreCase)
                    ? 5
                    : 4);
        }

        private static string GetMSBuildExeFromVsRoot(string visualStudioRoot)
        {
            return FileUtilities.CombinePaths(visualStudioRoot, "MSBuild", CurrentToolsVersion, "Bin", "MSBuild.exe");
        }

        private static bool CheckIfRunningTests()
        {
            var runningProcess = s_getProcessFromRunningProcess();

            // First check if we're running in a known test runner.
            if (IsProcessInList(runningProcess, s_testRunners))
            {
#if FEATURE_APPDOMAIN
                // If we are, then ensure we're running MSBuild's tests by seeing if any of our assemblies are loaded.
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (
                        s_testAssemblies.Any(
                            item => item.Equals(assembly.GetName().Name, StringComparison.InvariantCultureIgnoreCase)))
                        return true;
                }
#else
                return true;
#endif
            }

            return false;
        }

        /// <summary>
        /// Returns true if processName appears in the processList
        /// </summary>
        /// <param name="processName">Name of the process</param>
        /// <param name="processList">List of processes to check</param>
        /// <returns></returns>
        private static bool IsProcessInList(string processName, string[] processList)
        {
            return
                processList.Any(
                    s =>
                        Path.GetFileNameWithoutExtension(processName)?
                            .IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string GetProcessFromRunningProcess()
        {
            return Process.GetCurrentProcess().MainModule.FileName;
        }

        private static string GetExecutingAssemblyPath()
        {
            return FileUtilities.ExecutingAssemblyPath;
        }

        private static string GetAppContextBaseDirectory()
        {
#if !CLR2COMPATIBILITY // Assemblies compiled against anything older than .NET 4.0 won't have a System.AppContext
            return AppContext.BaseDirectory;
#else
            return null;
#endif
        }

        private static string GetEnvironmentVariable(string variable)
        {
            return Environment.GetEnvironmentVariable(variable);
        }

        /// <summary>
        /// Resets the current singleton instance (for testing).
        /// </summary>
        internal static void ResetInstance_ForUnitTestsOnly(Func<string> getProcessFromRunningProcess = null,
            Func<string> getExecutingAssemblyPath = null, Func<string> getAppContextBaseDirectory = null,
            Func<IEnumerable<VisualStudioInstance>> getVisualStudioInstances = null,
            Func<string, string> getEnvironmentVariable = null)
        {
            s_getProcessFromRunningProcess = getProcessFromRunningProcess ?? GetProcessFromRunningProcess;
            s_getExecutingAssemblyPath = getExecutingAssemblyPath ?? GetExecutingAssemblyPath;
            s_getAppContextBaseDirectory = getAppContextBaseDirectory ?? GetAppContextBaseDirectory;
            s_getVisualStudioInstances = getVisualStudioInstances ?? VisualStudioLocationHelper.GetInstances;
            s_getEnvironmentVariable = getEnvironmentVariable ?? GetEnvironmentVariable;

            BuildEnvironmentHelperSingleton.s_instance = Initialize();
        }

        private static Func<string> s_getProcessFromRunningProcess = GetProcessFromRunningProcess;
        private static Func<string> s_getExecutingAssemblyPath = GetExecutingAssemblyPath;
        private static Func<string> s_getAppContextBaseDirectory = GetAppContextBaseDirectory;
        private static Func<IEnumerable<VisualStudioInstance>> s_getVisualStudioInstances = VisualStudioLocationHelper.GetInstances;
        private static Func<string, string> s_getEnvironmentVariable = GetEnvironmentVariable;


        private static class BuildEnvironmentHelperSingleton
        {
            // Explicit static constructor to tell C# compiler
            // not to mark type as beforefieldinit
            static BuildEnvironmentHelperSingleton()
            { }

            public static BuildEnvironment s_instance = Initialize();
        }
    }

    /// <summary>
    /// Enum which defines which environment / mode MSBuild is currently running.
    /// </summary>
    internal enum BuildEnvironmentMode
    {
        /// <summary>
        /// Running from Visual Studio directly or from MSBuild installed under an instance of Visual Studio.
        /// Toolsets and extensions will be loaded from the Visual Studio instance.
        /// </summary>
        VisualStudio,

        /// <summary>
        /// Running in a standalone toolset mode. All toolsets and extensions paths are relative to the app 
        /// running and not dependent on Visual Studio. (e.g. dotnet CLI, open source clone of our repo)
        /// </summary>
        Standalone,

        /// <summary>
        /// Running without any defined toolsets. Most functionality limited. Likely will not be able to
        /// build or evaluate a project. (e.g. reference to Microsoft.*.dll without a toolset definition
        /// or Visual Studio instance installed).
        /// </summary>
        None
    }

    /// <summary>
    /// Defines the current environment for build tools.
    /// </summary>
    internal class BuildEnvironment
    {
        public BuildEnvironment(BuildEnvironmentMode mode, string currentMSBuildExePath, bool runningTests, bool runningInVisualStudio, string visualStudioPath)
        {
            Mode = mode;
            RunningTests = runningTests;
            RunningInVisualStudio = runningInVisualStudio;

            CurrentMSBuildExePath = currentMSBuildExePath;
            CurrentMSBuildToolsDirectory = Path.GetDirectoryName(currentMSBuildExePath);
            CurrentMSBuildConfigurationFile = string.Concat(currentMSBuildExePath, ".config");

            VisualStudioInstallRootDirectory = visualStudioPath;

            if (mode == BuildEnvironmentMode.VisualStudio)
            {
                var isAmd64 = FileUtilities.EnsureNoTrailingSlash(CurrentMSBuildToolsDirectory)
                    .EndsWith("amd64", StringComparison.OrdinalIgnoreCase);

                if (isAmd64)
                {
                    MSBuildToolsDirectory32 = FileUtilities.GetFolderAbove(CurrentMSBuildToolsDirectory);
                    MSBuildToolsDirectory64 = CurrentMSBuildToolsDirectory;
                }
                else
                {
                    MSBuildToolsDirectory32 = CurrentMSBuildToolsDirectory;
                    MSBuildToolsDirectory64 = Path.Combine(CurrentMSBuildToolsDirectory, "amd64");
                }
            }
            else
            {
                MSBuildToolsDirectory32 = CurrentMSBuildToolsDirectory;
                MSBuildToolsDirectory64 = CurrentMSBuildToolsDirectory;
            }

            MSBuildExtensionsPath = mode == BuildEnvironmentMode.VisualStudio
                ? Path.Combine(VisualStudioInstallRootDirectory, "MSBuild")
                : CurrentMSBuildToolsDirectory;
        }

        internal BuildEnvironmentMode Mode { get; }

        /// <summary>
        /// Gets the flag that indicates if we are running in a test harness.
        /// </summary>
        internal bool RunningTests { get; }

        /// <summary>
        /// Returns true when the entry point application is Visual Studio.
        /// </summary>
        internal bool RunningInVisualStudio { get; }

        /// <summary>
        /// Path to the MSBuild 32-bit tools directory.
        /// </summary>
        internal string MSBuildToolsDirectory32 { get; }

        /// <summary>
        /// Path to the MSBuild 64-bit (AMD64) tools directory.
        /// </summary>
        internal string MSBuildToolsDirectory64 { get; }

        /// <summary>
        /// Path to the Sdks folder for this MSBuild instance.
        /// </summary>
        internal string MSBuildSDKsPath
        {
            get
            {
                string defaultSdkPath;

                if (VisualStudioInstallRootDirectory != null)
                {
                    // Can't use the N-argument form of Combine because it doesn't exist on .NET 3.5
                    defaultSdkPath = FileUtilities.CombinePaths(VisualStudioInstallRootDirectory, "MSBuild", "Sdks");
                }
                else
                {
                    defaultSdkPath = Path.Combine(CurrentMSBuildToolsDirectory, "Sdks");
                }

                // Allow an environment-variable override of the default SDK location
                return Environment.GetEnvironmentVariable("MSBuildSDKsPath") ?? defaultSdkPath;
            }
        }

        /// <summary>
        /// Full path to the current MSBuild configuration file.
        /// </summary>
        internal string CurrentMSBuildConfigurationFile { get; }

        /// <summary>
        /// Full path to current MSBuild.exe.
        /// <remarks>
        /// This path is likely not the current running process. We may be inside
        /// Visual Studio or a test harness. In that case this will point to the
        /// version of MSBuild found to be associated with the current environment.
        /// </remarks>
        /// </summary>
        internal string CurrentMSBuildExePath { get; private set; }

        /// <summary>
        /// Full path to the current MSBuild tools directory. This will be 32-bit unless
        /// we're executing from the 'AMD64' folder.
        /// </summary>
        internal string CurrentMSBuildToolsDirectory { get; }

        /// <summary>
        /// Path to the root Visual Studio install directory
        /// (e.g. 'c:\Program Files (x86)\Microsoft Visual Studio 15.0')
        /// </summary>
        internal string VisualStudioInstallRootDirectory { get; }

        /// <summary>
        /// MSBuild extensions path. On Standalone this defaults to the MSBuild folder. In
        /// VisualStudio mode this folder will be %VSINSTALLDIR%\MSBuild.
        /// </summary>
        internal string MSBuildExtensionsPath { get; set; }
    }
}
