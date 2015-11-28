// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.DotNet.ProjectModel.Graph;
using NuGet;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace Microsoft.DotNet.ProjectModel.Resolution
{
    public class ReferenceAssemblyDependencyResolver
    {
        public ReferenceAssemblyDependencyResolver(FrameworkReferenceResolver frameworkReferenceResolver)
        {
            FrameworkResolver = frameworkReferenceResolver;
        }

        private FrameworkReferenceResolver FrameworkResolver { get; set; }

        public LibraryDescription GetDescription(LibraryRange libraryRange, NuGetFramework targetFramework)
        {
            if (!LibraryType.ReferenceAssembly.CanSatisfyConstraint(libraryRange.Target))
            {
                return null;
            }

            var name = libraryRange.Name;
            var version = libraryRange.VersionRange?.MinVersion;

            string path;
            Version assemblyVersion;

            if (!FrameworkResolver.TryGetAssembly(name, targetFramework, out path, out assemblyVersion))
            {
                return null;
            }

            if (version == null || version.Version == assemblyVersion)
            {
                return new LibraryDescription(
                    new LibraryIdentity(libraryRange.Name, new NuGetVersion(assemblyVersion), LibraryType.ReferenceAssembly),
                    string.Empty, // Framework assemblies don't have hashes
                    path,
                    Enumerable.Empty<LibraryRange>(),
                    targetFramework,
                    resolved: true,
                    compatible: true);
            }

            return null;
        }
    }
}
