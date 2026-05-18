// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Experimental.BuildCheck;

internal static class BuildCheckUtilities
{
    internal static string RootEvaluatedPath(string path, string projectFilePath)
    {
        if (!Path.IsPathRooted(path))
        {
            path = Path.Combine(Path.GetDirectoryName(projectFilePath)!, path);
        }
        // Normalize the path to avoid false negatives due to different path representations.
        path = FileUtilities.NormalizePath(path)!;

        return path;
    }
}
