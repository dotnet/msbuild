// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

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