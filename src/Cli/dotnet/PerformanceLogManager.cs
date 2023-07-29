// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Configurer;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Cli.Utils
{
    internal sealed class PerformanceLogManager
    {
        internal const string PerfLogDirEnvVar = "DOTNET_PERFLOG_DIR";
        private const string PerfLogRoot = "PerformanceLogs";
        private const int DefaultNumLogsToKeep = 10;

        private IFileSystem _fileSystem;
        private string _perfLogRoot;
        private string _currentLogDir;

        internal static PerformanceLogManager Instance
        {
            get;
            private set;
        }

        internal static void InitializeAndStartCleanup(IFileSystem fileSystem)
        {
            if(Instance == null)
            {
                Instance = new PerformanceLogManager(fileSystem);

                // Check to see if this instance is part of an already running chain of processes.
                string perfLogDir = Env.GetEnvironmentVariable(PerfLogDirEnvVar);
                if (!string.IsNullOrEmpty(perfLogDir))
                {
                        // This process has been provided with a log directory, so use it.
                        Instance.UseExistingLogDirectory(perfLogDir);
                }
                else
                {
                    // This process was not provided with a log root, so make a new one.
                    Instance._perfLogRoot = Path.Combine(CliFolderPathCalculator.DotnetUserProfileFolderPath, PerfLogRoot);
                    Instance.CreateLogDirectory();

                    Task.Factory.StartNew(() =>
                    {
                        Instance.CleanupOldLogs();
                    });
                }
            }
        }

        internal PerformanceLogManager(IFileSystem fileSystem)
        {
            _fileSystem = fileSystem;
        }

        internal string CurrentLogDirectory
        {
            get { return _currentLogDir; }
        }

        private void CreateLogDirectory()
        {
            // Ensure the log root directory exists.
            if(!_fileSystem.Directory.Exists(_perfLogRoot))
            {
                _fileSystem.Directory.CreateDirectory(_perfLogRoot);
            }

            // Create a new perf log directory.
            _currentLogDir = Path.Combine(_perfLogRoot, Guid.NewGuid().ToString("N"));
            _fileSystem.Directory.CreateDirectory(_currentLogDir);
        }

        private void UseExistingLogDirectory(string logDirectory)
        {
            _currentLogDir = logDirectory;
        }

        private void CleanupOldLogs()
        {
            if(_fileSystem.Directory.Exists(_perfLogRoot))
            {
                List<DirectoryInfo> logDirectories = new List<DirectoryInfo>();
                foreach(string directoryPath in _fileSystem.Directory.EnumerateDirectories(_perfLogRoot))
                {
                    logDirectories.Add(new DirectoryInfo(directoryPath));
                }

                // Sort the list.
                logDirectories.Sort(new LogDirectoryComparer());

                // Figure out how many logs to keep.
                int numLogsToKeep;
                string strNumLogsToKeep = Env.GetEnvironmentVariable("DOTNET_PERF_LOG_COUNT");
                if(!int.TryParse(strNumLogsToKeep, out numLogsToKeep))
                {
                    numLogsToKeep = DefaultNumLogsToKeep;

                    // -1 == keep all logs
                    if(numLogsToKeep == -1)
                    {
                        numLogsToKeep = int.MaxValue;
                    }
                }

                // Skip the first numLogsToKeep elements.
                if(logDirectories.Count > numLogsToKeep)
                {
                    // Prune the old logs.
                    for(int i = logDirectories.Count - numLogsToKeep - 1; i>=0; i--)
                    {
                        try
                        {
                            logDirectories[i].Delete(true);
                        }
                        catch
                        {
                            // Do nothing if a log can't be deleted.
                            // We'll get another chance next time around.
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Used to sort log directories when deciding which ones to delete.
    /// </summary>
    internal sealed class LogDirectoryComparer : IComparer<DirectoryInfo>
    {
        int IComparer<DirectoryInfo>.Compare(DirectoryInfo x, DirectoryInfo y)
        {
            return x.CreationTime.CompareTo(y.CreationTime);
        }
    }
}
