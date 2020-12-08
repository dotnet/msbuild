// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;

#if FEATURE_FILE_TRACKER

namespace Microsoft.Build.Utilities
{
    /// <summary>
    /// Enumeration to express the type of executable being wrapped by Tracker.exe
    /// </summary>
    public enum ExecutableType
    {
        /// <summary>
        /// 32-bit native executable
        /// </summary>
        Native32Bit = 0,

        /// <summary>
        /// 64-bit native executable 
        /// </summary>
        Native64Bit = 1,

        /// <summary>
        /// A managed executable without a specified bitness
        /// </summary>
        ManagedIL = 2,

        /// <summary>
        /// A managed executable specifically marked as 32-bit
        /// </summary>
        Managed32Bit = 3,

        /// <summary>
        /// A managed executable specifically marked as 64-bit
        /// </summary>
        Managed64Bit = 4,

        /// <summary>
        /// Use the same bitness as the currently running executable. 
        /// </summary>
        SameAsCurrentProcess = 5
    }

    /// <summary>
    /// This class contains utility functions to encapsulate launching and logging for the Tracker
    /// </summary>
    public static class FileTracker
    {
        #region Static Member Data

        // The default path to temp, used to create explicitly short and long paths
        private static readonly string s_tempPath = Path.GetTempPath();

        // The short path to temp
        private static readonly string s_tempShortPath = FileUtilities.EnsureTrailingSlash(NativeMethodsShared.GetShortFilePath(s_tempPath).ToUpperInvariant());

        // The long path to temp
        private static readonly string s_tempLongPath = FileUtilities.EnsureTrailingSlash(NativeMethodsShared.GetLongFilePath(s_tempPath).ToUpperInvariant());

        // The path to ApplicationData (is equal to %USERPROFILE%\Application Data folder in Windows XP and %USERPROFILE%\AppData\Roaming in Vista and later)
        private static readonly string s_applicationDataPath = FileUtilities.EnsureTrailingSlash(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData).ToUpperInvariant());

