// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Configurer
{
    public static class ToolPackageFolderPathCalculator
    {
        private const string NestedToolPackageFolderName = ".store";
        public static string GetToolPackageFolderPath(string toolsShimPath)
        {
            return Path.Combine(toolsShimPath, NestedToolPackageFolderName);
        }
    }
}
