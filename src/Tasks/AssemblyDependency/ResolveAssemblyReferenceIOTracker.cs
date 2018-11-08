using System;
using System.Collections.Generic;

namespace Microsoft.Build.Tasks
{
    internal class ResolveAssemblyReferenceIOTracker
    {
        internal HashSet<string> TrackedPaths { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public void Track(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            TrackedPaths.Add(path);
        }
    }
}