        // The path to LocalApplicationData (is equal to %USERPROFILE%\Local Settings\Application Data folder in Windows XP and %USERPROFILE%\AppData\Local in Vista and later).
        private static readonly string s_localApplicationDataPath = FileUtilities.EnsureTrailingSlash(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData).ToUpperInvariant());

        // The path to the LocalLow folder. In Vista and later, user application data is organized across %USERPROFILE%\AppData\LocalLow,  %USERPROFILE%\AppData\Local (%LOCALAPPDATA%) 
        // and %USERPROFILE%\AppData\Roaming (%APPDATA%). The LocalLow folder is not present in XP.
        private static readonly string s_localLowApplicationDataPath = FileUtilities.EnsureTrailingSlash(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData\\LocalLow").ToUpperInvariant());

        // The path to the common Application Data, which is also used by some programs (e.g. antivirus) that we wish to ignore.
        // Is equal to C:\Documents and Settings\All Users\Application Data on XP, and C:\ProgramData on Vista+.
        // But for backward compatibility, the paths "C:\Documents and Settings\All Users\Application Data" and "C:\Users\All Users\Application Data" are still accessible via Junction point on Vista+.
        // Thus this list is created to store all possible common application data paths to cover more cases as possible.
        private static readonly List<string> s_commonApplicationDataPaths;

        // The name of the standalone tracker tool.
        private static readonly string s_TrackerFilename = "Tracker.exe";

        // The name of the assembly that is injected into the executing process.
        // Detours handles picking between FileTracker{32,64}.dll so only mention one.
        private static readonly string s_FileTrackerFilename = "FileTracker32.dll";

        // The name of the PATH environment variable.
        private const string pathEnvironmentVariableName = "PATH";

        // Static cache of the path separator character in an array for use in String.Split.
        private static readonly char[] pathSeparatorArray = { Path.PathSeparator };

        // Static cache of the path separator character in an array for use in String.Split.
        private static readonly string pathSeparator = Path.PathSeparator.ToString();

        #endregion

        #region Static constructor

        static FileTracker()
        {
            s_commonApplicationDataPaths = new List<string>();

            string defaultCommonApplicationDataPath = FileUtilities.EnsureTrailingSlash(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData).ToUpperInvariant());
            s_commonApplicationDataPaths.Add(defaultCommonApplicationDataPath);

            string defaultRootDirectory = Path.GetPathRoot(defaultCommonApplicationDataPath);
            string alternativeCommonApplicationDataPath1 = FileUtilities.EnsureTrailingSlash(Path.Combine(defaultRootDirectory, @"Documents and Settings\All Users\Application Data").ToUpperInvariant());

            if (!alternativeCommonApplicationDataPath1.Equals(defaultCommonApplicationDataPath, StringComparison.Ordinal))
            {
                s_commonApplicationDataPaths.Add(alternativeCommonApplicationDataPath1);
            }

            string alternativeCommonApplicationDataPath2 = FileUtilities.EnsureTrailingSlash(Path.Combine(defaultRootDirectory, @"Users\All Users\Application Data").ToUpperInvariant());

            if (!alternativeCommonApplicationDataPath2.Equals(defaultCommonApplicationDataPath, StringComparison.Ordinal))
            {
                s_commonApplicationDataPaths.Add(alternativeCommonApplicationDataPath2);
            }
        }

        #endregion

        #region Native method wrappers

        /// <summary>
        /// Stops tracking file accesses.  
        /// </summary>
        public static void EndTrackingContext() => InprocTrackingNativeMethods.EndTrackingContext();

        /// <summary>
        /// Resume tracking file accesses in the current tracking context. 
        /// </summary>
        public static void ResumeTracking() => InprocTrackingNativeMethods.ResumeTracking();

        /// <summary>
        /// Set the global thread count, and assign that count to the current thread. 
        /// </summary>
        public static void SetThreadCount(int threadCount) => InprocTrackingNativeMethods.SetThreadCount(threadCount);

        /// <summary>
        /// Starts tracking file accesses. 
        /// </summary>
        /// <param name="intermediateDirectory">The directory into which to write the tracking log files</param>
        /// <param name="taskName">The name of the task calling this function, used to determine the 
        /// names of the tracking log files</param>
        public static void StartTrackingContext(string intermediateDirectory, string taskName) => InprocTrackingNativeMethods.StartTrackingContext(intermediateDirectory, taskName);

        /// <summary>
        /// Starts tracking file accesses, using the rooting marker in the response file provided.  To 
        /// automatically generate a response file given a rooting marker, call 
        /// FileTracker.CreateRootingMarkerResponseFile. 
        /// </summary>
        /// <param name="intermediateDirectory">The directory into which to write the tracking log files</param>
        /// <param name="taskName">The name of the task calling this function, used to determine the 
        /// names of the tracking log files</param>
        /// <param name="rootMarkerResponseFile">The path to the root marker response file.</param>
        public static void StartTrackingContextWithRoot(string intermediateDirectory, string taskName, string rootMarkerResponseFile)
            => InprocTrackingNativeMethods.StartTrackingContextWithRoot(intermediateDirectory, taskName, rootMarkerResponseFile);

        /// <summary>
        /// Stop tracking file accesses and get rid of the current tracking contexts. 
        /// </summary>
        public static void StopTrackingAndCleanup() => InprocTrackingNativeMethods.StopTrackingAndCleanup();

        /// <summary>
        /// Temporarily suspend tracking of file accesses in the current tracking context. 
        /// </summary>
        public static void SuspendTracking() => InprocTrackingNativeMethods.SuspendTracking();

        /// <summary>
        /// Write tracking logs for all contexts and threads. 
        /// </summary>
        /// <param name="intermediateDirectory">The directory into which to write the tracking log files</param>
        /// <param name="taskName">The name of the task calling this function, used to determine the 
        /// names of the tracking log files</param>
        [SuppressMessage("Microsoft.Naming", "CA1702:CompoundWordsShouldBeCasedCorrectly", MessageId = "TLogs", Justification = "Has now shipped as public API; plus it's unclear whether 'Tlog' or 'TLog' is the preferred casing")]
        public static void WriteAllTLogs(string intermediateDirectory, string taskName) => InprocTrackingNativeMethods.WriteAllTLogs(intermediateDirectory, taskName);

        /// <summary>
        /// Write tracking logs corresponding to the current tracking context.  
        /// </summary>
        /// <param name="intermediateDirectory">The directory into which to write the tracking log files</param>
        /// <param name="taskName">The name of the task calling this function, used to determine the 
        /// names of the tracking log files</param>
        [SuppressMessage("Microsoft.Naming", "CA1702:CompoundWordsShouldBeCasedCorrectly", MessageId = "TLogs", Justification = "Has now shipped as public API; plus it's unclear whether 'Tlog' or 'TLog' is the preferred casing")]
        public static void WriteContextTLogs(string intermediateDirectory, string taskName) => InprocTrackingNativeMethods.WriteContextTLogs(intermediateDirectory, taskName);

        #endregion // Native method wrappers

        #region Methods

        /// <summary>
        /// Test to see if the specified file is excluded from tracked dependencies
        /// </summary>
        /// <param name="fileName">
        /// Full path of the file to test
        /// </param>
        public static bool FileIsExcludedFromDependencies(string fileName)
        {
            // UNDONE: This check means that we cannot incremental build projects
            // that exist under the following directories on XP:
            // %USERPROFILE%\Application Data
            // %USERPROFILE%\Local Settings\Application Data
            //
            // and the following directories on Vista:
            // %USERPROFILE%\AppData\Local
            // %USERPROFILE%\AppData\LocalLow
            // %USERPROFILE%\AppData\Roaming

            // We don't want to be including these as dependencies or outputs:
            // 1. Files under %USERPROFILE%\Application Data in XP and %USERPROFILE%\AppData\Roaming in Vista and later.
            // 2. Files under %USERPROFILE%\Local Settings\Application Data in XP and %USERPROFILE%\AppData\Local in Vista and later.
            // 3. Files under %USERPROFILE%\AppData\LocalLow in Vista and later.
            // 4. Files that are in the TEMP directory (Since on XP, temp files are not
            //    located under AppData, they would not be compacted out correctly otherwise).
            // 5. Files under the common ("All Users") Application Data location -- C:\Documents and Settings\All Users\Application Data 
            //    on XP and either C:\Users\All Users\Application Data or C:\ProgramData on Vista+

            return FileIsUnderPath(fileName, s_applicationDataPath) ||
                   FileIsUnderPath(fileName, s_localApplicationDataPath) ||
                   FileIsUnderPath(fileName, s_localLowApplicationDataPath) ||
                   FileIsUnderPath(fileName, s_tempShortPath) ||
                   FileIsUnderPath(fileName, s_tempLongPath) ||
                   s_commonApplicationDataPaths.Any(p => FileIsUnderPath(fileName, p));
        }

        /// <summary>
        /// Test to see if the specified file is under the specified path
        /// </summary>
        /// <param name="fileName">
        /// Full path of the file to test
        /// </param>
        /// <param name="path">
        /// Is the file under this full path?
        /// </param>
        public static bool FileIsUnderPath(string fileName, string path)
        {
            // UNDONE: Get the long file path for the entry
            // This is an incredibly expensive operation. The tracking log
            // as written by CL etc. does not contain short paths
            // fileDirectory = NativeMethods.GetFullLongFilePath(fileDirectory);

            // Ensure that the path has a trailing slash that we are checking under
            // By default the paths that we check for most often will have, so this will
            // return fast and not allocate memory in the process
            path = FileUtilities.EnsureTrailingSlash(path);

            // Is the fileName under the filePath?
            return string.Compare(fileName, 0, path, 0, path.Length, StringComparison.OrdinalIgnoreCase) == 0;
        }

        /// <summary>
        /// Construct a rooting marker string from the ITaskItem array of primary sources.
        /// </summary>
        /// <param name="source">An <see cref="ITaskItem"/> containing information about the primary source.</param>
        public static string FormatRootingMarker(ITaskItem source) => FormatRootingMarker(new[] { source }, null);

        /// <summary>
        /// Construct a rooting marker string from the ITaskItem array of primary sources.
        /// </summary>
        /// <param name="source">An <see cref="ITaskItem"/> containing information about the primary source.</param>
        /// <param name="output">An <see cref="ITaskItem"/> containing information about the output.</param>
        public static string FormatRootingMarker(ITaskItem source, ITaskItem output) => FormatRootingMarker(new[] { source }, new[] { output });

        /// <summary>
        /// Construct a rooting marker string from the ITaskItem array of primary sources.
        /// </summary>
        /// <param name="sources">
        /// ITaskItem array of primary sources.
        /// </param>
        public static string FormatRootingMarker(ITaskItem[] sources) => FormatRootingMarker(sources, null);

        /// <summary>
        /// Construct a rooting marker string from the ITaskItem array of primary sources.
        /// </summary>
        /// <param name="sources">
        /// ITaskItem array of primary sources.
        /// </param>
        /// <param name="outputs">ITaskItem array of outputs.</param>
        public static string FormatRootingMarker(ITaskItem[] sources, ITaskItem[] outputs)
        {
            ErrorUtilities.VerifyThrowArgumentNull(sources, nameof(sources));

            // So we don't have to deal with null checks.
            outputs ??= Array.Empty<ITaskItem>();

            var rootSources = new List<string>(sources.Length + outputs.Length);

            foreach (ITaskItem source in sources)
            {
                rootSources.Add(FileUtilities.NormalizePath(source.ItemSpec).ToUpperInvariant());
            }

            foreach (ITaskItem output in outputs)
            {
                rootSources.Add(FileUtilities.NormalizePath(output.ItemSpec).ToUpperInvariant());
            }

            rootSources.Sort(StringComparer.OrdinalIgnoreCase);

            return string.Join("|", rootSources);
        }

        /// <summary>
        /// Given a set of source files in the form of ITaskItem, creates a temporary response
        /// file containing the rooting marker that corresponds to those sources. 
        /// </summary>
        /// <param name="sources">
        /// ITaskItem array of primary sources.
        /// </param>
        /// <returns>The response file path.</returns>
        public static string CreateRootingMarkerResponseFile(ITaskItem[] sources) => CreateRootingMarkerResponseFile(FormatRootingMarker(sources));

        /// <summary>
        /// Given a rooting marker, creates a temporary response file with that rooting marker 
        /// in it.
        /// </summary>
        /// <param name="rootMarker">The rooting marker to put in the response file.</param>
        /// <returns>The response file path.</returns>
        public static string CreateRootingMarkerResponseFile(string rootMarker)
        {
            string trackerResponseFile = FileUtilities.GetTemporaryFile(".rsp");
            File.WriteAllText(trackerResponseFile, "/r \"" + rootMarker + "\"", Encoding.Unicode);

            return trackerResponseFile;
        }

        /// <summary>
        /// Prepends the path to the appropriate FileTracker assembly to the PATH
        /// environment variable.  Used for inproc tracking, or when the .NET Framework may 
        /// not be on the PATH.
        /// </summary>
        /// <returns>The old value of PATH</returns>
        public static string EnsureFileTrackerOnPath() => EnsureFileTrackerOnPath(null);

        /// <summary>
        /// Prepends the path to the appropriate FileTracker assembly to the PATH
        /// environment variable.  Used for inproc tracking, or when the .NET Framework may 
        /// not be on the PATH.
        /// </summary>
        /// <param name="rootPath">The root path for FileTracker.dll.  Overrides the toolType if specified.</param>
        /// <returns>The old value of PATH</returns>
        public static string EnsureFileTrackerOnPath(string rootPath)
        {
            string oldPath = Environment.GetEnvironmentVariable(pathEnvironmentVariableName);
            string fileTrackerPath = GetFileTrackerPath(ExecutableType.SameAsCurrentProcess, rootPath);

            if (!string.IsNullOrEmpty(fileTrackerPath))
            {
                Environment.SetEnvironmentVariable(pathEnvironmentVariableName, Path.GetDirectoryName(fileTrackerPath) + pathSeparator + oldPath);
            }

            return oldPath;
        }

        /// <summary>
        /// Searches %PATH% for the location of Tracker.exe, and returns the first 
        /// path that matches. 
        /// <returns>Matching full path to Tracker.exe or null if a matching path is not found.</returns>
        /// </summary>
        public static string FindTrackerOnPath()
        {
            string[] paths = Environment.GetEnvironmentVariable(pathEnvironmentVariableName).Split(pathSeparatorArray, StringSplitOptions.RemoveEmptyEntries);

            foreach (string path in paths)
            {
                try
                {
                    string trackerPath = !Path.IsPathRooted(path)
                        ? Path.GetFullPath(path)
                        : path;

                    trackerPath = Path.Combine(trackerPath, s_TrackerFilename);

                    if (FileSystems.Default.FileExists(trackerPath))
                    {
                        return trackerPath;
                    }
                }
                catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
                {
                    // ignore this path and move on -- it's just bad for some reason. 
                }
            }

            // Still haven't found it.
            return null;
        }

        /// <summary>
        /// Determines whether we must track out-of-proc, or whether inproc tracking will work. 
        /// </summary>
        /// <param name="toolType">The executable type for the tool being tracked</param>
        /// <returns>True if we need to track out-of-proc, false if inproc tracking is OK</returns>
        public static bool ForceOutOfProcTracking(ExecutableType toolType) => ForceOutOfProcTracking(toolType, null, null);

        /// <summary>
        /// Determines whether we must track out-of-proc, or whether inproc tracking will work. 
        /// </summary>
        /// <param name="toolType">The executable type for the tool being tracked</param>
        /// <param name="dllName">An optional assembly name.</param>
        /// <param name="cancelEventName">The name of the cancel event tracker should listen for, or null if there isn't one</param>
        /// <returns>True if we need to track out-of-proc, false if inproc tracking is OK</returns>
        public static bool ForceOutOfProcTracking(ExecutableType toolType, string dllName, string cancelEventName)
        {
            bool trackOutOfProc = false;

            if (cancelEventName != null)
            {
                // If we have a cancel event, we must track out-of-proc. 
                trackOutOfProc = true;
            }
            else if (dllName != null)
            {
                // If we have a DLL name, we need to track out of proc -- inproc tracking just uses
                // the default FileTracker
                trackOutOfProc = true;
            }

            // toolType is not relevant now that Detours can handle child processes of a different
            // bitness than the parent.

            return trackOutOfProc;
        }

        /// <summary>
        /// Given the ExecutableType of the tool being wrapped and information that we 
        /// know about our current bitness, figures out and returns the path to the correct
        /// Tracker.exe. 
        /// </summary>
        /// <param name="toolType">The <see cref="ExecutableType"/> of the tool being wrapped</param>
        public static string GetTrackerPath(ExecutableType toolType) => GetTrackerPath(toolType, null);

        /// <summary>
        /// Given the ExecutableType of the tool being wrapped and information that we 
        /// know about our current bitness, figures out and returns the path to the correct
        /// Tracker.exe. 
        /// </summary>
        /// <param name="toolType">The <see cref="ExecutableType"/> of the tool being wrapped</param>
        /// <param name="rootPath">The root path for Tracker.exe.  Overrides the toolType if specified.</param>
        public static string GetTrackerPath(ExecutableType toolType, string rootPath) => GetPath(s_TrackerFilename, toolType, rootPath);

        /// <summary>
        /// Given the ExecutableType of the tool being wrapped and information that we 
        /// know about our current bitness, figures out and returns the path to the correct
        /// FileTracker.dll. 
        /// </summary>
        /// <param name="toolType">The <see cref="ExecutableType"/> of the tool being wrapped</param>
        public static string GetFileTrackerPath(ExecutableType toolType) => GetFileTrackerPath(toolType, null);

        /// <summary>
        /// Given the ExecutableType of the tool being wrapped and information that we 
        /// know about our current bitness, figures out and returns the path to the correct
        /// FileTracker.dll. 
        /// </summary>
        /// <param name="toolType">The <see cref="ExecutableType"/> of the tool being wrapped</param>
        /// <param name="rootPath">The root path for FileTracker.dll.  Overrides the toolType if specified.</param>
        public static string GetFileTrackerPath(ExecutableType toolType, string rootPath) => GetPath(s_FileTrackerFilename, toolType, rootPath);

        /// <summary>
        /// Given a filename (only really meant to support either Tracker.exe or FileTracker.dll), returns
        /// the appropriate path for the appropriate file type. 
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="toolType"></param>
        /// <param name="rootPath">The root path for the file.  Overrides the toolType if specified.</param>
        private static string GetPath(string filename, ExecutableType toolType, string rootPath)
        {
            string trackerPath;

            if (!string.IsNullOrEmpty(rootPath))
            {
                trackerPath = Path.Combine(rootPath, filename);

                if (!FileSystems.Default.FileExists(trackerPath))
                {
                    // if an override path was specified, that's it -- we don't want to fall back if the file
                    // is not found there.
                    trackerPath = null;
                }
            }
            else
            {
                // Since Detours can handle cross-bitness process launches, the toolType
                // can be ignored; just return the path corresponding to the current architecture.
                trackerPath = GetPath(filename, DotNetFrameworkArchitecture.Current);
            }

            return trackerPath;
        }

        /// <summary>
        /// Given a filename (currently only Tracker.exe and FileTracker.dll are supported), return 
        /// the path to that file. 
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="bitness"></param>
        /// <returns></returns>
        private static string GetPath(string filename, DotNetFrameworkArchitecture bitness)
        {
            // Make sure that if someone starts passing the wrong thing to this method we don't silently 
            // eat it and do something possibly unexpected. 
            ErrorUtilities.VerifyThrow(
                                       s_TrackerFilename.Equals(filename, StringComparison.OrdinalIgnoreCase) ||
                                       s_FileTrackerFilename.Equals(filename, StringComparison.OrdinalIgnoreCase),
                                       "This method should only be passed s_TrackerFilename or s_FileTrackerFilename, but was passed {0} instead!",
                                       filename
                                       );

            // Look for FileTracker.dll/Tracker.exe in the MSBuild tools directory. They may exist elsewhere on disk,
            // but other copies aren't guaranteed to be compatible with the latest.
            var path = ToolLocationHelper.GetPathToBuildToolsFile(filename, ToolLocationHelper.CurrentToolsVersion, bitness);

            // Due to a Detours limitation, the path to FileTracker32.dll must be
            // representable in ANSI characters. Look for it first in the global
            // shared location which is guaranteed to be ANSI. Fall back to
            // current folder.
            if (s_FileTrackerFilename.Equals(filename, StringComparison.OrdinalIgnoreCase))
            {
                string progfilesPath = Path.Combine(FrameworkLocationHelper.GenerateProgramFiles32(),
                    "MSBuild", "15.0", "FileTracker", s_FileTrackerFilename);

                if (FileSystems.Default.FileExists(progfilesPath))
                {
                    return progfilesPath;
                }
            }

            return path;
        }

        /// <summary>
        /// This method constructs the correct Tracker.exe response file arguments from its parameters
        /// </summary>
        /// <param name="dllName">The name of the dll that will do the tracking</param>
        /// <param name="intermediateDirectory">Intermediate directory where tracking logs will be written</param>
        /// <param name="rootFiles">Rooting marker</param>
        /// <returns>The arguments as a string</returns>
        public static string TrackerResponseFileArguments(string dllName, string intermediateDirectory, string rootFiles)
            => TrackerResponseFileArguments(dllName, intermediateDirectory, rootFiles, null);

        /// <summary>
        /// This method constructs the correct Tracker.exe response file arguments from its parameters
        /// </summary>
        /// <param name="dllName">The name of the dll that will do the tracking</param>
        /// <param name="intermediateDirectory">Intermediate directory where tracking logs will be written</param>
        /// <param name="rootFiles">Rooting marker</param>
        /// <param name="cancelEventName">If a cancel event has been created that Tracker should be listening for, its name is passed here</param>
        /// <returns>The arguments as a string</returns>
        public static string TrackerResponseFileArguments(string dllName, string intermediateDirectory, string rootFiles, string cancelEventName)
        {
            CommandLineBuilder builder = new CommandLineBuilder();

            builder.AppendSwitchIfNotNull("/d ", dllName);

            if (!string.IsNullOrEmpty(intermediateDirectory))
            {
                intermediateDirectory = FileUtilities.NormalizePath(intermediateDirectory);
                // If the intermediate directory ends up with a trailing slash, then be rid of it!
                if (FileUtilities.EndsWithSlash(intermediateDirectory))
                {
                    intermediateDirectory = Path.GetDirectoryName(intermediateDirectory);
                }
                builder.AppendSwitchIfNotNull("/i ", intermediateDirectory);
            }

            builder.AppendSwitchIfNotNull("/r ", rootFiles);

            builder.AppendSwitchIfNotNull("/b ", cancelEventName); // b for break

            return builder.ToString() + " ";
        }

        /// <summary>
        /// This method constructs the correct Tracker.exe command arguments from its parameters
        /// </summary>
        /// <param name="command">The command to track</param>
        /// <param name="arguments">The command to track's arguments</param>
        /// <returns>The arguments as a string</returns>
        public static string TrackerCommandArguments(string command, string arguments)
        {
            CommandLineBuilder builder = new CommandLineBuilder();

            builder.AppendSwitch(" /c");
            builder.AppendFileNameIfNotNull(command);

            string fullArguments = builder.ToString();

            fullArguments += " " + arguments;

            return fullArguments;
        }

        /// <summary>
        /// This method constructs the correct Tracker.exe arguments from its parameters
        /// </summary>
        /// <param name="command">The command to track</param>
        /// <param name="arguments">The command to track's arguments</param>
        /// <param name="dllName">The name of the dll that will do the tracking</param>
        /// <param name="intermediateDirectory">Intermediate directory where tracking logs will be written</param>
        /// <param name="rootFiles">Rooting marker</param>
        /// <returns>The arguments as a string</returns>
        public static string TrackerArguments(string command, string arguments, string dllName, string intermediateDirectory, string rootFiles)
            => TrackerArguments(command, arguments, dllName, intermediateDirectory, rootFiles, null);

        /// <summary>
        /// This method constructs the correct Tracker.exe arguments from its parameters
        /// </summary>
        /// <param name="command">The command to track</param>
        /// <param name="arguments">The command to track's arguments</param>
        /// <param name="dllName">The name of the dll that will do the tracking</param>
        /// <param name="intermediateDirectory">Intermediate directory where tracking logs will be written</param>
        /// <param name="rootFiles">Rooting marker</param>
        /// <param name="cancelEventName">If a cancel event has been created that Tracker should be listening for, its name is passed here</param>
        /// <returns>The arguments as a string</returns>
        public static string TrackerArguments(string command, string arguments, string dllName, string intermediateDirectory, string rootFiles, string cancelEventName)
            => TrackerResponseFileArguments(dllName, intermediateDirectory, rootFiles, cancelEventName) + TrackerCommandArguments(command, arguments);

        #region StartProcess methods

        /// <summary>
        /// Start the process; tracking the command.  
        /// </summary>
        /// <param name="command">The command to track</param>
        /// <param name="arguments">The command to track's arguments</param>
        /// <param name="toolType">The type of executable the wrapped tool is</param>
        /// <param name="dllName">The name of the dll that will do the tracking</param>
        /// <param name="intermediateDirectory">Intermediate directory where tracking logs will be written</param>
        /// <param name="rootFiles">Rooting marker</param>
        /// <param name="cancelEventName">If Tracker should be listening on a particular event for cancellation, pass its name here</param>
        /// <returns>Process instance</returns>
        public static Process StartProcess(string command, string arguments, ExecutableType toolType, string dllName, string intermediateDirectory, string rootFiles, string cancelEventName)
        {
            dllName ??= GetFileTrackerPath(toolType);

            string fullArguments = TrackerArguments(command, arguments, dllName, intermediateDirectory, rootFiles, cancelEventName);
            return Process.Start(GetTrackerPath(toolType), fullArguments);
        }

        /// <summary>
        /// Start the process; tracking the command.
        /// </summary>
        /// <param name="command">The command to track</param>
        /// <param name="arguments">The command to track's arguments</param>
        /// <param name="toolType">The type of executable the wrapped tool is</param>
        /// <param name="dllName">The name of the dll that will do the tracking</param>
        /// <param name="intermediateDirectory">Intermediate directory where tracking logs will be written</param>
        /// <param name="rootFiles">Rooting marker</param>
        /// <returns>Process instance</returns>
        public static Process StartProcess(string command, string arguments, ExecutableType toolType, string dllName, string intermediateDirectory, string rootFiles)
            => StartProcess(command, arguments, toolType, dllName, intermediateDirectory, rootFiles, null);

        /// <summary>
        /// Start the process; tracking the command.
        /// </summary>
        /// <param name="command">The command to track</param>
        /// <param name="arguments">The command to track's arguments</param>
        /// <param name="toolType">The type of executable the wrapped tool is</param>
        /// <param name="intermediateDirectory">Intermediate directory where tracking logs will be written</param>
        /// <param name="rootFiles">Rooting marker</param>
        /// <returns>Process instance</returns>
        public static Process StartProcess(string command, string arguments, ExecutableType toolType, string intermediateDirectory, string rootFiles)
            => StartProcess(command, arguments, toolType, null, intermediateDirectory, rootFiles, null);

        /// <summary>
        /// Start the process; tracking the command.
        /// </summary>
        /// <param name="command">The command to track</param>
        /// <param name="arguments">The command to track's arguments</param>
        /// <param name="toolType">The type of executable the wrapped tool is</param>
        /// <param name="rootFiles">Rooting marker</param>
        /// <returns>Process instance</returns>
        public static Process StartProcess(string command, string arguments, ExecutableType toolType, string rootFiles)
            => StartProcess(command, arguments, toolType, null, null, rootFiles, null);

        /// <summary>
        /// Start the process; tracking the command.
        /// </summary>
        /// <param name="command">The command to track</param>
        /// <param name="arguments">The command to track's arguments</param>
        /// <param name="toolType">The type of executable the wrapped tool is</param>
        /// <returns>Process instance</returns>
        public static Process StartProcess(string command, string arguments, ExecutableType toolType)
            => StartProcess(command, arguments, toolType, null, null, null, null);

        #endregion // StartProcess methods

        /// <summary>
        /// Logs a message of the given importance using the specified resource string. To the specified Log.
        /// </summary>
        /// <remarks>This method is not thread-safe.</remarks>
        /// <param name="Log">The Log to log to.</param>
        /// <param name="importance">The importance level of the message.</param>
        /// <param name="messageResourceName">The name of the string resource to load.</param>
        /// <param name="messageArgs">Optional arguments for formatting the loaded string.</param>
        /// <exception cref="ArgumentNullException">Thrown when <c>messageResourceName</c> is null.</exception>
        internal static void LogMessageFromResources(TaskLoggingHelper Log, MessageImportance importance, string messageResourceName, params object[] messageArgs)
        {
            // Only log when we have been passed a TaskLoggingHelper
            if (Log != null)
            {
                ErrorUtilities.VerifyThrowArgumentNull(messageResourceName, nameof(messageResourceName));

                Log.LogMessage(importance, AssemblyResources.GetString(messageResourceName), messageArgs);
            }
        }

        /// <summary>
        /// Logs a message of the given importance using the specified string.
        /// </summary>
        /// <remarks>This method is not thread-safe.</remarks>
        /// <param name="Log">The Log to log to.</param>
        /// <param name="importance">The importance level of the message.</param>
        /// <param name="message">The message string.</param>
        /// <param name="messageArgs">Optional arguments for formatting the message string.</param>
        /// <exception cref="ArgumentNullException">Thrown when <c>message</c> is null.</exception>
        internal static void LogMessage(TaskLoggingHelper Log, MessageImportance importance, string message, params object[] messageArgs)
        {
            // Only log when we have been passed a TaskLoggingHelper
            Log?.LogMessage(importance, message, messageArgs);
        }

        /// <summary>
        /// Logs a warning using the specified resource string.
        /// </summary>
        /// <param name="Log">The Log to log to.</param>
        /// <param name="messageResourceName">The name of the string resource to load.</param>
        /// <param name="messageArgs">Optional arguments for formatting the loaded string.</param>
        /// <exception cref="ArgumentNullException">Thrown when <c>messageResourceName</c> is null.</exception>
        internal static void LogWarningWithCodeFromResources(TaskLoggingHelper Log, string messageResourceName, params object[] messageArgs)
        {
            // Only log when we have been passed a TaskLoggingHelper
            Log?.LogWarningWithCodeFromResources(messageResourceName, messageArgs);
        }

        #endregion
    }

    /// <summary>
    /// Dependency filter delegate. Used during TLog saves in order for tasks to selectively remove dependencies from the written
    /// graph.
    /// </summary>
    /// <param name="fullPath">The full path to the dependency file about to be written to the compacted TLog</param>
    /// <returns>If the file should actually be written to the TLog (true) or not (false)</returns>
    public delegate bool DependencyFilter(string fullPath);
}

#endif
