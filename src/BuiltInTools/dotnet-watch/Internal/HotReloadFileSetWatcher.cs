// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Collections.Concurrent;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Internal
{
    internal sealed class HotReloadFileSetWatcher : IDisposable
    {
        private static readonly TimeSpan DebounceInterval = TimeSpan.FromMilliseconds(50);
        private readonly FileWatcher _fileWatcher;
        private readonly FileSet _fileSet;
        private readonly object _changedFilesLock = new();
        private volatile ConcurrentDictionary<string, FileItem> _changedFiles = new(StringComparer.Ordinal);
        private TaskCompletionSource<FileItem[]?>? _tcs;
        private bool _initialized;
        private bool _disposed;

        public HotReloadFileSetWatcher(FileSet fileSet, IReporter reporter)
        {
            Ensure.NotNull(fileSet, nameof(fileSet));

            _fileSet = fileSet;
            _fileWatcher = new FileWatcher(reporter);
        }

        private void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            foreach (var file in _fileSet)
            {
                _fileWatcher.WatchDirectory(Path.GetDirectoryName(file.FilePath));
            }

            _fileWatcher.OnFileChange += FileChangedCallback;

            Task.Factory.StartNew(async () =>
            {
                // Debounce / polling loop
                while (!_disposed)
                {
                    await Task.Delay(DebounceInterval);
                    if (_changedFiles.IsEmpty)
                    {
                        continue;
                    }

                    var tcs = Interlocked.Exchange(ref _tcs, null!);
                    if (tcs is null)
                    {
                        continue;
                    }


                    FileItem[] changedFiles;
                    lock (_changedFilesLock)
                    {
                        changedFiles = _changedFiles.Values.ToArray();
                        _changedFiles.Clear();
                    }

                    tcs.TrySetResult(changedFiles);
                }

            }, default, TaskCreationOptions.LongRunning, TaskScheduler.Default);

            void FileChangedCallback(string path, bool newFile)
            {
                if (newFile)
                {
                    lock (_changedFilesLock)
                    {
                        _changedFiles.TryAdd(path, new FileItem { FilePath = path, IsNewFile = newFile });
                    }
                }
                else if (_fileSet.TryGetValue(path, out var fileItem))
                {
                    lock (_changedFilesLock)
                    {
                        _changedFiles.TryAdd(path, fileItem);
                    }
                }
            }
        }

        public Task<FileItem[]?> GetChangedFileAsync(CancellationToken cancellationToken, bool forceWaitForNewUpdate = false)
        {
            EnsureInitialized();

            var tcs = _tcs;
            if (!forceWaitForNewUpdate && tcs is not null)
            {
                return tcs.Task;
            }

            _tcs = tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            cancellationToken.Register(() => tcs.TrySetResult(null));
            return tcs.Task;
        }

        public void Dispose()
        {
            _disposed = true;
            _fileWatcher.Dispose();
        }
    }
}
