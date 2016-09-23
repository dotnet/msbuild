// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using NuGet.Frameworks;
using NuGet.LibraryModel;

namespace Microsoft.DotNet.ProjectModel.Resolution
{
    public static class UnresolvedDependencyProvider
    {
        public static LibraryDescription GetDescription(ProjectLibraryDependency libraryDependency, NuGetFramework targetFramework)
        {
            return new LibraryDescription(
                new LibraryIdentity(
                    libraryDependency.Name,
                    libraryDependency.LibraryRange.VersionRange?.MinVersion,
                    LibraryType.Unresolved),
                hash: null,
                path: null,
                dependencies: Enumerable.Empty<ProjectLibraryDependency>(),
                framework: targetFramework,
                resolved: false,
                compatible: true);
        }
    }
}
