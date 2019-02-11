// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

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
