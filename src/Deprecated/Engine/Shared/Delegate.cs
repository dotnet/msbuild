// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.BuildEngine.Shared
{
    /// <summary>
    /// GetDirectories delegate
    /// </summary>
    /// <param name="path">The path to get directories for.</param>
    /// <param name="pattern">The pattern to search for.</param>
    /// <returns>An array of directories.</returns>
    internal delegate string[] GetDirectories(string path, string pattern);
}
