using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Extensions.ProjectModel.Compilation
{
    public struct LibraryAsset
    {
        public string Name { get; }
        public string RelativePath { get; }
        public string ResolvedPath { get; }

        public LibraryAsset(string name, string relativePath, string resolvedPath)
        {
            Name = name;
            RelativePath = relativePath;
            ResolvedPath = resolvedPath;
        }
    }
}
