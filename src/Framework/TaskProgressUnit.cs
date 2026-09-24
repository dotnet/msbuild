// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Describes the unit used by task progress updates.
    /// </summary>
    public enum TaskProgressUnit
    {
        /// <summary>
        /// The operation does not have a specified unit.
        /// </summary>
        Unspecified,

        /// <summary>
        /// The operation measures discrete items.
        /// </summary>
        Items,

        /// <summary>
        /// The operation measures bytes.
        /// </summary>
        Bytes,
    }
}
