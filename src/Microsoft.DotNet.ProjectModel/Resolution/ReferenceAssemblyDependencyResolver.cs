// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using NuGet.Frameworks;
using NuGet.LibraryModel;
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

        public LibraryDescription GetDescription(ProjectLibraryDependency libraryDependency, NuGetFramework targetFramework)
        {
            if (!libraryDependency.LibraryRange.TypeConstraintAllows(LibraryDependencyTarget.Reference))
            {
                return null;
            }

            var name = libraryDependency.Name;
            var version = libraryDependency.LibraryRange.VersionRange?.MinVersion;

            string path;
            Version assemblyVersion;

            if (!FrameworkResolver.TryGetAssembly(name, targetFramework, out path, out assemblyVersion))
            {
                return null;
            }

            return new LibraryDescription(
                new LibraryIdentity(libraryDependency.Name, new NuGetVersion(assemblyVersion), LibraryType.Reference),
                string.Empty, // Framework assemblies don't have hashes
                path,
                Enumerable.Empty<ProjectLibraryDependency>(),
                targetFramework,
                resolved: true,
                compatible: true);
        }
    }
}
