// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Extensions.ProjectModel.Graph;
using NuGet;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace Microsoft.Extensions.ProjectModel.Resolution
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
