// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using NuGet.Versioning;

namespace Microsoft.DotNet.ProjectModel.Graph
{
    public class LockFileLookup
    {
        // REVIEW: Case sensitivity?
        private readonly Dictionary<Tuple<string, NuGetVersion>, LockFilePackageLibrary> _packages;
        private readonly Dictionary<string, LockFileProjectLibrary> _projects;

        public LockFileLookup(LockFile lockFile)
        {
            _packages = new Dictionary<Tuple<string, NuGetVersion>, LockFilePackageLibrary>();
            _projects = new Dictionary<string, LockFileProjectLibrary>();

            foreach (var library in lockFile.PackageLibraries)
            {
                _packages[Tuple.Create(library.Name, library.Version)] = library;
            }

            foreach (var libary in lockFile.ProjectLibraries)
            {
                _projects[libary.Name] = libary;
            }
        }

        public LockFileProjectLibrary GetProject(string name)
        {
            LockFileProjectLibrary project;
            if (_projects.TryGetValue(name, out project))
            {
                return project;
            }

            return null;
        }

        public LockFilePackageLibrary GetPackage(string id, NuGetVersion version)
        {
            LockFilePackageLibrary package;
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
