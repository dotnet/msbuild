// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Tasks
{
    public sealed class ExecutableExtension
    {
        public static string ForRuntimeIdentifier(string runtimeIdentifier)
        {
            if (runtimeIdentifier.StartsWith("win", StringComparison.OrdinalIgnoreCase))
            {
                return ".exe";
            }
            return string.Empty;
        }
    }
}
