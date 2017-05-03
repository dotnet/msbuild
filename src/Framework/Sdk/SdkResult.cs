// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.Framework
{
    /// <summary>
    ///     An abstract interface class to indicate SDK resolver success or failure.
    ///     <remarks>
    ///         Note: Use <see cref="SdkResultFactory" /> to create instances of this class. Do not
    ///         inherit from this class.
    ///     </remarks>
    /// </summary>
    public abstract class SdkResult
    {
        /// <summary>
        ///     Indicates the resolution was successful.
        /// </summary>
        public bool Success { get; protected set; }
    }
}
