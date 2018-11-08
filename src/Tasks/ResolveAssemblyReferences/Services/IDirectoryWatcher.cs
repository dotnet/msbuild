using System.Collections.Generic;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Services
{
    internal interface IDirectoryWatcher
    {
        List<FileSystemChange> RecentChanges { get; }

        void Watch(string directory);
    }
}
