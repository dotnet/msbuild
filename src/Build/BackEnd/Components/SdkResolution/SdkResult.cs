// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

using SdkReference = Microsoft.Build.Framework.SdkReference;
using SdkResultBase = Microsoft.Build.Framework.SdkResult;

namespace Microsoft.Build.BackEnd.SdkResolution
{
    /// <summary>
    /// An internal implementation of <see cref="Microsoft.Build.Framework.SdkResult"/>.
    /// </summary>
    internal sealed class SdkResult : SdkResultBase
    {
        public SdkResult(SdkReference sdkReference, IEnumerable<string> errors, IEnumerable<string> warnings)
        {
            Success = false;
            Sdk = sdkReference;
            Errors = errors;
            Warnings = warnings;
        }

        public SdkResult(SdkReference sdkReference, string path, string version, IEnumerable<string> warnings)
        {
            Success = true;
            Sdk = sdkReference;
            Path = path;
            Version = version;
            Warnings = warnings;
        }

        public Construction.ElementLocation ElementLocation { get; set; }

        public IEnumerable<string> Errors { get; }

        public string Path { get; }

        public SdkReference Sdk { get; }

        public string Version { get; }

        public IEnumerable<string> Warnings { get; }
    }
}
