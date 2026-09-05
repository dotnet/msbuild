// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Versioning;
using Microsoft.Build.Shared;
#nullable disable

namespace Microsoft.Build.Utilities
{
    [SupportedOSPlatform("windows")]
    internal sealed class AssemblyFoldersEx : Microsoft.Build.Shared.AssemblyFoldersEx<AssemblyFoldersExInfo>
    {
        internal AssemblyFoldersEx(
            string registryKeyRoot,
            string targetRuntimeVersion,
            string registryKeySuffix,
            string osVersion,
            string platform,
            GetRegistrySubKeyNames getRegistrySubKeyNames,
            GetRegistrySubKeyDefaultValue getRegistrySubKeyDefaultValue,
            System.Reflection.ProcessorArchitecture targetProcessorArchitecture,
            OpenBaseKey openBaseKey)
            : base(
                registryKeyRoot,
                targetRuntimeVersion,
                registryKeySuffix,
                osVersion,
                platform,
                getRegistrySubKeyNames,
                getRegistrySubKeyDefaultValue,
                targetProcessorArchitecture,
                openBaseKey,
                static (hive, view, key, path, version) => new AssemblyFoldersExInfo(hive, view, key, path, version))
        {
        }
    }
}
