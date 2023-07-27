// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks
{
    internal static class PackageReferenceConverter
    {
        public static IEnumerable<string> GetPackageIds(ITaskItem[] packageReferences)
        {
            if (packageReferences == null)
            {
                return Enumerable.Empty<string>();
            }

            return packageReferences
                .Select(p => p.ItemSpec)
                .ToArray();
        }
    }
}
