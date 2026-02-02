// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Build.Shared.FileSystem;

#nullable disable

namespace Microsoft.Build.Shared
{
    internal sealed class BuildEnvironmentHelper
    {
        // Since this class is added as 'link' to shared source in multiple projects,
        // MSBuildConstants.CurrentVisualStudioVersion is not available in all of them.
        private const string CurrentVisualStudioVersion = "18.0";

        // MSBuildConstants.CurrentToolsVersion
        private const string CurrentToolsVersion = "Current";

        /// <summary>
        /// Name of the Visual Studio (and Blend) process.
        /// VS ASP intellisense server fails without Microsoft.VisualStudio.Web.Host. Remove when issue fixed: https://devdiv.visualstudio.com/DevDiv/_workitems/edit/574986
        /// </summary>
        private static readonly string[] s_visualStudioProcess = { "DEVENV", "BLEND", "Microsoft.VisualStudio.Web.Host" };

        /// <summary>
        /// Name of the MSBuild process(es)
        /// </summary>
        private static readonly string[] s_msBuildProcess = { "MSBUILD", "MSBUILDTASKHOST" };

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
        /// https://github.com/dotnet/msbuild/issues/1461 for details.
        /// </remarks>
        /// <returns>Build environment.</returns>
        private static BuildEnvironment Initialize()
        {
            // See https://github.com/dotnet/msbuild/issues/1461 for specification of ordering and details.
            var possibleLocations = new Func<BuildEnvironment>[]
            {
                TryFromEnvironmentVariable,
                TryFromVisualStudioProcess,
                TryFromMSBuildProcess,
                TryFromMSBuildAssembly,
                TryFromDevConsole
            };

            foreach (var location in possibleLocations)
            {
                var env = location();
                if (env != null)
                {
                    return env;
                }
            }

            // If we can't find a suitable environment, continue in the 'None' mode.
            // We will use the current running process for the CurrentMSBuildExePath value.  This is likely
            // wrong, but many things use the CurrentMSBuildToolsDirectory value which must be set for basic
            // functionality to work.
            string msbuildExePath = GetProcessFromRunningProcess();

            return new BuildEnvironment(BuildEnvironmentMode.None, msbuildExePath);
        }

        private static BuildEnvironment TryFromEnvironmentVariable()
        {
            var msBuildExePath = GetEnvironmentVariable("MSBUILD_EXE_PATH");

            return msBuildExePath == null
                ? null
                : TryFromMSBuildExeUnderVisualStudio(msBuildExePath, allowLegacyToolsVersion: true) ?? TryFromStandaloneMSBuildExe(msBuildExePath);
        }

        private static BuildEnvironment TryFromVisualStudioProcess()
        {
            var vsProcess = GetProcessFromRunningProcess();
            if (!IsProcessInList(vsProcess, s_visualStudioProcess))
            {
                return null;
            }

            var vsRoot = FileUtilities.GetFolderAbove(vsProcess, 3);
            string msBuildExe = GetMSBuildExeFromVsRoot(vsRoot);

            return new BuildEnvironment(BuildEnvironmentMode.VisualStudio, msBuildExe);
        }

        private static BuildEnvironment TryFromMSBuildProcess()
        {
            var msBuildExe = GetProcessFromRunningProcess();
            if (!IsProcessInList(msBuildExe, s_msBuildProcess))
            {
                return null;
            }

            // First check if we're in a VS installation
            if (Regex.IsMatch(msBuildExe, $@".*\\MSBuild\\{CurrentToolsVersion}\\Bin\\.*MSBuild(?:TaskHost)?\.exe", RegexOptions.IgnoreCase))
            {
                return new BuildEnvironment(BuildEnvironmentMode.VisualStudio, msBuildExe);
            }

            // Standalone mode running in MSBuild.exe
            return new BuildEnvironment(BuildEnvironmentMode.Standalone, msBuildExe);
        }

        private static BuildEnvironment TryFromMSBuildAssembly()
        {
            var buildAssembly = GetExecutingAssemblyPath();
            if (buildAssembly == null)
            {
                return null;
            }

            // Check for MSBuild.[exe|dll] next to the current assembly
            var msBuildExe = Path.Combine(FileUtilities.GetFolderAbove(buildAssembly), "MSBuild.exe");
            var msBuildDll = Path.Combine(FileUtilities.GetFolderAbove(buildAssembly), "MSBuild.dll");

            // First check if we're in a VS installation
            var environment = TryFromMSBuildExeUnderVisualStudio(msBuildExe);
            if (environment != null)
            {
                return environment;
            }

            // We're not in VS, check for MSBuild.exe / dll to consider this a standalone environment.
            string msBuildPath = null;
            if (FileSystems.Default.FileExists(msBuildExe))
            {
                msBuildPath = msBuildExe;
            }
            else if (FileSystems.Default.FileExists(msBuildDll))
            {
                msBuildPath = msBuildDll;
            }

            if (!string.IsNullOrEmpty(msBuildPath))
            {
                // Standalone mode with toolset
                return new BuildEnvironment(BuildEnvironmentMode.Standalone, msBuildPath);
            }

            return null;
        }

