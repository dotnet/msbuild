// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Internal
{
    internal sealed class FileWatcher
    {
        private bool _disposed;

        private readonly IDictionary<string, IFileSystemWatcher> _watchers;
        private readonly IReporter _reporter;

        public FileWatcher()
            : this(NullReporter.Singleton)
        { }

        public FileWatcher(IReporter reporter)
        {
            _reporter = reporter ?? throw new ArgumentNullException(nameof(reporter));
            _watchers = new Dictionary<string, IFileSystemWatcher>();
        }

        public event Action<string, bool> OnFileChange;

        public void WatchDirectory(string directory)
        {
            EnsureNotDisposed();
            AddDirectoryWatcher(directory);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            foreach (var watcher in _watchers)
            {
                watcher.Value.OnFileChange -= WatcherChangedHandler;
                watcher.Value.OnError -= WatcherErrorHandler;
                watcher.Value.Dispose();
            }

            _watchers.Clear();
        }

        private void AddDirectoryWatcher(string directory)
        {
            directory = EnsureTrailingSlash(directory);

            var alreadyWatched = _watchers
                .Where(d => directory.StartsWith(d.Key))
                .Any();

            if (alreadyWatched)
            {
                return;
            }

            var redundantWatchers = _watchers
                .Where(d => d.Key.StartsWith(directory))
                .Select(d => d.Key)
                .ToList();

            if (redundantWatchers.Any())
            {
                foreach (var watcher in redundantWatchers)
                {
                    DisposeWatcher(watcher);
                }
            }

            var newWatcher = FileWatcherFactory.CreateWatcher(directory);
            newWatcher.OnFileChange += WatcherChangedHandler;
            newWatcher.OnError += WatcherErrorHandler;
            newWatcher.EnableRaisingEvents = true;

            _watchers.Add(directory, newWatcher);
        }

        private void WatcherErrorHandler(object sender, Exception error)
        {
            if (sender is IFileSystemWatcher watcher)
            {
                _reporter.Warn($"The file watcher observing '{watcher.BasePath}' encountered an error: {error.Message}");
            }
        }

        private void WatcherChangedHandler(object sender, (string changedPath, bool newFile) args)
        {
            NotifyChange(args.changedPath, args.newFile);
        }

        private void NotifyChange(string path, bool newFile)
        {
            if (OnFileChange != null)
            {
                OnFileChange(path, newFile);
            }
        }

        private void DisposeWatcher(string directory)
        {
            var watcher = _watchers[directory];
            _watchers.Remove(directory);

            watcher.EnableRaisingEvents = false;

            watcher.OnFileChange -= WatcherChangedHandler;
            watcher.OnError -= WatcherErrorHandler;

            watcher.Dispose();
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(FileWatcher));
            }
        }

        private static string EnsureTrailingSlash(string path)
        {
            if (!string.IsNullOrEmpty(path) &&
                path[path.Length - 1] != Path.DirectorySeparatorChar)
            {
                return path + Path.DirectorySeparatorChar;
            }

            return path;
        }
    }
}
