// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Extension methods for AbsolutePath.
    /// </summary>
    internal static class AbsolutePathExtensions
    {
        internal static string[] ToStringArray(this AbsolutePath[] absolutePaths)
        {
            string[] stringPaths = new string[absolutePaths.Length];
            for (int i = 0; i < absolutePaths.Length; i++)
            {
                stringPaths[i] = absolutePaths[i].Value;
            }
            return stringPaths;
        }
    }
}
