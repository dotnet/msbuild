// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Microsoft.DotNet.Tools.Test.Utilities
{
    public static class TestPathUtilities
    {
        public static string CreateAbsolutePath(string directoryName = null)
            => Path.Combine(
                RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? "A:"
                    : Path.DirectorySeparatorChar.ToString(),
                directoryName ?? "testworkdir") + Path.DirectorySeparatorChar;
    }
}
