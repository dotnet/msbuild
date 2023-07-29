// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable IDE0240 // Nullable directive is redundant (when file is included to a project that already enables nullable

#nullable enable


namespace Microsoft.DotNet;

static class PathUtilities
{
    const int S_IRUSR = 256;
    const int S_IWUSR = 128;
    const int S_IXUSR = 64;
    const int S_IRWXU = S_IRUSR | S_IWUSR | S_IXUSR; // 700 (octal) Permissions 

    const int MAX_NUM_DIRECTORY_CREATE_RETRIES = 2;

    public static string CreateTempSubdirectory()
    {
        return CreateTempSubdirectoryRetry(0);
    }

    [DllImport("libc", SetLastError = true)]
    private static extern int mkdir(string pathname, int mode);
    private static string CreateTempSubdirectoryRetry(int attemptNo)
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            int mkdirStatusCode = mkdir(path, S_IRWXU);
            if (mkdirStatusCode != 0)
            {
                int errno = Marshal.GetLastWin32Error();
                if (Directory.Exists(path) && attemptNo < MAX_NUM_DIRECTORY_CREATE_RETRIES)
                {
                    return CreateTempSubdirectoryRetry(attemptNo + 1);
                }
                else
                    throw new IOException($"Failed to create a temporary subdirectory {path} with mkdir, error code: {errno}");
            }
        }
        else
        {
            Directory.CreateDirectory(path);
        }
        return path;
    }
}
