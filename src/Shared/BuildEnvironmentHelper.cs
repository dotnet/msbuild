// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Collections.Generic;

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
            "VSTESTHOST", "QTAGENT32", "CONCURRENT", "RESHARPER", "MDHOST", "TE.PROCESSHOST"
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
        private static readonly string[] s_msBuildExeNames = {"MSBuild.exe", "MSBuild.dll"};

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
        /// <returns>Build environment.</returns>
        private static BuildEnvironment Initialize()
        {
            // Get the executable we are running
            var processNameCommandLine = s_getProcessFromCommandLine();
            var processNameCurrentProcess = s_getProcessFromRunningProcess();
            var executingAssembly = s_getExecutingAssmblyPath();
            var currentDirectory = s_getCurrentDirectory();
            var appContextBaseDirectory = s_getAppContextBaseDirectory();


            // Check if our current process name is in the list of own test runners
            var runningTests = CheckIfRunningTests(processNameCommandLine, processNameCurrentProcess);

            // Check to see if we're running inside of Visual Studio
            bool runningInVisualStudio;
            var visualStudioPath = GetVisualStudioPath(out runningInVisualStudio);

            var possibleLocations = new Func<BuildEnvironment>[]
            {
                // Check explicit %MSBUILD_EXE_PATH% environment variable.
                () => TryFromEnvironmentVariable(runningTests, runningInVisualStudio, visualStudioPath),

                // See if we're running from MSBuild.exe
                () => TryFromCurrentProcess(processNameCommandLine, runningTests, runningInVisualStudio, visualStudioPath),
                () => TryFromCurrentProcess(processNameCurrentProcess, runningTests, runningInVisualStudio, visualStudioPath),

                // Try from our current executing assembly (e.g. path to Microsoft.Build.dll)
                () => TryFromFolder(Path.GetDirectoryName(executingAssembly), runningTests, runningInVisualStudio, visualStudioPath),

                // Try based on the Visual Studio Root
                ()=> TryFromVisualStudioRoot(visualStudioPath, runningTests, runningInVisualStudio),

#if !CLR2COMPATIBILITY // Assemblies compiled against anything older than .NET 4.0 won't have a System.AppContext
                // Try the base directory that the assembly resolver uses to probe for assemblies.
                // Under certain scenarios the assemblies are loaded from spurious locations like the NuGet package cache
                // but the toolset files are copied to the app's directory via "contentFiles".
                () => TryFromFolder(appContextBaseDirectory, runningTests, runningInVisualStudio, visualStudioPath),
#endif

                // Try from the current directory
                () => TryFromFolder(currentDirectory, runningTests, runningInVisualStudio, visualStudioPath),
            };

            foreach (var location in possibleLocations)
            {
                var env = location();
                if (env != null)
                    return env;
            }

            ErrorUtilities.ThrowInvalidOperation("Shared.CanNotFindValidMSBuildLocation");
            return null; // Not reachable
        }

        private static BuildEnvironment TryFromVisualStudioRoot(string visualStudioPath, bool runningTests, bool runningInVisualStudio)
        {
            if (string.IsNullOrEmpty(visualStudioPath)) return null;

            var msbuildFromVisualStudioRoot = FileUtilities.CombinePaths(visualStudioPath, "MSBuild", CurrentToolsVersion, "Bin");
            return TryFromFolder(msbuildFromVisualStudioRoot, runningTests, runningInVisualStudio, visualStudioPath);
        }

        private static BuildEnvironment TryFromCurrentProcess(string runningProcess, bool runningTests, bool runningInVisualStudio, string visualStudioPath)
        {
            // No need to check the current process if we know we're running in VS or a test harness
            if (runningTests || runningInVisualStudio) return null;
            if (!IsProcessInList(runningProcess, s_msBuildProcess)) return null;

            return IsValidMSBuildPath(runningProcess)
                ? new BuildEnvironment(runningProcess, runningTests, runningInVisualStudio, visualStudioPath)
                : null;
        }

        private static BuildEnvironment TryFromEnvironmentVariable(bool runningTests, bool runningInVisualStudio, string visualStudioPath)
        {
            var msBuildExePath = Environment.GetEnvironmentVariable("MSBUILD_EXE_PATH");

            return IsValidMSBuildPath(msBuildExePath)
                ? new BuildEnvironment(msBuildExePath, runningTests, runningInVisualStudio, visualStudioPath)
                : null;
        }

        private static BuildEnvironment TryFromFolder(string folder, bool runningTests, bool runningInVisualStudio, string visualStudioPath)
        {
            if (string.IsNullOrEmpty(folder)) return null;

            return (
                    s_msBuildExeNames.Select(msbuildFileName => Path.Combine(folder, msbuildFileName))
                    .Where(IsValidMSBuildPath)
                    .Select(msBuildPath => new BuildEnvironment(msBuildPath, runningTests, runningInVisualStudio, visualStudioPath))
                   ).FirstOrDefault();
        }

        /// <summary>
        /// Determine whether the given path is considered to be an acceptable path to MSBuild.
        /// </summary>
        /// <remarks>
        /// If we are running in an orphaned way (i.e. running from Microsoft.Build.dll in someone else's process),
        /// that folder will not be sufficient as a build tools folder (e.g. we can't launch MSBuild.exe from that
        /// location). At minimum, it must have MSBuild.exe and MSBuild.exe.config.
        /// </remarks>
        /// <param name="path">Full path to MSBuild.exe</param>
        /// <returns>True when the path to MSBuild is valid.</returns>
        private static bool IsValidMSBuildPath(string path)
        {
            bool msbuildExeExists = !string.IsNullOrEmpty(path) &&
                s_msBuildExeNames.Any(i => i.Equals(Path.GetFileName(path), StringComparison.OrdinalIgnoreCase)) &&
                    File.Exists(path);
#if FEATURE_SYSTEM_CONFIGURATION
            // If we can read toolsets out of msbuild.exe.config, we must
            // try to do so.
            return msbuildExeExists &&
                   File.Exists($"{path}.config");
#else
            // On .NET Core, we can't read the contents of msbuild.exe.config,
            // so it doesn't matter if it exists.
            return msbuildExeExists;
#endif
        }

        private static bool CheckIfRunningTests(string processNameCommandLine, string processNameCurrentProcess)
        {
            // First check if we're running in a known test runner.
            if (IsProcessInList(processNameCommandLine, s_testRunners) ||
                IsProcessInList(processNameCurrentProcess, s_testRunners))
            {
#if FEATURE_APPDOMAIN
                // If we are, then ensure we're running MSBuild's tests by seeing if any of our assemblies are loaded.
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (s_testAssemblies.Any(item => item.Equals(assembly.GetName().Name, StringComparison.InvariantCultureIgnoreCase)))
                        return true;
                }
#else
                return true;
#endif
            }

            return false;
        }

        /// <summary>
        /// Look for Visual Studio
        /// </summary>
        /// <param name="runningInVisualStudio">True if running in Visual Studio</param>
        /// <returns>Path to Visual Studio install root.</returns>
        private static string GetVisualStudioPath(out bool runningInVisualStudio)
        {
            var processNameCommandLine = s_getProcessFromCommandLine();
            var processNameCurrentProcess = s_getProcessFromRunningProcess();

            // Check to see if we're running inside of Visual Studio
            runningInVisualStudio = IsProcessInList(processNameCommandLine, s_visualStudioProcess) ||
                                    IsProcessInList(processNameCurrentProcess, s_visualStudioProcess);

            // Define the order in which we will look for Visual Studio. Stop when the first instance
            // is found.
            var possibleLocations = new Func<string>[]
            {
                () => TryGetVsFromProcess(processNameCommandLine),
                () => TryGetVsFromProcess(processNameCurrentProcess),
                () => TryGetVsFromEnvironment(),
                () => TryGetVsFromInstalled(),
                () => TryGetVsFromMSBuildLocation(processNameCommandLine),
                () => TryGetVsFromMSBuildLocation(processNameCurrentProcess)
            };

            return possibleLocations.Select(location => location()).FirstOrDefault(path => !string.IsNullOrEmpty(path));
        }

        private static string TryGetVsFromProcess(string process)
        {
            // Check to see if we're running inside of Visual Studio
            // This assumes running from VS\Common7\IDE\<process>.exe.
            return IsProcessInList(process, s_visualStudioProcess)
                ? FileUtilities.GetFolderAbove(process, 3)
                : null;
        }

        private static string TryGetVsFromEnvironment()
        {
            // VSInstallDir is set from the Developer Command Prompt
            var vsInstallDir = Environment.GetEnvironmentVariable("VSINSTALLDIR");
            var vsVersion = Environment.GetEnvironmentVariable("VisualStudioVersion");

            if (!string.IsNullOrEmpty(vsInstallDir) &&
                !string.IsNullOrEmpty(vsVersion) &&
                vsVersion == CurrentVisualStudioVersion &&
                Directory.Exists(vsInstallDir))
            {
                return vsInstallDir;
            }

            return null;
        }

        private static string TryGetVsFromInstalled()
        {
            var instances = s_getVisualStudioInstances();
            Version v = new Version(CurrentVisualStudioVersion);

            // Get the first instance of Visual Studio that matches our Major/Minor compatible version
            return instances.FirstOrDefault(
                i => i.Version.Major == v.Major && i.Version.Minor == v.Minor && Directory.Exists(i.Path))?.Path;
        }

        private static string TryGetVsFromMSBuildLocation(string process)
        {
            // Check assuming we're running in VS\MSBuild\15.0\Bin\MSBuild.exe

            if (IsProcessInList(process, s_msBuildProcess))
            {
                var vsPath = FileUtilities.GetFolderAbove(process, 4);
                var devEnv = FileUtilities.CombinePaths(vsPath, "Common7", "IDE", "devenv.exe");

                // Make sure VS is actually there before we suggest this root.
                if (File.Exists(devEnv))
                {
                    return vsPath;
                }

                // VS\MSBuild\15.0\Bin\amd64\MSBuild.exe
                var vsPath64 = FileUtilities.GetFolderAbove(process, 5);
                var devEnv64 = FileUtilities.CombinePaths(vsPath64, "Common7", "IDE", "devenv.exe");

                // Make sure VS is actually there before we suggest this root.
                if (File.Exists(devEnv64))
                {
                    return vsPath64;
                }
            }


            return null;
        }

        /// <summary>
        /// Returns true if processName appears in the processList
        /// </summary>
        /// <param name="processName">Name of the process</param>
        /// <param name="processList">List of processes to check</param>
        /// <returns></returns>
        private static bool IsProcessInList(string processName, string[] processList)
        {
            return processList.Any(s => Path.GetFileNameWithoutExtension(processName)?.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string GetProcessFromCommandLine()
        {
#if FEATURE_GET_COMMANDLINE
            return Environment.GetCommandLineArgs()[0];
#else
            return null;
#endif
        }

        private static string GetProcessFromRunningProcess()
        {
            return Process.GetCurrentProcess().MainModule.FileName;
        }

        private static string GetExecutingAssmblyPath()
        {
            return FileUtilities.ExecutingAssemblyPath;
        }

        private static string GetCurrentDirectory()
        {
            return Directory.GetCurrentDirectory();
        }

        private static string GetAppContextBaseDirectory()
        {
#if !CLR2COMPATIBILITY // Assemblies compiled against anything older than .NET 4.0 won't have a System.AppContext
            return AppContext.BaseDirectory;
#else
            return null;
#endif
        }

        /// <summary>
        /// Resets the current singleton instance (for testing).
        /// </summary>
        internal static void ResetInstance_ForUnitTestsOnly(Func<string> getProcessFromCommandLine = null,
            Func<string> getProcessFromRunningProcess = null, Func<string> getExecutingAssmblyPath = null,
            Func<string> getCurrentDirectory = null, Func<string> getAppContextBaseDirectory = null,
            Func<IEnumerable<VisualStudioInstance>> getVisualStudioInstances = null)
        {
            s_getProcessFromCommandLine = getProcessFromCommandLine ?? GetProcessFromCommandLine;
            s_getProcessFromRunningProcess = getProcessFromRunningProcess ?? GetProcessFromRunningProcess;
            s_getExecutingAssmblyPath = getExecutingAssmblyPath ?? GetExecutingAssmblyPath;
            s_getCurrentDirectory = getCurrentDirectory ?? GetCurrentDirectory;
            s_getVisualStudioInstances = getVisualStudioInstances ?? VisualStudioLocationHelper.GetInstances;
            s_getAppContextBaseDirectory = getAppContextBaseDirectory ?? GetAppContextBaseDirectory;

            BuildEnvironmentHelperSingleton.s_instance = Initialize();
        }

        private static Func<string> s_getProcessFromCommandLine = GetProcessFromRunningProcess;
        private static Func<string> s_getProcessFromRunningProcess = GetProcessFromRunningProcess;
        private static Func<string> s_getExecutingAssmblyPath = GetExecutingAssmblyPath;
        private static Func<string> s_getCurrentDirectory = GetCurrentDirectory;
        private static Func<string> s_getAppContextBaseDirectory = GetAppContextBaseDirectory;
        private static Func<IEnumerable<VisualStudioInstance>> s_getVisualStudioInstances = VisualStudioLocationHelper.GetInstances;

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
    /// Defines the current environment for build tools.
    /// </summary>
    internal class BuildEnvironment
    {
        public BuildEnvironment(string processNameCommandLine, bool runningTests, bool runningInVisualStudio, string visualStudioPath)
        {
            RunningTests = runningTests;
            RunningInVisualStudio = runningInVisualStudio;

            CurrentMSBuildExePath = processNameCommandLine;
            CurrentMSBuildToolsDirectory = Path.GetDirectoryName(processNameCommandLine);
            CurrentMSBuildConfigurationFile = string.Concat(processNameCommandLine, ".config");

            VisualStudioInstallRootDirectory = visualStudioPath;

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

        /// <summary>
        /// Gets the flag that indicates if we are running in a test harness.
        /// </summary>
        internal bool RunningTests { get; private set; }

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
        internal string MSBuildToolsDirectory64 { get; private set; }

        /// <summary>
        /// Full path to the current MSBuild configuration file.
        /// </summary>
        internal string CurrentMSBuildConfigurationFile { get; private set; }

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
        internal string VisualStudioInstallRootDirectory { get; private set; }
    }
}