// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using NuGet.LibraryModel;
using NuGet.ProjectModel;
using NuGet.Versioning;

namespace Microsoft.DotNet.ProjectModel.Graph
{
    public class LockFileLookup
    {
        // REVIEW: Case sensitivity?
        private readonly Dictionary<Tuple<string, NuGetVersion>, LockFileLibrary> _packages;
        private readonly Dictionary<string, LockFileLibrary> _projects;

        public LockFileLookup(LockFile lockFile)
        {
            _packages = new Dictionary<Tuple<string, NuGetVersion>, LockFileLibrary>();
            _projects = new Dictionary<string, LockFileLibrary>();

            foreach (var library in lockFile.Libraries)
            {
                var libraryType = LibraryType.Parse(library.Type);

                if (libraryType == LibraryType.Package)
                {
                    _packages[Tuple.Create(library.Name, library.Version)] = library;
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
            if (_packages.TryGetValue(Tuple.Create(id, version), out package))
            {
                return package;
            }

            return null;
        }

        public void Clear()
        {
            _packages.Clear();
            _projects.Clear();
        }
    }
}
