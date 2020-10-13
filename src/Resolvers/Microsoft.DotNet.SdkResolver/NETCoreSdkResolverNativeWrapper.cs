// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;

#nullable disable

namespace Microsoft.DotNet.DotNetSdkResolver
{
    internal static class NETCoreSdkResolverNativeWrapper
    {
        public static SdkResolutionResult ResolveSdk(
            string dotnetExeDirectory, 
            string globalJsonStartDirectory,
            bool disallowPrerelease = false)
        {
            var result = new SdkResolutionResult();
            var flags = disallowPrerelease ? Interop.hostfxr_resolve_sdk2_flags_t.disallow_prerelease : 0;

            int errorCode = Interop.RunningOnWindows
                ? Interop.Windows.hostfxr_resolve_sdk2(dotnetExeDirectory, globalJsonStartDirectory, flags, result.Initialize)
                : Interop.Unix.hostfxr_resolve_sdk2(dotnetExeDirectory, globalJsonStartDirectory, flags, result.Initialize);

            Debug.Assert((errorCode == 0) == (result.ResolvedSdkDirectory != null));
            return result;
        }

        private sealed class SdkList
        {
            public string[] Entries;

            public void Initialize(int count, string[] entries)
            {
                entries = entries ?? Array.Empty<string>();
                Debug.Assert(count == entries.Length);
                Entries = entries;
            }
        }

        public static string[] GetAvailableSdks(string dotnetExeDirectory)
        {
            var list = new SdkList();

            int errorCode = Interop.RunningOnWindows
                ? Interop.Windows.hostfxr_get_available_sdks(dotnetExeDirectory, list.Initialize)
                : Interop.Unix.hostfxr_get_available_sdks(dotnetExeDirectory, list.Initialize);

            return list.Entries;
        }
    }
}
