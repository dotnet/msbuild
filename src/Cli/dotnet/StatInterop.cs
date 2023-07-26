// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli
{
    // https://github.com/dotnet/runtime/blob/main/src/libraries/Common/src/Interop/Unix/System.Native/Interop.Stat.cs

    internal static class StatInterop
    {
        // Even though csc will by default use a sequential layout, a CS0649 warning as error
        // is produced for un-assigned fields when no StructLayout is specified.
        //
        // Explicitly saying Sequential disables that warning/error for consumers which only
        // use Stat in debug builds.
        [StructLayout(LayoutKind.Sequential)]
        internal struct FileStatus
        {
            internal FileStatusFlags Flags;
            internal int Mode;
            internal uint Uid;
            internal uint Gid;
            internal long Size;
            internal long ATime;
            internal long ATimeNsec;
            internal long MTime;
            internal long MTimeNsec;
            internal long CTime;
            internal long CTimeNsec;
            internal long BirthTime;
            internal long BirthTimeNsec;
            internal long Dev;
            internal long RDev;
            internal long Ino;
            internal uint UserFlags;
        }

        [Flags]
        internal enum Permissions
        {
            Mask = S_IRWXU | S_IRWXG | S_IRWXO,

            S_IRWXU = S_IRUSR | S_IWUSR | S_IXUSR,
            S_IRUSR = 0x100,
            S_IWUSR = 0x80,
            S_IXUSR = 0x40,

            S_IRWXG = S_IRGRP | S_IWGRP | S_IXGRP,
            S_IRGRP = 0x20,
            S_IWGRP = 0x10,
            S_IXGRP = 0x8,

            S_IRWXO = S_IROTH | S_IWOTH | S_IXOTH,
            S_IROTH = 0x4,
            S_IWOTH = 0x2,
            S_IXOTH = 0x1,

            S_IXUGO = S_IXUSR | S_IXGRP | S_IXOTH,
        }

        [Flags]
        internal enum FileStatusFlags
        {
            None = 0,
            HasBirthTime = 1,
        }

        [DllImport("libSystem.Native", EntryPoint = "SystemNative_LStat", SetLastError = true)]
        internal static extern int LStat(string path, out FileStatus output);
    }
}
