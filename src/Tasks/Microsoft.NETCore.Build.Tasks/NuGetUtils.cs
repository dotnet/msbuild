// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Packaging.Core;

namespace Microsoft.NETCore.Build.Tasks
{
    internal static class NuGetUtils
    {
        public static bool IsPlaceholderFile(string path)
        {
            return string.Equals(Path.GetFileName(path), PackagingCoreConstants.EmptyFolder, StringComparison.Ordinal);
        }

        public static IEnumerable<string> FilterPlaceHolderFiles(this IEnumerable<string> files)
        {
            return files.Where(f => !IsPlaceholderFile(f));
        }

        public static string GetLockFileLanguageName(string projectLanguage)
        {
            switch (projectLanguage)
            {
                case "C#": return "cs";
                case "F#": return "fs";
                default: return projectLanguage?.ToLowerInvariant();
            }
        }
    }
}
