// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
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
