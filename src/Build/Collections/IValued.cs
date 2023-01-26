// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

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
