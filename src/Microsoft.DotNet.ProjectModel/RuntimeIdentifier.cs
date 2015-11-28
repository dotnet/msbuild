// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace Microsoft.DotNet.ProjectModel
{
    public static class RuntimeIdentifier
    {
        public static string Current { get; } = DetermineRID();

        private static string DetermineRID()
        {
            // TODO: Not this, obviously. Do proper RID detection
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return "win7-x64";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return "ubuntu.14.04-x64";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return "osx.10.10-x64";
            }
            return "unknown";
        }
    }
}
