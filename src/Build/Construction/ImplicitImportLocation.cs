// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.Construction
{
    /// <summary>
    /// Represents the location of an implicit import.
    /// </summary>
    public enum ImplicitImportLocation
    {
        /// <summary>
        /// The import is not implicitly added and is explicitly added in a user-specified location.
        /// </summary>
        None,
        /// <summary>
        /// The import was implicitly added at the top of the project.
        /// </summary>
        Top,
        /// <summary>
        /// The import was implicitly added at the bottom of the project.
        /// </summary>
        Bottom
    }
}
