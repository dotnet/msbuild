// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

//only Microsoft.DotNet.NativeWrapper (net7.0) has nullables disabled
#pragma warning disable IDE0240 // Remove redundant nullable directive
#nullable disable
#pragma warning restore IDE0240 // Remove redundant nullable directive

namespace Microsoft.DotNet.NativeWrapper
{
    public class SdkResolutionResult
    {
        /// <summary>
        /// Path to .NET Core SDK selected by hostfxr (e.g. C:\Program Files\dotnet\sdk\2.1.300).
        /// </summary>
        public string ResolvedSdkDirectory;

        /// <summary>
        /// Path to global.json file that impacted resolution.
        /// </summary>
        public string GlobalJsonPath;

        /// <summary>
        /// The .NET SDK version specified in <strong>global.json</strong>.
        /// </summary>
        public string RequestedVersion;

        /// <summary>
        /// True if a global.json was found but there was no compatible SDK, so it was ignored. 
        /// </summary>
        public bool FailedToResolveSDKSpecifiedInGlobalJson;

        internal void Initialize(Interop.hostfxr_resolve_sdk2_result_key_t key, string value)
        {
            switch (key)
            {
                case Interop.hostfxr_resolve_sdk2_result_key_t.resolved_sdk_dir:
                    ResolvedSdkDirectory = value;
                    break;
                case Interop.hostfxr_resolve_sdk2_result_key_t.global_json_path:
                    GlobalJsonPath = value;
                    break;
                case Interop.hostfxr_resolve_sdk2_result_key_t.requested_version:
                    RequestedVersion = value;
                    break;
            }
        }
    }
}
