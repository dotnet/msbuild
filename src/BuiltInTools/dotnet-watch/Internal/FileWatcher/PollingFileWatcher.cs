// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Diagnostics;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Internal
{
    internal class PollingFileWatcher : IFileSystemWatcher
    {
        // The minimum interval to rerun the scan
        private static readonly TimeSpan _minRunInternal = TimeSpan.FromSeconds(.5);

        private readonly DirectoryInfo _watchedDirectory;

        private Dictionary<string, FileMeta> _knownEntities = new();
        private Dictionary<string, FileMeta> _tempDictionary = new();
        private HashSet<string> _changes = new();

        private Thread _pollingThread;
        private bool _raiseEvents;

        private bool _disposed;

        public PollingFileWatcher(string watchedDirectory)
        {
            Ensure.NotNullOrEmpty(watchedDirectory, nameof(watchedDirectory));

            _watchedDirectory = new DirectoryInfo(watchedDirectory);
            BasePath = _watchedDirectory.FullName;

            _pollingThread = new Thread(new ThreadStart(PollingLoop))
            {
                IsBackground = true,
                Name = nameof(PollingFileWatcher)
            };

            CreateKnownFilesSnapshot();

            _pollingThread.Start();
        }

        public event EventHandler<(string, bool)> OnFileChange;

#pragma warning disable CS0067 // not used
        public event EventHandler<Exception> OnError;
#pragma warning restore

        public string BasePath { get; }

        public bool EnableRaisingEvents
        {
            get => _raiseEvents;
            set
            {
                EnsureNotDisposed();
                _raiseEvents = value;
            }
        }

        private void PollingLoop()
        {
            var stopwatch = Stopwatch.StartNew();
            stopwatch.Start();

            while (!_disposed)
            {
                if (stopwatch.Elapsed < _minRunInternal)
                {
                    // Don't run too often
                    // The min wait time here can be double
                    // the value of the variable (FYI)
                    Thread.Sleep(_minRunInternal);
                }

                stopwatch.Reset();

                if (!_raiseEvents)
                {
                    continue;
                }

                CheckForChangedFiles();
            }

            stopwatch.Stop();
        }

        private void CreateKnownFilesSnapshot()
        {
            _knownEntities.Clear();

            ForeachEntityInDirectory(_watchedDirectory, f =>
            {
                _knownEntities.Add(f.FullName, new FileMeta(f));
            });
        }

        private void CheckForChangedFiles()
        {
            _changes.Clear();

            ForeachEntityInDirectory(_watchedDirectory, f =>
            {
                var fullFilePath = f.FullName;

                if (!_knownEntities.ContainsKey(fullFilePath))
                {
                    // New file
                    RecordChange(f);
                }
                else
                {
                    var fileMeta = _knownEntities[fullFilePath];

                    try
                    {
                        if (fileMeta.FileInfo.LastWriteTime != f.LastWriteTime)
                        {
                            // File changed
                            RecordChange(f);
                        }

                        _knownEntities[fullFilePath] = new FileMeta(fileMeta.FileInfo, true);
                    }
                    catch (FileNotFoundException)
                    {
                        _knownEntities[fullFilePath] = new FileMeta(fileMeta.FileInfo, false);
                    }
                }

                _tempDictionary.Add(f.FullName, new FileMeta(f));
            });

            foreach (var file in _knownEntities)
            {
                if (!file.Value.FoundAgain)
                {
                    // File deleted
                    RecordChange(file.Value.FileInfo);
                }
            }

            NotifyChanges();

            // Swap the two dictionaries
            var swap = _knownEntities;
            _knownEntities = _tempDictionary;
            _tempDictionary = swap;

            _tempDictionary.Clear();
        }

        private void RecordChange(FileSystemInfo fileInfo)
        {
            if (fileInfo == null ||
                _changes.Contains(fileInfo.FullName) ||
                fileInfo.FullName.Equals(_watchedDirectory.FullName, StringComparison.Ordinal))
            {
                return;
            }

            _changes.Add(fileInfo.FullName);
            if (fileInfo.FullName != _watchedDirectory.FullName)
            {
                var file = fileInfo as FileInfo;
                if (file != null)
                {
                    RecordChange(file.Directory);
                }
                else
                {
                    var dir = fileInfo as DirectoryInfo;
                    if (dir != null)
                    {
                        RecordChange(dir.Parent);
                    }
                }
            }
        }

        private void ForeachEntityInDirectory(DirectoryInfo dirInfo, Action<FileSystemInfo> fileAction)
        {
            if (!dirInfo.Exists)
            {
                return;
            }

            IEnumerable<FileSystemInfo> entities;
            try
            {
                entities = dirInfo.EnumerateFileSystemInfos("*.*");
            }
            // If the directory is deleted after the exists check this will throw and could crash the process
            catch (DirectoryNotFoundException)
            {
                return;
            }

            foreach (var entity in entities)
            {
                fileAction(entity);

                var subdirInfo = entity as DirectoryInfo;
                if (subdirInfo != null)
                {
                    ForeachEntityInDirectory(subdirInfo, fileAction);
                }
            }
        }

        private void NotifyChanges()
        {
            foreach (var path in _changes)
            {
                if (_disposed || !_raiseEvents)
                {
                    break;
                }

                if (OnFileChange != null)
                {
                    OnFileChange(this, (path, false));
                }
            }
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(PollingFileWatcher));
            }
        }

        public void Dispose()
        {
            EnableRaisingEvents = false;
            _disposed = true;
        }

        private struct FileMeta
        {
            public FileMeta(FileSystemInfo fileInfo, bool foundAgain = false)
            {
                FileInfo = fileInfo;
                FoundAgain = foundAgain;
            }

            public FileSystemInfo FileInfo;

            public bool FoundAgain;
        }
    }
}
