// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using NuGet.LibraryModel;
using NuGet.ProjectModel;
using NuGet.Versioning;

namespace Microsoft.NET.Build.Tasks
{
    internal class LockFileLookup
    {
        private readonly Dictionary<PackageCacheKey, LockFileLibrary> _packages;
        private readonly Dictionary<string, LockFileLibrary> _projects;

        public LockFileLookup(LockFile lockFile)
        {
            _packages = new Dictionary<PackageCacheKey, LockFileLibrary>();
            _projects = new Dictionary<string, LockFileLibrary>();

            foreach (var library in lockFile.Libraries)
            {
                var libraryType = LibraryType.Parse(library.Type);

                if (libraryType == LibraryType.Package)
                {
                    _packages[new PackageCacheKey(library.Name, library.Version)] = library;
                }
                if (libraryType == LibraryType.Project)
                {
                    _projects[library.Name.ToLowerInvariant()] = library;
                }
            }
        }

        public LockFileLibrary GetProject(string name)
        {
            LockFileLibrary project;
            if (_projects.TryGetValue(name.ToLowerInvariant(), out project))
            {
                return project;
            }

            return null;
        }

        public LockFileLibrary GetPackage(string id, NuGetVersion version)
        {
            LockFileLibrary package;
            if (_packages.TryGetValue(new PackageCacheKey(id, version), out package))
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

        private class PackageCacheKey : Tuple<string, NuGetVersion>
        {
            public PackageCacheKey(string id, NuGetVersion version)
                : base(id.ToLowerInvariant(), version)
            {
            }
        }
    }
}
