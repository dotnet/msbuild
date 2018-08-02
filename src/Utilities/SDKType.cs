// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.Utilities
{
    /// <summary>
    /// Type of SDK
    /// </summary>
    public enum SDKType
    {
        /// <summary>
        /// Not specified
        /// </summary>
        Unspecified,

        /// <summary>
        /// Traditional 3rd party SDK
        /// </summary>
        External,

        /// <summary>
        /// Platform extension SDK
        /// </summary>
        Platform,

        /// <summary>
        /// Framework extension SDK
        /// </summary>
        Framework
    }
}
