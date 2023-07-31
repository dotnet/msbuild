// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
