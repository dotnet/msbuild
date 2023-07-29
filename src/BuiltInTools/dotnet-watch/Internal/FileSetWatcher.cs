// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Internal
{
    internal sealed class FileSetWatcher : IDisposable
    {
        private readonly FileWatcher _fileWatcher;
        private readonly FileSet _fileSet;

        public FileSetWatcher(FileSet fileSet, IReporter reporter)
        {
            Ensure.NotNull(fileSet, nameof(fileSet));

            _fileSet = fileSet;
            _fileWatcher = new FileWatcher(reporter);
        }

        public async Task<FileItem?> GetChangedFileAsync(CancellationToken cancellationToken, Action startedWatching)
        {
            foreach (var file in _fileSet)
            {
                _fileWatcher.WatchDirectory(Path.GetDirectoryName(file.FilePath));
            }

            var tcs = new TaskCompletionSource<FileItem?>(TaskCreationOptions.RunContinuationsAsynchronously);
            cancellationToken.Register(() => tcs.TrySetResult(null));

            void FileChangedCallback(string path, bool newFile)
            {
                if (_fileSet.TryGetValue(path, out var fileItem))
                {
                    tcs.TrySetResult(fileItem);
                }
            }

            _fileWatcher.OnFileChange += FileChangedCallback;
            startedWatching();
            var changedFile = await tcs.Task;
            _fileWatcher.OnFileChange -= FileChangedCallback;

            return changedFile;
        }

        public Task<FileItem?> GetChangedFileAsync(CancellationToken cancellationToken)
        {
            return GetChangedFileAsync(cancellationToken, () => { });
        }

        public void Dispose()
        {
            _fileWatcher.Dispose();
        }
    }
}
