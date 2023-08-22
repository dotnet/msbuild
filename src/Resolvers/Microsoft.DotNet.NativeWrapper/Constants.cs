// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.NativeWrapper
{
    internal static class Constants
    {
        public const string HostFxr = "hostfxr";
        public const string DotNet = "dotnet";
        public const string PATH = "PATH";
        public const string DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR = "DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR";

        public static readonly string ExeSuffix =
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : string.Empty;

        public static class RuntimeProperty
        {
            public const string HostFxrPath = "HOSTFXR_PATH";
        }
    }
}
