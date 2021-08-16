// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Internal
{
    public class HotReloadFileSetWatcher : IDisposable
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

        public Task<FileItem[]?> GetChangedFileAsync(CancellationToken cancellationToken)
        {
            EnsureInitialized();

            var tcs = _tcs;
            if (tcs is not null)
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
