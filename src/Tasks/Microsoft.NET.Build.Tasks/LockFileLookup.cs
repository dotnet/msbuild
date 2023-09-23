// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using NuGet.LibraryModel;
using NuGet.ProjectModel;
using NuGet.Versioning;

namespace Microsoft.NET.Build.Tasks
{
    internal class LockFileLookup
    {
        private readonly Dictionary<KeyValuePair<string, NuGetVersion>, LockFileLibrary> _packages;
        private readonly Dictionary<string, LockFileLibrary> _projects;

        public LockFileLookup(LockFile lockFile)
        {
            _packages = new Dictionary<KeyValuePair<string, NuGetVersion>, LockFileLibrary>(PackageCacheKeyComparer.Instance);
            _projects = new Dictionary<string, LockFileLibrary>(StringComparer.OrdinalIgnoreCase);

            foreach (var library in lockFile.Libraries)
            {
                var libraryType = LibraryType.Parse(library.Type);

                if (libraryType == LibraryType.Package)
                {
                    _packages[new KeyValuePair<string, NuGetVersion>(library.Name, library.Version)] = library;
                }
                if (libraryType == LibraryType.Project)
                {
                    _projects[library.Name] = library;
                }
            }
        }

        public LockFileLibrary GetProject(string name)
        {
            LockFileLibrary project;
            if (_projects.TryGetValue(name, out project))
            {
                return project;
            }

            return null;
        }

        public LockFileLibrary GetPackage(string id, NuGetVersion version)
        {
            LockFileLibrary package;
            if (_packages.TryGetValue(new KeyValuePair<string, NuGetVersion>(id, version), out package))
            {
                return package;
            }

            return null;
        }

        public bool TryGetLibrary(LockFileTargetLibrary targetLibrary, out LockFileLibrary library)
        {
            var libraryType = LibraryType.Parse(targetLibrary.Type);
            if (libraryType == LibraryType.Package)
            {
                library = GetPackage(targetLibrary.Name, targetLibrary.Version);
            }
            else
            {
                library = GetProject(targetLibrary.Name);
            }

            return library != null;
        }

        public void Clear()
        {
            _packages.Clear();
            _projects.Clear();
        }

        private class PackageCacheKeyComparer : IEqualityComparer<KeyValuePair<string, NuGetVersion>>
        {
            public static readonly PackageCacheKeyComparer Instance = new();

            private PackageCacheKeyComparer()
            {
            }

            public bool Equals(KeyValuePair<string, NuGetVersion> x, KeyValuePair<string, NuGetVersion> y)
            {
                return string.Equals(x.Key, y.Key, StringComparison.OrdinalIgnoreCase) &&
                    x.Value == y.Value;
            }

            public int GetHashCode(KeyValuePair<string, NuGetVersion> obj)
            {
                var hashCode = 0;
                if (obj.Key != null)
                {
                    hashCode ^= StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Key);
                }

                hashCode ^= obj.Value?.GetHashCode() ?? 0;
                return hashCode;
            }
        }
    }
}
