// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using NuGet.LibraryModel;

namespace Microsoft.DotNet.Internal.ProjectModel
{
    internal class ProjectLibraryDependency : LibraryDependency
    {
        public string SourceFilePath { get; set;  }
        public int SourceLine { get; set; }
        public int SourceColumn { get; set; }

        public ProjectLibraryDependency()
        {
        }

        public ProjectLibraryDependency(LibraryRange libraryRange)
        {
            LibraryRange = libraryRange;
        }
    }
}
