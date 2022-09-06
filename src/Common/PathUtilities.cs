// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable disable

using System.IO;
using System.Runtime.InteropServices;

namespace Microsoft.DotNet;

static class PathUtilities
{
    const int S_IRUSR = 256;
    const int S_IWUSR = 128;
    const int S_IXUSR = 64;
    const int S_IRWXU = S_IRUSR | S_IWUSR | S_IXUSR; // 700 (octal) Permissions 

    [DllImport("libc", SetLastError = true)]
    private static extern int mkdir(string pathname, int mode);
    public static string CreateTempSubdirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            mkdir(path, S_IRWXU);
        }
        else
        {
            Directory.CreateDirectory(path);
        }
        return path;
    }
}
