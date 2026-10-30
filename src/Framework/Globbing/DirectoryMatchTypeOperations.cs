// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Adapted from Jeremy Kuhne's JeremyKuhne/touki at commit 3c58c6a4edc814abbe55d18f607a80982437d87b:
// https://github.com/JeremyKuhne/touki/blob/3c58c6a4edc814abbe55d18f607a80982437d87b/touki/Touki/Io/DirectoryMatchTypeOperations.cs
// Copyright (c) 2025 Jeremy W Kuhne. SPDX-License-Identifier: MIT.

using Microsoft.Build.Shared.Globbing;

namespace Microsoft.Build.Shared;

internal partial class FileMatcher
{
    internal sealed partial class GlobExpander
    {
        private static class DirectoryMatchTypeOperations
        {
            public static DirectoryMatchType Normalize(DirectoryMatchType matchType) => matchType switch
            {
                DirectoryMatchType.NoDescendantFilesMatch => matchType,
                DirectoryMatchType.AllDescendantFilesMatch => matchType,
                _ => DirectoryMatchType.MayContainMatchingFiles
            };

            public static DirectoryMatchType Not(DirectoryMatchType matchType) => Normalize(matchType) switch
            {
                DirectoryMatchType.NoDescendantFilesMatch => DirectoryMatchType.AllDescendantFilesMatch,
                DirectoryMatchType.AllDescendantFilesMatch => DirectoryMatchType.NoDescendantFilesMatch,
                _ => DirectoryMatchType.MayContainMatchingFiles
            };

            public static DirectoryMatchType And(DirectoryMatchType left, DirectoryMatchType right)
            {
                left = Normalize(left);
                right = Normalize(right);
                if (left == DirectoryMatchType.NoDescendantFilesMatch
                    || right == DirectoryMatchType.NoDescendantFilesMatch)
                {
                    return DirectoryMatchType.NoDescendantFilesMatch;
                }

                return left == DirectoryMatchType.AllDescendantFilesMatch
                    && right == DirectoryMatchType.AllDescendantFilesMatch
                        ? DirectoryMatchType.AllDescendantFilesMatch
                        : DirectoryMatchType.MayContainMatchingFiles;
            }
        }
    }
}
