// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

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
