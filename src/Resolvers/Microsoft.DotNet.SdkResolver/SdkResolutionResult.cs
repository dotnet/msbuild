using System;
using System.Collections.Generic;
using System.Text;

#nullable disable

namespace Microsoft.DotNet.DotNetSdkResolver
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
            }
        }
    }
}
