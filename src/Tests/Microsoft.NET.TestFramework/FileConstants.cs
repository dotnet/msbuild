// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.TestFramework
{
    public static class FileConstants
    {
        public static readonly string DynamicLibPrefix = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "" : "lib";

        public static readonly string DynamicLibSuffix = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".dll" :
                                                         RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? ".dylib" : ".so";
        public static readonly string UserProfileFolder = Environment.GetEnvironmentVariable(
                                                                RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ?
                                                                    "USERPROFILE" : "HOME");
    }
}
