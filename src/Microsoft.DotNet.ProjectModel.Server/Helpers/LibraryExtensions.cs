// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.ProjectModel.Graph;

namespace Microsoft.DotNet.ProjectModel.Server.Helpers
{
    public static class LibraryExtensions
    {
        public static string GetUniqueName(this LibraryDescription library)
        {
            var identity = library.Identity;
            return identity.Type != LibraryType.ReferenceAssembly ? identity.Name : $"fx/{identity.Name}";
        }

        public static string GetUniqueName(this LibraryRange range)
        {
            return range.Target != LibraryType.ReferenceAssembly ? range.Name : $"fx/{range.Name}";
        }
    }
}
