// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;

namespace Microsoft.DotNet.MSBuildSdkResolver
{
    internal static class NETCoreSdkResolver
    {
        public sealed class Result
        {
            /// <summary>
            /// Path to .NET Core SDK selected by hostfxr (e.g. C:\Program Files\dotnet\sdk\2.1.300).
            /// </summary>
            public string ResolvedSdkDirectory;

            /// <summary>
            /// Path to global.json file that impacted resolution.
            /// </summary>
            public string GlobalJsonPath;

            public void Initialize(Interop.hostfxr_resolve_sdk2_result_key_t key, string value)
            {
                switch (key)
                {
                    case Interop.hostfxr_resolve_sdk2_result_key_t.resolved_sdk_dir:
                        ResolvedSdkDirectory = value;
                        break;
                    case Interop.hostfxr_resolve_sdk2_result_key_t.global_json_path:
                        GlobalJsonPath = value;
                        break;
                }
            }
        }

        public static Result ResolveSdk(
            string dotnetExeDirectory, 
            string globalJsonStartDirectory,
            bool disallowPrerelease = false)
        {
            var result = new Result();
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
