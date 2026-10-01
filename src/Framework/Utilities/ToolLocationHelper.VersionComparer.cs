// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

#nullable disable

namespace Microsoft.Build.Utilities;

public static partial class ToolLocationHelper
{
    /// <summary>
    /// Compares framework version strings of the format v4.1.2.3
    /// major version and minor version are mandatory others are optional
    /// </summary>
    private class VersionComparer : IComparer<string>
    {
        public static VersionComparer Instance { get; } = new VersionComparer();

        private VersionComparer()
        {
        }

        public int Compare(string versionX, string versionY)
        {
#if NET
            return Version.Parse(versionX.AsSpan(1)).CompareTo(Version.Parse(versionY.AsSpan(1)));
#else
            return new Version(versionX.Substring(1)).CompareTo(new Version(versionY.Substring(1)));
#endif
        }
    }
}
