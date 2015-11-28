// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using Microsoft.DotNet.ProjectModel.Graph;
using NuGet.Frameworks;

namespace Microsoft.DotNet.ProjectModel.Resolution
{
    public static class UnresolvedDependencyProvider
    {
        public static LibraryDescription GetDescription(LibraryRange libraryRange, NuGetFramework targetFramework)
        {
            return new LibraryDescription(
                new LibraryIdentity(libraryRange.Name, libraryRange.VersionRange?.MinVersion, libraryRange.Target),
                hash: null,
                path: null,
                dependencies: Enumerable.Empty<LibraryRange>(),
                framework: targetFramework,
                resolved: false,
                compatible: true);
        }
    }
}
