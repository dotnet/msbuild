// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.Collections
{
    /// <summary>
    /// Interface allowing values of things to be gotten.
    /// </summary>
    /// <remarks>
    /// This can be internal as it is a constraint only on internal collections.
    /// </remarks>
    internal interface IValued
    {
        /// <summary>
        /// Returns some value of a thing
        /// </summary>
        string EscapedValue { get; }
    }
}
