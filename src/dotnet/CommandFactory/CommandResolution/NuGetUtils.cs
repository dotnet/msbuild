// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;

namespace Microsoft.DotNet.CommandFactory
{
    internal static class NuGetUtils
    {
        public static bool IsPlaceholderFile(string path)
        {
            return string.Equals(Path.GetFileName(path), PackagingCoreConstants.EmptyFolder, StringComparison.Ordinal);
        }

        public static IEnumerable<LockFileItem> FilterPlaceHolderFiles(this IEnumerable<LockFileItem> files)
        {
            return files.Where(f => !IsPlaceholderFile(f.Path));
        }
    }
}
