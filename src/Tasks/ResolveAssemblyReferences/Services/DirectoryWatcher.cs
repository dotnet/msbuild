using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Services
{
    internal class DirectoryWatcher : IDirectoryWatcher
    {

        private Dictionary<string, FileSystemWatcher> DirectoryToWatcher { get; } =
            new Dictionary<string, FileSystemWatcher>(StringComparer.OrdinalIgnoreCase);

        private ConcurrentQueue<FileSystemChange> FileSystemChangeQueue { get; } = new ConcurrentQueue<FileSystemChange>();

        private List<FileSystemChange> NoChanges { get; } = new List<FileSystemChange>();

        public List<FileSystemChange> RecentChanges
        {
            get
            {
                if (FileSystemChangeQueue.IsEmpty)
                {
                    return NoChanges;
                }

                var recentChanges = new List<FileSystemChange>(FileSystemChangeQueue.Count);

                while (!FileSystemChangeQueue.IsEmpty)
                {
                    if (FileSystemChangeQueue.TryDequeue(out FileSystemChange fileSystemChange))
                    {
                        recentChanges.Add(fileSystemChange);
                    }
                }

                return recentChanges;
            }
        }

        public void Watch(string directory)
        {
            bool isWatching = DirectoryToWatcher.ContainsKey(directory);

            if (isWatching)
            {
                return;
            }

            var watcher = new FileSystemWatcher(directory);
            FileSystemEventHandler onChange = OnChangeEnqueue(watcher);
            RenamedEventHandler onRenamed = OnRenamedEnqueue(watcher);
            watcher.NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName
                                                               | NotifyFilters.LastAccess | NotifyFilters.LastWrite;
            watcher.Changed += onChange;
            watcher.Created += onChange;
            watcher.Deleted += onChange;
            watcher.Renamed += onRenamed;

            DirectoryToWatcher[directory] = watcher;
            watcher.EnableRaisingEvents = true;
        }

        private FileSystemEventHandler OnChangeEnqueue(FileSystemWatcher watcher)
        {
            return (sender, args) =>
            {
                string directory = watcher.Path;
                string file = args.FullPath;

                FileSystemChangeQueue.Enqueue(new FileSystemChange(directory, file));           
            };
        }

        private RenamedEventHandler OnRenamedEnqueue(FileSystemWatcher watcher)
        {
            return (sender, args) =>
            {
                string directory = watcher.Path;
                string file = args.FullPath;

                FileSystemChangeQueue.Enqueue(new FileSystemChange(directory, file));
            };
        }
    }
}