        private static BuildEnvironment TryFromMSBuildExeUnderVisualStudio(string msbuildExe, bool allowLegacyToolsVersion = false)
        {
            string msBuildPathPattern = allowLegacyToolsVersion
                ? $@".*\\MSBuild\\({CurrentToolsVersion}|\d+\.0)\\Bin\\.*"
                : $@".*\\MSBuild\\{CurrentToolsVersion}\\Bin\\.*";

            if (Regex.IsMatch(msbuildExe, msBuildPathPattern, RegexOptions.IgnoreCase))
            {
                string visualStudioRoot = GetVsRootFromMSBuildAssembly(msbuildExe);
                return new BuildEnvironment(
                    BuildEnvironmentMode.VisualStudio,
                    GetMSBuildExeFromVsRoot(visualStudioRoot));
            }

            return null;
        }

        private static BuildEnvironment TryFromDevConsole()
        {
            // VSINSTALLDIR and VisualStudioVersion are set from the Developer Command Prompt.
            var vsInstallDir = GetEnvironmentVariable("VSINSTALLDIR");
            var vsVersion = GetEnvironmentVariable("VisualStudioVersion");

            if (string.IsNullOrEmpty(vsInstallDir) || string.IsNullOrEmpty(vsVersion) ||
                vsVersion != CurrentVisualStudioVersion || !FileSystems.Default.DirectoryExists(vsInstallDir))
            {
                return null;
            }

            return new BuildEnvironment(
                BuildEnvironmentMode.VisualStudio,
                GetMSBuildExeFromVsRoot(vsInstallDir));
        }

        private static BuildEnvironment TryFromStandaloneMSBuildExe(string msBuildExePath)
        {
            if (!string.IsNullOrEmpty(msBuildExePath) && FileSystems.Default.FileExists(msBuildExePath))
            {
                // MSBuild.exe was found outside of Visual Studio. Assume Standalone mode.
                return new BuildEnvironment(BuildEnvironmentMode.Standalone, msBuildExePath);
            }

            return null;
        }

        private static string GetVsRootFromMSBuildAssembly(string msBuildAssembly)
        {
            string directory = Path.GetDirectoryName(msBuildAssembly);
            return FileUtilities.GetFolderAbove(msBuildAssembly,
                directory.EndsWith(@"\amd64", StringComparison.OrdinalIgnoreCase)
                    ? 5
                    : 4);
        }

        private static string GetMSBuildExeFromVsRoot(string visualStudioRoot)
            => FileUtilities.CombinePaths(
                visualStudioRoot,
                "MSBuild",
                CurrentToolsVersion,
                "Bin",
                NativeMethodsShared.Is64Bit ? "amd64" : string.Empty,
                "MSBuild.exe");

        /// <summary>
        /// Returns true if processName appears in the processList
        /// </summary>
        /// <param name="processName">Name of the process</param>
        /// <param name="processList">List of processes to check</param>
        /// <returns></returns>
        private static bool IsProcessInList(string processName, string[] processList)
        {
            var processFileName = Path.GetFileNameWithoutExtension(processName);

            if (string.IsNullOrEmpty(processFileName))
            {
                return false;
            }

            return processList.Any(s =>
                processFileName.Equals(s, StringComparison.OrdinalIgnoreCase));
        }

        private static string GetProcessFromRunningProcess()
            => EnvironmentUtilities.ProcessPath;

        private static string GetExecutingAssemblyPath()
            => FileUtilities.ExecutingAssemblyPath;

        private static string GetEnvironmentVariable(string variable)
            => Environment.GetEnvironmentVariable(variable);

        private static class BuildEnvironmentHelperSingleton
        {
            // Explicit static constructor to tell C# compiler
            // not to mark type as beforefieldinit
            static BuildEnvironmentHelperSingleton()
            {
            }

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
    internal sealed class BuildEnvironment
    {
        public BuildEnvironment(BuildEnvironmentMode mode, string currentMSBuildExePath)
        {
            FileInfo currentMSBuildExeFile = null;
            DirectoryInfo currentToolsDirectory = null;

            CurrentMSBuildExePath = currentMSBuildExePath;

            if (!string.IsNullOrEmpty(currentMSBuildExePath))
            {
                currentMSBuildExeFile = new FileInfo(currentMSBuildExePath);
                currentToolsDirectory = currentMSBuildExeFile.Directory;

                CurrentMSBuildToolsDirectory = currentMSBuildExeFile.DirectoryName;
                MSBuildToolsDirectoryRoot = CurrentMSBuildToolsDirectory;
            }

            // We can't detect an environment, don't try to set other paths.
            if (mode == BuildEnvironmentMode.None || currentMSBuildExeFile == null || currentToolsDirectory == null)
            {
                return;
            }

            var msBuildExeName = currentMSBuildExeFile.Name;

            if (mode == BuildEnvironmentMode.VisualStudio)
            {
                // In Visual Studio, the entry-point MSBuild.exe is often from an arch-specific subfolder
                MSBuildToolsDirectoryRoot = !NativeMethodsShared.Is64Bit
                    ? CurrentMSBuildToolsDirectory
                    : currentToolsDirectory.Parent?.FullName;
            }
            else
            {
                // In the .NET SDK, there's one copy of MSBuild.dll and it's in the root folder.
                MSBuildToolsDirectoryRoot = CurrentMSBuildToolsDirectory;

                // If we're standalone, we might not be in the SDK. Rely on folder paths at this point.
                if (string.Equals(currentToolsDirectory.Name, "amd64", StringComparison.OrdinalIgnoreCase))
                {
                    MSBuildToolsDirectoryRoot = currentToolsDirectory.Parent?.FullName;
                }
            }
        }

        /// <summary>
        /// Path to the root of the MSBuild folder (in VS scenarios, <c>MSBuild\Current\bin</c>).
        /// </summary>
        internal string MSBuildToolsDirectoryRoot { get; }

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
    }
}
